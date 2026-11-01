using System;
using System.Linq;
using Inno.VisualNovel.Authoring.CharacterEditing;
using Inno.VisualNovel.Authoring.Publishing;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using Inno.VisualNovel.Stage.Composition;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class CharacterVariantContracts
{
    internal static void DefaultsAndReferences(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        StoryEditService edits
    ) {
        var source = new CharacterAsset { resourceId = "guide", poses = [new() { parts = [new() { id = "body", mask = 0,
            transform = new() { pivotX = 9 }, surface = new() { cropWidth = .5 } }] }],
            expressions = [new(), new() { id = "smile", parts = [new() { partId = "body", visibility = 0, offsetX = 7 }] }] };
        string pose = CharacterEditService.DuplicatePose(source, source.defaultPose);
        Check(source.poses[1].parts[0].mask == 0 && source.poses[1].parts[0].transform.pivotX == 9, "Pose duplication lost mask or pivot policy.");
        source.poses[1].parts[0].surface.cropWidth = 1;
        Check(source.poses[0].parts[0].surface.cropWidth == .5, "Duplicated pose shares mutable surfaces.");
        string expression = CharacterVariantEditService.DuplicateExpression(source, "smile");
        source.expressions.Single(item => item.id == expression).parts[0].offsetX = 99;
        Check(source.expressions.Single(item => item.id == "smile").parts[0].offsetX == 7, "Expression duplication shares mutable overrides.");
        Reject(() => CharacterVariantEditService.RemovePose(source, "default"), "Default pose was deleted without an explicit replacement.");
        Check(source.poses.Length == 2 && source.defaultPose == "default", "Rejected pose deletion partially changed the default library.");
        CharacterVariantEditService.RemovePose(source, "default", pose);
        Check(source.poses.Single().id == pose && source.defaultPose == pose, "Explicit pose replacement did not preserve its stable identity.");
        Reject(() => CharacterVariantEditService.RemovePose(source, pose), "The last usable pose was deleted.");
        Reject(() => CharacterVariantEditService.RemoveExpression(source, "neutral", "absent"), "Unknown expression default was accepted.");
        Check(source.expressions.Length == 3 && source.defaultExpression == "neutral", "Rejected expression deletion partially changed the library.");
        CharacterVariantEditService.RemoveExpression(source, "neutral", "smile");
        CharacterVariantEditService.ClearPartOverride(source, "smile", "body");
        Check(source.defaultExpression == "smile" && source.expressions.Single(item => item.id == "smile").parts.Length == 0
            && source.expressions.Single(item => item.id == expression).parts[0].offsetX == 99, "Clearing one expression changed unrelated overrides.");

        var draft = FirstStoryTemplate.Create(codec);
        var instruction = new StageNode { operation = StageOperation.ShowCharacter, resourceId = "guide", target = "lead", pose = "default" };
        var node = edits.Add(draft, "opening", StoryNodeIds.stage, instruction, new(0, 0));
        edits.Insert(draft, draft.graph.document.edges.Single(item => item.output.nodeId.value == "entry").id, node);
        var chapter = draft.ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        var game = new NovelProjectAsset { chapters = [chapter], startChapter = chapter.chapterId, characters = [source],
            font = new Inno.Text.FontAsset(), material = new Inno.Rendering.Assets.MaterialAsset() };
        string error = Failure(() => NovelProjectValidator.Validate(game));
        Check(error.Contains("Character pose/expression", StringComparison.Ordinal) && error.Contains(node.value, StringComparison.Ordinal)
            && error.Contains("guide", StringComparison.Ordinal) && error.Contains("default/", StringComparison.Ordinal), "Deleted pose reference did not identify its story node and owner.");
        instruction.pose = ""; instruction.expression = "neutral"; codec.Write(draft.graph.document.FindNode(node)!, instruction);
        chapter = draft.ToAsset(); chapter.program = compiler.Compile(chapter).program!; game.chapters = [chapter];
        error = Failure(() => NovelProjectValidator.Validate(game));
        Check(error.Contains("Character pose/expression", StringComparison.Ordinal) && error.Contains("neutral", StringComparison.Ordinal), "Deleted expression reference escaped publication diagnostics.");

        var stage = new StageController([]); stage.SetStage(new() { layers = [new() { id = "keep" }] });
        source.expressions = []; source.defaultExpression = "";
        Reject(() => stage.ShowCharacter("lead", source, pose, "missing", new()), "Explicit missing expression silently became unexpressive inheritance.");
        Check(stage.BuildFrame(1920, 1080).items.Single().id == "stage/keep", "Rejected expression changed the active composition.");
        string first = CharacterVariantEditService.AddExpression(source);
        Check(source.defaultExpression == first && source.expressions.Single().parts.Length == 0, "The first new expression did not establish a usable default.");
    }
    private static string Failure(Action action)
    {
        try { action(); } catch (InvalidOperationException error) { return error.Message; }
        throw new InvalidOperationException("Expected a source validation failure.");
    }
    private static void Reject(Action action, string message)
    { bool rejected = false; try { action(); } catch (InvalidOperationException) { rejected = true; } Check(rejected, message); }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
