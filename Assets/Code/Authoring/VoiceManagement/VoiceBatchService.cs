using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text;
using InnoEngine.Audio;
using InnoEngine.Graphs;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.ScriptProjection;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Audio;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Model.Localization;
using Inno.VisualNovel.Player.Localization;
using Inno.VisualNovel.Authoring.Diagnostics;

namespace Inno.VisualNovel.Authoring.VoiceManagement;

/// <summary>Matches stable dialogue identities to native audio with explicit review and stale-source rejection.</summary>
public sealed class VoiceBatchService(StoryNodeDataCodec codec)
{
    /// <summary>Reviews filename matches without changing story, assets, project libraries or existing bindings.</summary>
    /// <param name="draft">The current shared chapter draft.</param>
    /// <param name="project">The game's explicit reusable cue library.</param>
    /// <param name="clips">Canonical imported candidate clips.</param>
    /// <param name="prefix">An optional exact filename prefix removed before matching the dialogue text ID.</param>
    /// <param name="replaceExisting">Whether an existing different source voice may be replaced.</param>
    /// <param name="language">The recording language, or null for the source language.</param>
    /// <returns>An immutable review containing ready and unavailable lines.</returns>
    public VoiceBatchPreview Preview(
        StoryDraftState draft,
        NovelProjectAsset project,
        IEnumerable<AudioClipAsset> clips,
        string prefix = "",
        bool replaceExisting = false,
        string? language = null
    ) {
        string sourceLanguage = project.localization?.sourceLanguage ?? project.defaultLanguage;
        language ??= sourceLanguage;
        bool localized = language != sourceLanguage;
        var characterNames = project.characters.ToDictionary(static item => item.resourceId, static item => item.displayName, StringComparer.Ordinal);
        var languageView = new VoiceBatchLanguage(project.localization, language, localized, characterNames);
        var candidates = clips.Take(10001).DistinctBy(static clip => clip.identity.persistentId).ToArray();
        if (candidates.Length > 10000) throw new InvalidOperationException("Voice import is limited to 10000 clips per reviewed batch.");
        var names = candidates.Where(clip => Path.GetFileNameWithoutExtension(clip.assetPath.localPath).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .GroupBy(clip => Path.GetFileNameWithoutExtension(clip.assetPath.localPath)[prefix.Length..], StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var lines = Lines(draft).ToArray();
        var duplicates = lines.GroupBy(static item => item.line.textId, StringComparer.OrdinalIgnoreCase).Where(static group => group.Count() > 1)
            .Select(static group => group.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rows = new List<VoiceBatchRow>();
        foreach (var item in lines)
        {
            DialogueNode line = item.line; AudioClipAsset? clip = null; string cue = "", message; bool applicable = false;
            var recording = languageView.Read(line);
            if (string.IsNullOrWhiteSpace(line.textId) || duplicates.Contains(line.textId)) message = "台词 ID 为空或在不区分大小写时重复，先修复身份。";
            else if (recording.issue.Length > 0) message = recording.issue;
            else if (!replaceExisting && recording.cue.Length > 0) message = "保留现有配音；启用替换后重新预览。";
            else if (!names.TryGetValue(line.textId, out var matches)) message = "缺少与稳定台词 ID 同名的录音。";
            else if (matches.Length != 1) message = "同名录音不止一个，无法确定配音。";
            else
            {
                clip = matches[0];
                if (clip.isMissing || clip.metadata is null || clip.duration <= TimeSpan.Zero || clip.assetPath.localPath.Length == 0) message = "录音未成功导入或为空。";
                else
                {
                    cue = project.audio.FirstOrDefault(existing => existing.clip?.identity.persistentId == clip.identity.persistentId && existing.channel == NovelAudioChannel.Voice && !existing.loop)?.resourceId
                        ?? "voice." + clip.identity.persistentId.ToString("N");
                    if (project.audio.Any(existing => existing.resourceId == cue && (existing.clip?.identity.persistentId != clip.identity.persistentId || existing.channel != NovelAudioChannel.Voice || existing.loop)))
                        message = "生成的语音资源 ID 与游戏库中另一项冲突。";
                    else { applicable = recording.cue != cue; message = applicable ? "可关联" : "已关联相同录音。"; }
                }
            }
            rows.Add(new(item.node, line.textId, recording.speaker, recording.text, recording.cue, cue, clip?.identity.persistentId ?? Guid.Empty,
                clip?.contentVersion ?? 0, clip?.assetPath.ToString() ?? "", applicable, message));
        }
        var usedNames = lines.Where(static item => item.line.characterId.Length > 0 && item.line.speaker.Length == 0).Select(static item => item.line.characterId).Distinct(StringComparer.Ordinal)
            .ToDictionary(static id => id, id => characterNames[id], StringComparer.Ordinal);
        return new(draft.chapterId, Fingerprint(draft, language, localized, project.localization), rows, language, localized, usedNames);
    }
    /// <summary>Checks the exact reviewed dialogue baseline before creating assets or editing bindings.</summary>
    /// <param name="draft">The current shared draft.</param>
    /// <param name="preview">The reviewed match set.</param>
    /// <param name="translations">The current translation draft for a localized recording batch.</param>
    public void ValidateBaseline(
        StoryDraftState draft,
        VoiceBatchPreview preview,
        LocalizationTableAsset? translations = null
    ) {
        if (draft.chapterId != preview.chapter || Fingerprint(draft, preview.language, preview.localized, translations) != preview.baseline)
            throw new AuthoringValidationException($"台词、译文或配音在预览后发生变化，请重新匹配。");
    }
    /// <summary>Rejects a recording review whose linked character names changed before applying it.</summary>
    /// <param name="project">The current explicit character library.</param>
    /// <param name="preview">The reviewed recording script.</param>
    public static void ValidateCharacters(
        NovelProjectAsset project,
        VoiceBatchPreview preview
    ) {
        foreach (var pair in preview.characterNames)
            if (!project.characters.Any(character => character.resourceId == pair.Key && character.displayName == pair.Value))
                throw new AuthoringValidationException($"角色姓名在配音预览后发生变化，请重新匹配：{pair.Key}");
    }
    /// <summary>Applies only reviewed ready matches as one detached native story operation.</summary>
    /// <param name="draft">The transaction candidate.</param>
    /// <param name="preview">The exact reviewed baseline.</param>
    /// <param name="cues">Prepared canonical reusable cues.</param>
    public void Apply(
        StoryDraftState draft,
        VoiceBatchPreview preview,
        IReadOnlyList<AudioCueAsset> cues
    ) {
        if (preview.localized) throw new InvalidOperationException("Localized voice batches must edit their native translation table.");
        ValidateBaseline(draft, preview);
        ValidateCues(preview, cues);
        foreach (var row in preview.rows.Where(static item => item.applicable))
        {
            var node = draft.graph.document.FindNode(new(row.node))!; DialogueNode line = codec.Read<DialogueNode>(node); line.voiceCueId = row.cue; codec.Write(node, line);
        }
    }
    /// <summary>Applies reviewed target-language voices without changing source instructions, translated text or review state.</summary>
    /// <param name="draft">The reviewed chapter baseline.</param>
    /// <param name="table">The detached current translation transaction candidate.</param>
    /// <param name="preview">The exact language review.</param>
    /// <param name="cues">Prepared canonical Voice cues.</param>
    public void ApplyLocalized(
        StoryDraftState draft,
        LocalizationTableAsset table,
        VoiceBatchPreview preview,
        IReadOnlyList<AudioCueAsset> cues
    ) {
        if (!preview.localized) throw new InvalidOperationException("Source voices belong to chapter instructions.");
        ValidateBaseline(draft, preview, table); ValidateCues(preview, cues);
        foreach (var row in preview.rows.Where(static item => item.applicable))
            table.entries.Single(item => item.id == StoryTextKeys.Dialogue(row.textId)).translations.Single(item => item.language == preview.language).voiceCueId = row.cue;
    }
    /// <summary>Exports a quoted UTF-8-compatible recording script or match-back manifest.</summary>
    /// <param name="draft">The source chapter.</param>
    /// <param name="preview">Optional reviewed clip matches.</param>
    /// <param name="separator">Comma or tab.</param>
    /// <param name="translations">The current translation draft for a localized manifest.</param>
    /// <returns>CSV/TSV exchange text, never a second native story representation.</returns>
    public string Export(
        StoryDraftState draft,
        VoiceBatchPreview? preview = null,
        char separator = ',',
        LocalizationTableAsset? translations = null
    ) {
        if (separator is not (',' or '\t')) throw new ArgumentException("Use CSV or TSV.", nameof(separator));
        if (preview is not null) ValidateBaseline(draft, preview, translations);
        var text = new StringBuilder(); Row(["chapter", "node", "textId", "speaker", "text", "voice", "filename", "status", "language"]);
        foreach (var item in Lines(draft))
        {
            var match = preview?.rows.SingleOrDefault(row => row.node == item.node);
            if (match is null && item.line.characterId.Length > 0 && item.line.speaker.Length == 0)
                throw new AuthoringValidationException($"关联角色的配音稿需先 Preview 游戏上下文，再将审核结果传给 Export。");
            Row([draft.chapterId, item.node, item.line.textId, match?.speaker ?? item.line.speaker, match?.text ?? item.line.text, match?.previousCue ?? item.line.voiceCueId,
                match is { clipPath.Length: > 0 } ? match.clipPath : item.line.textId + ".wav", match?.message ?? (item.line.voiceCueId.Length == 0 ? "缺少配音" : "已关联"), preview?.language ?? ""]);
        }
        return text.ToString();
        void Row(IEnumerable<string> cells) => text.AppendJoin(separator, cells.Select(static cell => "\"" + cell.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"")).Append("\r\n");
    }
    private IEnumerable<(string node, DialogueNode line)> Lines(StoryDraftState draft) => ScriptProjectionBuilder.Build(draft)
        .Select(row => draft.graph.document.FindNode(new(row.nodeId))!).Where(static node => node.definitionId == StoryNodeIds.dialogue)
        .Select(node => (node.id.value, codec.Read<DialogueNode>(node)));
    private static void ValidateCues(
        VoiceBatchPreview preview,
        IReadOnlyList<AudioCueAsset> cues
    ) {
        foreach (var row in preview.rows.Where(static item => item.applicable))
            if (!cues.Any(cue => cue.resourceId == row.cue && cue.channel == NovelAudioChannel.Voice && !cue.loop
                && cue.clip?.identity.persistentId == row.clipId && cue.clip.contentVersion == row.clipVersion))
                throw new InvalidOperationException("Reviewed voice has no prepared native cue: " + row.textId);
    }
    private string Fingerprint(
        StoryDraftState draft,
        string language,
        bool localized,
        LocalizationTableAsset? translations
    )
    {
        var text = new StringBuilder(); Add(draft.chapterId); Add(language); Add(localized.ToString()); var view = new VoiceBatchLanguage(translations, language, localized);
        foreach (var item in Lines(draft)) { Add(item.node); Add(item.line.textId); Add(item.line.characterId); Add(item.line.speaker); Add(item.line.text); Add(item.line.voiceCueId); view.Fingerprint(item.line, Add); }
        return NovelLocalizationService.Fingerprint(text.ToString());
        void Add(string value) => text.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);
    }
}
