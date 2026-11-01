using System;
using System.Linq;
using Inno.Text;
using Inno.VisualNovel.Authoring.Preview;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Narrative.Execution;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class PreviewTextBridgeContracts
{
    internal static void FullUnicodeValueClearingAndRecordedSubmission(
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var draft = FirstStoryTemplate.Create(codec); var edits = new StoryEditService(codec);
        var input = edits.Add(draft, "opening", StoryNodeIds.input, new InputNode { variableId = "player", defaultText = "old", minimumLength = 1, maximumLength = 5, allowCancel = true }, new(140, 100));
        edits.Connect(draft, new(new("entry"), new("next")), input); edits.Connect(draft, new(input, new("next")), new("hello")); edits.Connect(draft, new(input, new("cancel")), new("end"));
        var chapter = draft.ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        var game = new NovelProjectAsset { chapters = [chapter], startChapter = chapter.chapterId, screens = new[] { "title", "reading", "pause", "settings", "history", "credits" }
            .Select(role => new NovelScreenBinding { role = role, screen = ShellScreenTemplates.Create(role, new FontAsset(), "Clear") }).ToArray() };
        using var preview = new NovelPreviewSession(game, draft, compiler, new() { mode = PreviewRunMode.Complete }, static (_, text) => (text.Length * 12, 28));
        Check(preview.view.TryGetFocusedInputText(out string current) && current == "old", "The preview host could not resolve its actual focused input.");
        preview.HandleInput(new() { selectAll = true, text = ["家👨‍👩‍👧‍👦"] });
        Check(preview.view.TryGetFocusedInputText(out current) && current == "家👨‍👩‍👧‍👦", "The ordinary input bridge split Unicode or retained its previous value.");
        preview.view.ReleaseInput();
        Check(preview.view.TryGetFocusedInputText(out current) && current == "家👨‍👩‍👧‍👦", "Releasing host input destroyed logical text focus or its bound draft.");
        preview.HandleInput(new() { selectAll = true, delete = true }); preview.HandleInput(new() { textConfirm = true });
        Check(preview.view.TryGetFocusedInputText(out current) && current.Length == 0 && preview.session.interactiveStory?.status == StoryStatus.Input,
            "Clearing a native field retained old text or submitted an invalid empty name.");
        preview.HandleInput(new() { selectAll = true, text = ["🌙树"] }); preview.HandleInput(new() { textConfirm = true });
        Check(preview.session.interactiveStory?.status == StoryStatus.Dialogue && !preview.session.interactiveStory.isTextComplete
            && preview.session.interactiveStory.GetVariable("player").text == "🌙树" && !preview.view.TryGetFocusedInputText(out _), "Native input submission leaked into dialogue or retained an inactive field.");
        var route = preview.CaptureRoute();
        Check(route.inputs.Length == 1 && route.inputs[0].text == "🌙树" && !route.inputs[0].cancel, "The normal preview recorder lost or duplicated a bridged input result.");
    }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}
