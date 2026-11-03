using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using InnoEngine.Graphs;
using Inno.VisualNovel.Authoring.AssetImporting;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Audio;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Authoring.Diagnostics;

namespace Inno.VisualNovel.Authoring.StoryEditing;

/// <summary>Reviews external dialogue text and commits an ordinary independent native graph fragment.</summary>
/// <param name="serialization">The generation-owned engine serializer for neutral chapter transactions.</param>
/// <param name="codec">The same native node settings codec used by graph and script edits.</param>
public sealed class StoryImportService(
    IStorySerialization serialization,
    StoryNodeDataCodec codec
)
{
    /// <summary>Gets semantic column names accepted by the review mapping.</summary>
    public static IReadOnlyList<string> columns { get; } = Array.AsReadOnly(new[] { "text", "speaker", "characterId", "voice", "textId", "note", "layout" });
    /// <summary>Reviews quoted CSV or TSV using explicit semantic-to-column mappings.</summary>
    /// <param name="draft">The unchanged destination chapter.</param>
    /// <param name="text">The external dialogue table, including one header.</param>
    /// <param name="separator">Comma or tab.</param>
    /// <param name="mapping">Optional explicit semantic column indexes; minus one means absent.</param>
    /// <param name="characters">The game's explicitly reviewed character library.</param>
    /// <param name="audio">The game's explicitly reviewed audio cue library.</param>
    /// <returns>A frozen matching review; no graph or file is changed.</returns>
    public StoryImportPreview PreviewDelimited(
        StoryDraftState draft,
        string text,
        char separator,
        IReadOnlyDictionary<string, int>? mapping = null,
        CharacterAsset[]? characters = null,
        AudioCueAsset[]? audio = null
    ) {
        var parsed = DelimitedTextTable.Parse(text, separator);
        if (parsed.Count == 0) throw new AuthoringValidationException($"The script table requires a header row.");
        var positions = columns.ToDictionary(name => name, name => mapping is not null && mapping.TryGetValue(name, out int position) ? position
            : Array.FindIndex(parsed[0], header => header.Equals(name, StringComparison.OrdinalIgnoreCase)), StringComparer.Ordinal);
        if (positions["text"] < 0 || positions.Any(item => item.Value < -1 || item.Value >= parsed[0].Length)
            || positions.Values.Where(static value => value >= 0).Distinct().Count() != positions.Values.Count(static value => value >= 0))
            throw new AuthoringValidationException($"Map text to the dialogue column. Other columns are optional, and assigned columns must be distinct.");
        var input = new List<StoryImportRow>();
        for (int index = 1; index < parsed.Count; index++)
        {
            string[] cells = parsed[index]; if (cells.All(string.IsNullOrEmpty)) continue;
            string Cell(string column) => positions[column] >= 0 && positions[column] < cells.Length ? cells[positions[column]] : "";
            string layout = Cell("layout").Trim(); bool valid = layout.Length == 0 || Enum.TryParse<DialoguePresentation>(layout, true, out var _) && Enum.IsDefined(Enum.Parse<DialoguePresentation>(layout, true));
            input.Add(new(index + 1, Cell("speaker").Trim(), Cell("characterId").Trim(), Normalize(Cell("text")), Cell("voice").Trim(), Cell("textId").Trim(), Normalize(Cell("note")),
                valid && layout.Length > 0 ? Enum.Parse<DialoguePresentation>(layout, true) : DialoguePresentation.Adv, valid ? "" : "Layout must be Adv, Nvl, Bubble, Overlay or Letter."));
        }
        return Review(draft, input, characters ?? [], audio ?? []);
    }
    /// <summary>Reviews UTF-8-style paragraphs; a short name before a colon optionally supplies a speaker.</summary>
    /// <param name="draft">The unchanged destination chapter.</param>
    /// <param name="text">Blank-line-separated dialogue paragraphs.</param>
    /// <param name="characters">The game's reviewed character library.</param>
    /// <param name="audio">The game's reviewed voice cue library.</param>
    /// <returns>A frozen review preserving multiline paragraphs and literal narration.</returns>
    public StoryImportPreview PreviewText(
        StoryDraftState draft,
        string text,
        CharacterAsset[]? characters = null,
        AudioCueAsset[]? audio = null
    ) {
        if (text.Length > 16 * 1024 * 1024) throw new AuthoringValidationException($"The script exchange exceeds the 16 MiB preview budget.");
        var rows = new List<StoryImportRow>(); var paragraph = new StringBuilder();
        using var reader = new StringReader(Normalize(text).TrimStart('\uFEFF'));
        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) AddParagraph();
            else { if (paragraph.Length > 0) paragraph.Append('\n'); paragraph.Append(line); }
        }
        AddParagraph();
        return Review(draft, rows, characters ?? [], audio ?? []);
        void AddParagraph()
        {
            string content = paragraph.ToString().Trim(); paragraph.Clear(); if (content.Length == 0) return;
            if (rows.Count >= 10000) throw new AuthoringValidationException($"Import at most 10,000 dialogue lines per batch.");
            int colon = content.IndexOfAny([':', '：']); string speaker = "";
            if (colon == 0) content = content[1..].TrimStart();
            else if (colon is > 0 and <= 64 && !content[..colon].Contains('\n')) { speaker = content[..colon].Trim(); content = content[(colon + 1)..].TrimStart(); }
            rows.Add(new(rows.Count + 1, speaker, "", content, "", "", "", DialoguePresentation.Adv, ""));
        }
    }
    /// <summary>Atomically adds reviewed dialogue as a new public fragment without reconnecting existing control flow.</summary>
    /// <param name="draft">The caller's detached native document candidate.</param>
    /// <param name="preview">The explicit review; any invalid record blocks all changes.</param>
    /// <param name="name">The author-facing fragment name.</param>
    /// <param name="entryId">The new, unique public entry identity.</param>
    /// <param name="characters">The same reviewed character library.</param>
    /// <param name="audio">The same reviewed audio library.</param>
    /// <returns>The new entry node for shared graph/script selection.</returns>
    public string Append(
        StoryDraftState draft,
        StoryImportPreview preview,
        string name,
        string entryId,
        CharacterAsset[]? characters = null,
        AudioCueAsset[]? audio = null
    ) {
        byte[] before = serialization.Serialize(draft);
        if (Digest(before) != preview.baseline || ResourceDigest(characters ?? [], audio ?? []) != preview.resources)
            throw new AuthoringValidationException($"The chapter or character/voice library changed. Preview the import again.");
        if (preview.rows.Count == 0 || preview.rows.Count > 10000 || preview.rows.Any(static row => row.message.Length > 0)) throw new AuthoringValidationException($"Fix every import record first. A batch must contain 1–10,000 dialogue lines.");
        preview = Review(draft, preview.rows, characters ?? [], audio ?? []);
        if (preview.rows.Any(static row => row.message.Length > 0)) throw new AuthoringValidationException($"The import review data is invalid. Read the source and preview it again.");
        if (string.IsNullOrWhiteSpace(name) || name.Length > 256 || string.IsNullOrWhiteSpace(entryId) || entryId.Length > 128 || entryId.Any(char.IsControl)
            || draft.entries.Any(entry => entry.id == entryId)) throw new AuthoringValidationException($"Provide a fragment name and an unused public entry ID.");
        var candidate = serialization.Deserialize<StoryDraftState>(before); var edits = new StoryEditService(codec); var notes = new StoryNotesEditService(serialization);
        string fragment = Guid.NewGuid().ToString("N"); candidate.fragments = [.. candidate.fragments, new() { id = fragment, name = name }];
        GraphNodeId start = edits.Add(candidate, fragment, StoryNodeIds.entry, new(0, 0)), previous = start;
        int index = 0; var sourceRecords = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var row in preview.rows)
        {
            var node = edits.Add(candidate, fragment, StoryNodeIds.dialogue, new DialogueNode { textId = row.textId, characterId = row.character, speaker = row.speaker, text = row.text,
                voiceCueId = row.voice, presentation = row.presentation }, new((index % 5 + 1) * 320, index / 5 * 190)); index++;
            sourceRecords.Add(node.value, row.record);
            edits.Connect(candidate, new(previous, new("next")), node); previous = node;
            if (row.note.Length > 0) notes.Set(candidate, node.value, new() { note = row.note });
        }
        GraphNodeId end = edits.Add(candidate, fragment, StoryNodeIds.end, new(1920, index / 5 * 190)); edits.Connect(candidate, new(previous, new("next")), end);
        candidate.entries = [.. candidate.entries, new() { id = entryId, nodeId = start.value }];
        var compilation = new StoryCompiler(serialization).Compile(candidate.ToAsset());
        if (!compilation.success) throw new AuthoringValidationException($"The import candidate failed compilation: {string.Join("; ", compilation.diagnostics.Take(20).Select(issue =>
            (sourceRecords.TryGetValue(issue.nodeId?.value ?? string.Empty, out int record) ? "Record " + record.ToString(CultureInfo.InvariantCulture) + ": " : "") + issue.message))}");
        draft.graph = candidate.graph; draft.fragments = candidate.fragments; draft.entries = candidate.entries; draft.variables = candidate.variables; return start.value;
    }
    private StoryImportPreview Review(
        StoryDraftState draft,
        IReadOnlyList<StoryImportRow> input,
        CharacterAsset[] characters,
        AudioCueAsset[] audio
    ) {
        if (input.Count is < 1 or > 10000) throw new AuthoringValidationException($"Each import batch requires 1–10,000 dialogue lines.");
        var ids = draft.graph.document.nodes.Where(static node => node.definitionId == StoryNodeIds.dialogue).Select(node => codec.Read<DialogueNode>(node).textId).ToHashSet(StringComparer.Ordinal);
        var rows = new List<StoryImportRow>();
        foreach (var raw in input)
        {
            string message = raw.message, speaker = raw.speaker, character = raw.character, voice = raw.voice;
            string id = raw.textId.Length > 0 ? raw.textId : Guid.NewGuid().ToString("N");
            if (raw.record < 1 || !Enum.IsDefined(raw.presentation) || raw.text.Length == 0 || raw.text.Length > 1000000 || raw.note.Length > 32768 || speaker.Length > 256 || id.Length > 4096) message = "The dialogue is empty, or its record, layout, text, name, note or ID is invalid.";
            var matches = characters.Where(item => character.Length > 0 ? item.resourceId == character : speaker.Length > 0
                && (item.displayName.Equals(speaker, StringComparison.OrdinalIgnoreCase) || item.aliases.Contains(speaker, StringComparer.OrdinalIgnoreCase))).ToArray();
            if (matches.Length > 1 || character.Length > 0 && matches.Length != 1) message = "The character match is missing or ambiguous. Supply an explicit characterId.";
            else if (matches.Length == 1)
            {
                if (matches[0].isMissing) message = "The matched character asset is missing.";
                character = matches[0].resourceId;
                if (speaker.Equals(matches[0].displayName, StringComparison.OrdinalIgnoreCase) || matches[0].aliases.Contains(speaker, StringComparer.OrdinalIgnoreCase)) speaker = "";
            }
            if (voice.Length > 0)
            {
                var cues = audio.Where(item => item.resourceId == voice || item.title == voice).ToArray();
                if (cues.Length != 1 || cues[0].channel != NovelAudioChannel.Voice || cues[0].loop || cues[0].isMissing || cues[0].clip is not { isMissing: false }) message = "Voice must match one non-looping Voice cue with an assigned recording.";
                else voice = cues[0].resourceId;
            }
            if (!ids.Add(id)) message = "The text ID duplicates another record in this chapter or import.";
            rows.Add(raw with { speaker = speaker, character = character, voice = voice, textId = id, message = message });
        }
        return new(Digest(serialization.Serialize(draft)), ResourceDigest(characters, audio), Array.AsReadOnly(rows.ToArray()));
    }
    private static string ResourceDigest(
        CharacterAsset[] characters,
        AudioCueAsset[] audio
    ) {
        var text = new StringBuilder();
        Add("characters"); Add(characters.Length.ToString(CultureInfo.InvariantCulture));
        foreach (var character in characters) { Add(character.resourceId); Add(character.displayName); Add(character.isMissing.ToString()); Add(character.aliases.Length.ToString(CultureInfo.InvariantCulture)); foreach (string alias in character.aliases) Add(alias); }
        Add("audio"); Add(audio.Length.ToString(CultureInfo.InvariantCulture));
        foreach (var cue in audio) { Add(cue.resourceId); Add(cue.title); Add(cue.channel.ToString()); Add(cue.loop.ToString()); Add(cue.isMissing.ToString()); Add((cue.clip?.isMissing != false).ToString()); Add(cue.clip?.identity.persistentId.ToString("D") ?? ""); }
        return Digest(Encoding.UTF8.GetBytes(text.ToString()));
        void Add(string value) => text.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);
    }
    private static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
