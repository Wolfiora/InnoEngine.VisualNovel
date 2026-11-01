using System;
using System.Linq;
using System.Collections.Generic;
using Inno.VisualNovel.Model.Localization;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Player.Localization;

namespace Inno.VisualNovel.Authoring.VoiceManagement;

internal sealed class VoiceBatchLanguage(
    LocalizationTableAsset? table,
    string language,
    bool localized,
    IReadOnlyDictionary<string, string>? characterNames = null
)
{
    internal (string speaker, string text, string cue, string issue) Read(DialogueNode line)
    {
        string sourceName = SourceName(line);
        if (!localized) return (sourceName, line.text, line.voiceCueId, "");
        if (table is null || !table.languages.Any(item => item.id == language) || language == table.sourceLanguage)
            return (sourceName, "", "", "目标语言未在翻译表声明。");
        var row = table.entries.SingleOrDefault(item => item.id == StoryTextKeys.Dialogue(line.textId));
        var translation = row?.translations.SingleOrDefault(item => item.language == language);
        if (row is null || row.source != line.text || translation is null || translation.status == TranslationStatus.Draft
            || translation.sourceFingerprint != NovelLocalizationService.Fingerprint(line.text))
            return (sourceName, translation?.text ?? "", translation?.voiceCueId ?? "", "目标译文缺失、待审核或原文已更新，先完成翻译复核。");
        try { NovelLocalizationService.RequireMatchingPlaceholders(line.text, translation.text); }
        catch (InvalidOperationException failure) { return (sourceName, translation.text, translation.voiceCueId, failure.Message); }
        string speaker = sourceName;
        var name = table.entries.SingleOrDefault(item => item.id == NameKey(line));
        var translatedName = name?.translations.SingleOrDefault(item => item.language == language);
        if (name?.source == sourceName && translatedName is { status: not TranslationStatus.Draft }
            && translatedName.sourceFingerprint == NovelLocalizationService.Fingerprint(sourceName)) speaker = translatedName.text;
        return (speaker, translation.text, translation.voiceCueId, "");
    }
    internal void Fingerprint(
        DialogueNode line,
        Action<string> add
    ) {
        if (!localized) return;
        add(table?.sourceLanguage ?? "");
        add((table?.languages.Any(item => item.id == language) ?? false).ToString());
        foreach (string key in new[] { StoryTextKeys.Dialogue(line.textId), NameKey(line) })
        {
            var row = table?.entries.SingleOrDefault(item => item.id == key); add(key); add(row?.source ?? "");
            var translation = row?.translations.SingleOrDefault(item => item.language == language);
            add(translation?.text ?? ""); add(translation?.sourceFingerprint ?? ""); add(translation?.status.ToString() ?? ""); add(translation?.voiceCueId ?? "");
        }
    }
    private string SourceName(DialogueNode line) => line.characterId.Length > 0 && line.speaker.Length == 0
        ? characterNames?.GetValueOrDefault(line.characterId) ?? throw new InvalidOperationException("Linked recording character is missing: " + line.characterId) : line.speaker;
    private static string NameKey(DialogueNode line) => line.characterId.Length > 0 && line.speaker.Length == 0 ? StoryTextKeys.CharacterName(line.characterId) : StoryTextKeys.Speaker(line.textId);
}
