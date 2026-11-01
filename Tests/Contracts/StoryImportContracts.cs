using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Audio;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Narrative.Reading;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class StoryImportContracts
{
    internal static void ReviewedNativeFlow(
        string project,
        IStorySerialization native,
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var draft = FirstStoryTemplate.Create(codec); var service = new StoryImportService(native, codec);
        var actor = new CharacterAsset { resourceId = "guide", displayName = "澄", aliases = ["Cheng"] };
        var voice = new AudioCueAsset { resourceId = "voice", title = "Recording", channel = NovelAudioChannel.Voice, clip = new Inno.Audio.AudioClipAsset() };
        var mapping = new Dictionary<string, int> { ["text"] = 1, ["speaker"] = 0, ["textId"] = 2, ["voice"] = 3, ["note"] = 4, ["layout"] = 5 };
        var preview = service.PreviewDelimited(draft, "\uFEFFPerson,Words,Line,Voice,Comment,Layout\r\nCheng,\"Hi, \"\"world\"\"\r\nnext\",import-a,Recording,review,Letter", ',', mapping, [actor], [voice]);
        var row = preview.rows.Single();
        Check(row.message.Length == 0 && row.character == "guide" && row.speaker == "" && row.voice == "voice" && row.text == "Hi, \"world\"\nnext", "Quoted dialogue matching lost Unicode multiline text, stable character or voice identity.");
        var oldEdges = draft.graph.document.edges.Select(static edge => edge.id.value).ToArray();
        string entry = service.Append(draft, preview, "导入对白", "imported", [actor], [voice]);
        draft = native.Deserialize<StoryDraftState>(native.Serialize(draft)); var compilation = compiler.Compile(draft.ToAsset());
        Check(compilation.success && oldEdges.All(id => draft.graph.document.edges.Any(edge => edge.id.value == id)) && draft.entries.Single(item => item.id == "imported").nodeId == entry,
            "Import reconnected existing control flow or lost its new public entry after native restoration.");
        var lineNode = draft.graph.document.nodes.Single(node => node.definitionId == Model.Story.StoryNodeIds.dialogue && codec.Read<DialogueNode>(node).textId == "import-a");
        Check(new StoryNotesEditService(native).Read(lineNode).note == "review" && codec.Read<DialogueNode>(lineNode).presentation == DialoguePresentation.Letter, "Reviewed layout or native author annotation was discarded.");
        using var story = new StoryMachine(compilation.program!, speakers: new Names()); story.Start("imported");
        Check(story.status == StoryStatus.Dialogue && story.characterId == "guide" && story.speaker == actor.displayName && story.dialogueText == row.text, "Imported dialogue did not run through the authoritative graph: " + story.status + " / " + story.error);
        story.Advance(); story.Advance(); Check(story.status == StoryStatus.Completed, "Imported dialogue did not reach its ordinary end instruction.");
        string path = StoryExchangeFiles.ExportReading(project, draft, codec, Guid.NewGuid(), [actor]);
        string reading = File.ReadAllText(path);
        Check(reading.Contains(row.text, StringComparison.Ordinal) && reading.Contains("voice", StringComparison.Ordinal)
            && reading.Split('\n').Count(static line => line.StartsWith('[')) == draft.graph.document.nodes.Count, "The proofreading export omitted original nodes or expanded control flow recursively.");
    }
    internal static void RejectedCandidates(
        IStorySerialization native,
        StoryNodeDataCodec codec
    ) {
        var draft = FirstStoryTemplate.Create(codec); var service = new StoryImportService(native, codec);
        var actor = new CharacterAsset { resourceId = "guide", displayName = "澄", aliases = ["Cheng"] };
        var preview = service.PreviewText(draft, "Cheng：第一段\n仍是同一台词\n\n第二段旁白", [actor]);
        Check(preview.rows.Count == 2 && preview.rows[0].character == "guide" && preview.rows[1].character.Length == 0, "Plain text paragraphs merged records or lost explicit speaker matching.");
        byte[] before = native.Serialize(draft); actor.displayName = "Renamed"; bool rejected = false;
        try { service.Append(draft, preview, "Imported", "imported", [actor]); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && native.Serialize(draft).SequenceEqual(before), "A stale resource review partially changed the document.");
        string existing = codec.Read<DialogueNode>(draft.graph.document.FindNode(new("hello"))!).textId;
        var duplicate = service.PreviewDelimited(draft, "text,textId,voice\nfirst," + existing + ",\nsecond,new-line,missing", ',');
        Check(duplicate.rows.All(static row => row.message.Length > 0), "An existing text identity or unresolved voice passed import review.");
        rejected = false; try { service.Append(draft, duplicate, "Imported", "imported"); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && native.Serialize(draft).SequenceEqual(before), "Invalid import records changed existing native graph state.");
        var clean = service.PreviewText(draft, "旁白");
        var forged = clean with { rows = [clean.rows[0] with { presentation = (DialoguePresentation)999 }] };
        rejected = false; try { service.Append(draft, forged, "Imported", "imported"); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && native.Serialize(draft).SequenceEqual(before), "A caller-edited review bypassed final validation.");
    }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class Names : StorySpeakerSource
    {
        public override string ResolveName(
            string characterId,
            StoryTextSource? texts
        ) => characterId == "guide" ? "澄" : throw new InvalidOperationException("Unexpected imported character: " + characterId);
    }
}
