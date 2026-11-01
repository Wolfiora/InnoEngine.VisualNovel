using System;
using System.Linq;
using Inno.Core.Serialization;
using Inno.Text;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Player.Shell;
using Inno.VisualNovel.Authoring.Diagnostics;
using Inno.VisualNovel.Authoring.UiEditing;
using Inno.VisualNovel.Model.Interface;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class PlayerInputContracts
{
    internal static void RequiredScreenBindings(
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var game = Project(codec, compiler);
        var reading = ShellScreenTemplates.Create("reading", new FontAsset(), "Clear");
        game.screens = [new() { role = "reading", screen = reading }];
        PresentationValidator.Validate(game);
        ScreenEditService.Flatten(reading).Single(row => row.element.binding == "input.value").element.binding = "wrong";
        bool rejected = false;
        try { PresentationValidator.Validate(game); } catch (InvalidOperationException error) { rejected = error.Message.Contains("input.value", StringComparison.Ordinal); }
        Check(rejected, "A story input was publishable without its required editable input binding.");
    }
    internal static void NativeDraftAndCancellation(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var game = Project(codec, compiler);
        using var session = new NovelSession(game); session.StartNew();
        StoryMachine story = session.story!;
        Check(story.status == StoryStatus.Input && story.inputText == "默认" && !story.Advance(), "Input did not truncate its initial text by grapheme or block narrative advance.");
        Check(!story.SetInputText("甲乙丙") && story.GetVariable("player").text == "旅人", "Overlong draft changed input state or committed its variable.");
        string name = "A\u0301👨‍👩‍👧‍👦";
        Check(story.SetInputText(name), "A two-grapheme Unicode name was rejected.");
        var saved = native.Deserialize<NovelSessionState>(native.Serialize(session.Capture()));
        Check(session.SubmitInput() && session.story!.status == StoryStatus.Dialogue && !session.story.isTextComplete && session.story.speaker == name, "Input confirmation leaked into dialogue or dynamic speaker interpolation failed.");
        session.Restore(saved);
        Check(session.story!.status == StoryStatus.Input && session.story.inputText == name && session.story.GetVariable("player").text == "旅人", "Native pending input restoration changed its uncommitted variable.");
        Check(session.CancelInput() && session.story.status == StoryStatus.Completed && session.story.GetVariable("player").text == "旅人", "Cancellation committed player text or took the wrong output.");
    }
    internal static void ActualShellConsumption(
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var game = Project(codec, compiler);
        var font = new FontAsset();
        game.screens = new[] { "title", "reading", "pause", "settings", "history", "credits" }.Select(role => new NovelScreenBinding { role = role, screen = ShellScreenTemplates.Create(role, font, "Clear") }).ToArray();
        using var session = new NovelSession(game); session.StartNew();
        var view = new NovelGameView(game, session, static (element, text) => (text.Length * element.style.fontSize, element.style.fontSize));
        view.BuildFrame();
        view.HandleInput(new() { backspace = true }); view.HandleInput(new() { backspace = true });
        view.HandleInput(new() { text = ["新名"], textConfirm = true });
        Check(session.story!.status == StoryStatus.Dialogue && !session.story.isTextComplete && session.story.GetVariable("player").text == "新名", "The real shell did not consume name submission exactly once.");
    }
    private static NovelProjectAsset Project(
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var draft = FirstStoryTemplate.Create(codec);
        var edits = new StoryEditService(codec);
        var input = edits.Add(draft, "opening", StoryNodeIds.input, new InputNode { variableId = "player", defaultText = "默认姓名", maximumLength = 2, allowCancel = true }, new(0, 300));
        edits.Connect(draft, new(new("entry"), new("next")), input);
        edits.Connect(draft, new(input, new("next")), new("hello")); edits.Connect(draft, new(input, new("cancel")), new("end"));
        DialogueNode line = codec.Read<DialogueNode>(draft.graph.document.FindNode(new("hello"))!); line.speaker = "{player}"; codec.Write(draft.graph.document.FindNode(new("hello"))!, line);
        var chapter = draft.ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        return new() { chapters = [chapter], startChapter = chapter.chapterId };
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
