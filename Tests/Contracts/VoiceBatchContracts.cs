using System;
using System.IO;
using System.Linq;
using Inno.Assets;
using Inno.Assets.Pipeline;
using Inno.Audio;
using Inno.Core.Diagnostics;
using Inno.Core.Identity;
using Inno.Core.Logging;
using Inno.Core.Serialization;
using Inno.Extensibility.Modules;
using Inno.Extensibility.Types;
using Inno.VisualNovel.Authoring.ProjectEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Authoring.VoiceManagement;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Model.Localization;
using Inno.VisualNovel.Authoring.LocalizationEditing;
using Inno.VisualNovel.Player.Localization;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class VoiceBatchContracts
{
    internal static void NativeReviewAndApply(
        string projectRoot,
        ModuleHost modules,
        TypeCatalog types,
        SerializationRegistry serialization,
        IdentityAllocator identities,
        StoryNodeDataCodec codec
    ) {
        string root = Path.Combine(projectRoot, "artifacts", "AcceptanceProjects", "VoiceBatch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Assets", "Recordings"));
        using (var output = new FileStream(Path.Combine(root, "Assets", "Recordings", "zhCN_Tone.wav"), FileMode.CreateNew))
        using (var writer = new BinaryWriter(output))
        {
            writer.Write("RIFF"u8); writer.Write(52); writer.Write("WAVEfmt "u8); writer.Write(16); writer.Write((ushort)1); writer.Write((ushort)1);
            writer.Write(8000); writer.Write(16000); writer.Write((ushort)2); writer.Write((ushort)16); writer.Write("data"u8); writer.Write(16);
            for (int index = 0; index < 8; index++) writer.Write((short)(index * 1000));
        }
        using var logs = new LogRouter();
        using var assets = new AssetPipeline(modules, types, serialization, identities, new DiagnosticHub(), logs,
            AssetPipelineOptions.Create(Path.Combine(root, "Assets"), Path.Combine(root, "Library")) with { enableFileSystemWatcher = false, deferUnavailableExtensions = true });
        using var scope = AssetExecutionContext.EnterScope(assets);
        var clips = NativeVoiceImports.ReadFolder(root, "Recordings"); Check(clips.Count == 1 && !clips[0].isMissing, "Explicit native recording folder failed to import its WAV.");
        var draft = FirstStoryTemplate.Create(codec); var node = draft.graph.document.FindNode(new("hello"))!;
        DialogueNode line = codec.Read<DialogueNode>(node); line.textId = "Tone"; line.text = "对白,\"引号\"\n第二行 {player}"; codec.Write(node, line);
        var game = new NovelProjectAsset(); var service = new VoiceBatchService(codec);
        var review = service.Preview(draft, game, clips, "zhCN_");
        Check(review.rows.Count == 3 && review.rows.Count(static item => item.applicable) == 1 && review.rows.Single(static item => item.applicable).node == "hello",
            "Stable line matching omitted missing recordings or matched unrelated source lines.");
        string csv = service.Export(draft, review);
        using (var reader = new StringReader(csv))
        using (var parser = new Microsoft.VisualBasic.FileIO.TextFieldParser(reader) { HasFieldsEnclosedInQuotes = true, TextFieldType = Microsoft.VisualBasic.FileIO.FieldType.Delimited })
        {
            parser.SetDelimiters(","); Check(parser.ReadFields()!.Length == 9, "Recording manifest lost its explicit schema columns.");
            string[]? fields; bool found = false;
            while ((fields = parser.ReadFields()) is not null)
                if (fields[1] == "hello") { found = true; Check(fields[4] == line.text && fields[2] == "Tone", "Independent CSV parser found corrupted multiline Unicode or quote escaping."); }
            Check(found, "Recording sheet omitted its stable source node.");
        }
        var cues = NativeVoiceImports.PrepareCues(game, review);
        Check(cues.Count == 1 && !cues[0].loop && cues[0].channel == Model.Audio.NovelAudioChannel.Voice && cues[0].clip!.identity.persistentId == clips[0].identity.persistentId,
            "Native batch prepared an incorrect or non-playable Voice cue.");
        byte[] before = serialization.Serialize(draft); bool rejected = false;
        try { service.Apply(draft, review, []); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && serialization.Serialize(draft).AsSpan().SequenceEqual(before), "An incomplete cue set partially changed the graph.");
        var edges = draft.graph.document.edges.ToArray(); service.Apply(draft, review, cues); NovelProjectEditService.Attach(game, cues[0]);
        Check(codec.Read<DialogueNode>(draft.graph.document.FindNode(new("hello"))!).voiceCueId == cues[0].resourceId
            && draft.graph.document.edges.SequenceEqual(edges), "Applying voice matches changed graph identity or execution links.");
        Check(EditorAssets.Save(AssetPath.Project("Voiced.ivnstory"), draft.ToAsset()), "Voiced native story did not compile and import.");
        var imported = Assets.Assets.Load<StoryAsset>(AssetPath.Project("Voiced.ivnstory"));
        Check(imported.program!.instructions.Single(static item => item.nodeId == "hello").dialogue!.voiceCueId == cues[0].resourceId,
            "Native voice binding was not carried into the actual compiled runtime program.");
        Check(NativeVoiceImports.PrepareCues(game, review)[0].identity.persistentId == cues[0].identity.persistentId, "Reusing an existing voice overwrote its imported source or created a duplicate.");
        Check(!service.Preview(draft, game, clips, "zhCN_").rows.Any(static item => item.applicable), "Existing voices were overwritten without explicit replace review.");
        var stale = service.Preview(draft, game, clips, "zhCN_", true);
        line = codec.Read<DialogueNode>(node); line.text = "changed after review"; codec.Write(node, line); rejected = false;
        try { service.ValidateBaseline(draft, stale); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "A recording sheet was applied after its reviewed source dialogue changed.");
        File.Copy(Path.Combine(root, "Assets", "Recordings", "zhCN_Tone.wav"), Path.Combine(root, "Assets", "Recordings", "en_Tone.wav"));
        var table = new LocalizationTableAsset { languages = [new(), new() { id = "en", name = "English" }], entries = [new() { id = StoryTextKeys.Dialogue("Tone"), source = line.text }] };
        LocalizationEditService.SetTranslation(table, StoryTextKeys.Dialogue("Tone"), "en", "English recording, \"voice\"\nnext line", TranslationStatus.Reviewed);
        game.localization = table;
        var translatedReview = service.Preview(draft, game, NativeVoiceImports.ReadFolder(root, "Recordings"), "en_", language: "en");
        Check(translatedReview.localized && translatedReview.language == "en" && translatedReview.rows.Single(static item => item.node == "hello").applicable
            && translatedReview.rows.Single(static item => item.node == "hello").text.StartsWith("English recording", StringComparison.Ordinal), "Target recording sheets used source dialogue or preserved a source-language voice instead of checking target bindings.");
        string translatedCsv = service.Export(draft, translatedReview, ',', table);
        Check(translatedCsv.Contains("\"English recording, \"\"voice\"\"", StringComparison.Ordinal) && translatedCsv.Contains("\"en\"", StringComparison.Ordinal),
            "Localized recording export omitted the reviewed target text or language.");
        var targetCues = NativeVoiceImports.PrepareCues(game, translatedReview);
        byte[] sourceBefore = serialization.Serialize(draft); string targetText = table.entries[0].translations[0].text;
        service.ApplyLocalized(draft, table, translatedReview, targetCues); NovelProjectEditService.Attach(game, targetCues[0]);
        Check(serialization.Serialize(draft).AsSpan().SequenceEqual(sourceBefore) && targetCues[0].resourceId != cues[0].resourceId
            && table.entries[0].translations[0].text == targetText && table.entries[0].translations[0].status == TranslationStatus.Reviewed,
            "Localized voice binding changed source instructions, target text or translation review state.");
        Check(EditorAssets.Save(AssetPath.Project("Voices.ivnlocale"), table), "Native localized voice bindings did not import.");
        var importedTable = Assets.Assets.Load<LocalizationTableAsset>(AssetPath.Project("Voices.ivnlocale"));
        Check(new NovelLocalizationService(importedTable, "en").ResolveVoice(StoryTextKeys.Dialogue("Tone"), line.text, cues[0].resourceId) == targetCues[0].resourceId,
            "Actual runtime localization did not resolve the native target-language voice.");
        var translatedStale = service.Preview(draft, game, NativeVoiceImports.ReadFolder(root, "Recordings"), "en_", true, "en");
        table.entries[0].translations[0].text = "edited after review"; rejected = false;
        try { service.ValidateBaseline(draft, translatedStale, table); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "A target-language batch ignored changes to the reviewed translation.");
        table.entries[0].translations[0].status = TranslationStatus.Draft;
        Check(!service.Preview(draft, game, NativeVoiceImports.ReadFolder(root, "Recordings"), "en_", true, "en").rows.Any(static item => item.applicable),
            "Unreviewed target text accepted automatic voice bindings.");
        Directory.CreateDirectory(Path.Combine(root, "Assets", "Recordings", "Duplicate"));
        File.Copy(Path.Combine(root, "Assets", "Recordings", "zhCN_Tone.wav"), Path.Combine(root, "Assets", "Recordings", "Duplicate", "zhCN_Tone.wav"));
        var ambiguous = service.Preview(draft, new(), NativeVoiceImports.ReadFolder(root, "Recordings"), "zhCN_", true);
        Check(!ambiguous.rows.Single(static item => item.node == "hello").applicable
            && ambiguous.rows.Single(static item => item.node == "hello").message.Contains("不止一个", StringComparison.Ordinal), "Ambiguous filenames silently selected the first recording.");
        rejected = false; try { NativeVoiceImports.ReadFolder(root, "../Outside"); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Recording lookup escaped the explicit project asset folder.");
        string export = NativeVoiceImports.Export(root, csv, ','); Check(export.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.ReadAllText(export) == csv, "Recording export left the test project or changed the exchange text.");
        Console.WriteLine("Native voice batch acceptance project: " + root);
    }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}
