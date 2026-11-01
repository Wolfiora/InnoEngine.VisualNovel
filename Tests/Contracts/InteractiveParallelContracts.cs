using System;
using System.Collections.Generic;
using System.Linq;
using Inno.Core.Graphs;
using Inno.Core.Serialization;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.LocalizationEditing;
using Inno.VisualNovel.Authoring.Preview;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Audio;
using Inno.VisualNovel.Model.Localization;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Flow;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Player.Ports;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Player.Shell;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class InteractiveParallelContracts
{
    internal static void InputNativeRestoreAndRollback(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var fixture = Create(codec); var game = Project(fixture.draft, compiler);
        var table = new LocalizationTableAsset { languages = [new(), new() { id = "en", name = "English" }] };
        LocalizationEditService.Synchronize(table, LocalizationExtractionService.Extract(game));
        LocalizationEditService.SetTranslation(table, StoryTextKeys.Dialogue("parallel-text"), "en", "QRSTUVWX", TranslationStatus.Reviewed);
        game.localization = table;
        using var session = new NovelSession(game); session.preferences.textSpeed = 4; session.StartNew();
        using var view = new NovelGameView(game, session, static (_, text) => (text.Length * 12, 30));
        Check(session.story!.status == StoryStatus.Parallel && session.interactiveStory!.sourceNodeId == fixture.line.value,
            "The bounded group lost its sole interactive owner.");
        session.Tick(.125);
        var revealed = RoundTrip(session, native);
        Check(session.interactiveStory!.visibleGraphemes == 1 && Math.Abs(revealed.narrative.tasks[1].execution.waitElapsed - .125) < 1e-9,
            "Reading speed accelerated a sibling timer or did not reach the child reveal controller.");
        Check(view.BuildFrame().items.Any(static item => item.text == "A"), "The real game page did not bind its child dialogue reveal prefix.");
        session.SetLanguage("en");
        Check(session.interactiveStory!.dialogueText == "QRSTUVWX" && session.history.Single().text == "QRSTUVWX", "Child language switching missed text or current history.");
        session.Restore(revealed);
        Check(session.interactiveStory!.dialogueText == "QRSTUVWX" && session.interactiveStory.visibleGraphemes == 1, "Loading an old-language child snapshot lost the selected language or reveal fraction.");
        session.SetLanguage(table.sourceLanguage);
        long beforeChoice = session.story!.interactionSequence;
        session.Advance(); session.Advance();
        Check(session.interactiveStory.status == StoryStatus.Choice && session.story.interactionSequence == beforeChoice + 1, "Dialogue input skipped a child choice or lost its root interaction sequence.");
        var option = view.BuildFrame().items.First(static item => item.kind == Model.Interface.UiElementKind.Button && item.id.EndsWith("/option", StringComparison.Ordinal));
        float x = option.bounds.x + 10, y = option.bounds.y + 10;
        view.HandleInput(new() { pointerInside = true, pressed = true, x = x, y = y });
        view.HandleInput(new() { pointerInside = true, released = true, confirm = true, x = x, y = y });
        Check(session.interactiveStory.status == StoryStatus.Input && session.story.lastInteractionResult?.sequence == 1,
            "One UI sample both selected an option and submitted the following child input.");
        Check(session.story.SetInputText("A\u0301👨‍👩‍👧‍👦"), "The root did not delegate a grapheme-bounded child input.");
        var input = RoundTrip(session, native);
        Check(session.SubmitInput() && session.story.GetVariable("player").text == "A\u0301👨‍👩‍👧‍👦" && session.story.lastInteractionResult?.sequence == 2,
            "Child input did not commit shared state and propagate its route result.");
        session.Restore(input);
        Check(session.interactiveStory!.status == StoryStatus.Input && session.interactiveStory.inputText == "A\u0301👨‍👩‍👧‍👦" && session.story!.GetVariable("player").text == "旅人",
            "Restoring pending child input committed its draft or lost its active owner.");
        var damaged = native.Deserialize<NovelSessionState>(native.Serialize(input)); damaged.narrative.selectedChoices = [];
        byte[] before = native.Serialize(session.Capture()); Reject(() => session.Restore(damaged));
        Check(native.Serialize(session.Capture()).SequenceEqual(before), "Invalid child choice ownership partially replaced the running candidate.");
        Check(session.CancelInput(), "The root could not cancel its child input."); session.Tick(4);
        Check(session.story!.status == StoryStatus.Dialogue && session.story.GetVariable("player").text == "旅人", "Cancellation wrote player text or failed to join the group.");
        session.Restore(revealed);
        Check(session.interactiveStory!.visibleGraphemes == 1 && session.historyCount == 1, "Native reveal restore lost child text or duplicated history.");
        session.Advance(); session.Advance(); session.Choose("yes");
        Check(session.Rollback() && session.interactiveStory!.status == StoryStatus.Choice && !session.interactiveStory.choices.Single(static item => item.id == "yes").selected,
            "Rollback failed to restore a child decision or retained the discarded choice mark.");
        Check(session.CanRollbackToHistory(0) && session.RollbackToHistory(0) && session.interactiveStory!.status == StoryStatus.Dialogue,
            "History could not locate and restore an interactive boundary inside a parallel snapshot.");
    }

    internal static void NvlAndRecordedRoutes(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var fixture = Create(codec); var edits = new StoryEditService(codec);
        var intro = edits.Add(fixture.draft, "opening", StoryNodeIds.dialogue,
            new DialogueNode { textId = "intro-text", text = "Before", presentation = DialoguePresentation.Nvl }, new(0, -100));
        edits.Connect(fixture.draft, new(new("entry"), new("next")), intro); edits.Connect(fixture.draft, new(intro, new("next")), fixture.fork);
        var line = codec.Read<DialogueNode>(fixture.draft.graph.document.FindNode(fixture.line)!); line.presentation = DialoguePresentation.Nvl;
        edits.SetSettings(fixture.draft, fixture.line, line);
        line = codec.Read<DialogueNode>(fixture.draft.graph.document.FindNode(new("hello"))!); line.presentation = DialoguePresentation.Nvl;
        edits.SetSettings(fixture.draft, new("hello"), line);
        var game = Project(fixture.draft, compiler);
        using var preview = new NovelPreviewSession(game, fixture.draft, compiler, new(), static (_, text) => (text.Length * 10, 30));
        preview.session.Advance(); preview.session.Advance();
        Check(preview.session.interactiveStory!.readingPage.text.Contains("Before", StringComparison.Ordinal), "A child NVL page did not inherit preceding root lines.");
        preview.session.Advance(); preview.session.Advance(); preview.session.Choose("yes"); preview.ObserveInteraction();
        var route = preview.CaptureRoute();
        Check(route.nodeId == fixture.input.value && route.choices.SequenceEqual(new[] { "yes" }) && route.targetInteraction == 4,
            "Route capture used the parallel root instead of the actual child target or lost global visit order.");
        using var replay = new NovelPreviewSession(game, fixture.draft, compiler, route, static (_, _) => (100, 30));
        for (int step = 0; step < 4 && !replay.targetReached; step++) replay.Tick(0);
        Check(replay.targetReached && replay.waitingAtTarget && replay.session.interactiveStory!.sourceNodeId == fixture.input.value,
            "Recorded replay could not traverse and stop at the exact child input.");
        preview.session.SubmitInput(); preview.ObserveInteraction(); preview.session.Tick(4);
        var joined = RoundTrip(preview.session, native);
        Check(joined.narrative.nvlLines.Length == 3 && joined.narrative.nvlLines[0].nodeId.EndsWith(intro.value, StringComparison.Ordinal)
            && joined.narrative.nvlLines[1].nodeId.EndsWith(fixture.line.value, StringComparison.Ordinal), "Completed child NVL lines were not adopted at join.");
        preview.session.Restore(joined);
        Check(preview.session.story!.readingPage.text.Contains("Before", StringComparison.Ordinal), "Joined NVL content did not survive native restore.");
    }

    internal static void FocusConflictsAndCancelledVoice(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var fixture = Create(codec); var edits = new StoryEditService(codec);
        var group = codec.Read<ParallelNode>(fixture.draft.graph.document.FindNode(fixture.fork)!);
        var second = fixture.draft.graph.document.edges.Single(edge => edge.output.nodeId == fixture.fork && edge.output.portId.value == group.branches[1]);
        var competing = edits.Add(fixture.draft, "opening", StoryNodeIds.dialogue, new DialogueNode { textId = "competing", text = "Other" }, new(100, 500));
        edits.Insert(fixture.draft, second.id, competing);
        var rejected = compiler.Compile(fixture.draft.ToAsset());
        Check(!rejected.success && rejected.diagnostics.Any(static issue => issue.code == "VN_PARALLEL_SCOPE" && issue.message.Contains("焦点", StringComparison.Ordinal)),
            "The production compiler accepted two interactive branches.");
        var valid = Create(codec); var game = Project(valid.draft, compiler);
        var forged = Model.Programs.StoryProgramCopy.Copy(game.chapters[0].program!);
        var fork = forged.instructions.Single(item => item.nodeId.EndsWith(valid.fork.value, StringComparison.Ordinal));
        fork.targets.Single(target => target.port == fork.parallel!.branches[1]).nodeId = fork.targets.Single(target => target.port == fork.parallel!.branches[0]).nodeId;
        using (var machine = new StoryMachine(forged))
        { machine.Start(); Check(machine.status == StoryStatus.Faulted && machine.interactionSequence == 0, "Runtime did not reject forged competing input before starting tasks."); }

        fixture = Create(codec); group = codec.Read<ParallelNode>(fixture.draft.graph.document.FindNode(fixture.fork)!); group.waitAny = true;
        edits.SetSettings(fixture.draft, fixture.fork, group);
        var line = codec.Read<DialogueNode>(fixture.draft.graph.document.FindNode(fixture.line)!); line.voiceCueId = "spoken";
        edits.SetSettings(fixture.draft, fixture.line, line); game = Project(fixture.draft, compiler);
        game.audio = [new AudioCueAsset { resourceId = "spoken", channel = NovelAudioChannel.Voice, clip = new Inno.Audio.AudioClipAsset() }];
        var output = new Output(); using var session = new NovelSession(game, output); session.StartNew();
        var saved = RoundTrip(session, native);
        Check(output.active.Count == 1 && saved.narrative.tasks[0].execution.ownedResources.Any(static item => item.kind == "audio" && item.id == "voice"),
            "Dialogue voice was started outside its task resource owner.");
        session.Tick(4);
        Check(session.story!.status == StoryStatus.Dialogue && output.active.Count == 0 && session.CaptureProgress().readContent.Length == 0,
            "Wait-any left cancelled speech playing or marked unrevealed child text as read.");
        session.Restore(saved); session.Tick(4);
        Check(output.active.Count == 0 && session.interactiveStory == session.story, "Restored cancellation retained voice or an obsolete input owner.");
    }

    internal static void SnapshotLanguageAndRestoredVoice(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var fixture = Create(codec); var edits = new StoryEditService(codec);
        var intro = edits.Add(fixture.draft, "opening", StoryNodeIds.dialogue, new DialogueNode { textId = "root-locale", text = "Before" }, new(0, -100));
        edits.Connect(fixture.draft, new(new("entry"), new("next")), intro); edits.Connect(fixture.draft, new(intro, new("next")), fixture.fork);
        var line = codec.Read<DialogueNode>(fixture.draft.graph.document.FindNode(fixture.line)!); line.voiceCueId = "zh-voice";
        edits.SetSettings(fixture.draft, fixture.line, line);
        var second = edits.ContinueDialogue(fixture.draft, fixture.line); line = codec.Read<DialogueNode>(fixture.draft.graph.document.FindNode(second)!);
        line.text = "第二句"; line.voiceCueId = "zh-voice"; edits.SetSettings(fixture.draft, second, line);
        string secondText = line.textId; var game = Project(fixture.draft, compiler);
        game.audio = new[] { "zh-voice", "en-voice" }.Select(static id => new AudioCueAsset { resourceId = id, channel = NovelAudioChannel.Voice, clip = new Inno.Audio.AudioClipAsset() }).ToArray();
        var table = new LocalizationTableAsset { languages = [new(), new() { id = "en", name = "English" }] };
        LocalizationEditService.Synchronize(table, LocalizationExtractionService.Extract(game));
        LocalizationEditService.SetTranslation(table, StoryTextKeys.Dialogue("parallel-text"), "en", "First EN", TranslationStatus.Reviewed, "en-voice");
        LocalizationEditService.SetTranslation(table, StoryTextKeys.Dialogue(secondText), "en", "Second EN", TranslationStatus.Reviewed, "en-voice");
        game.localization = table;
        using var session = new NovelSession(game, new Output(), options: new() { checkpointCodec = new EditorNovelStateCodec(native) });
        session.SetLanguage("en"); session.StartNew(); session.Advance(); session.Advance();
        session.SetLanguage(table.sourceLanguage); session.Advance(); session.Advance();
        var saved = RoundTrip(session, native);
        Check(saved.narrative.textLocale == "en" && saved.presentationLanguage == table.sourceLanguage && saved.audio.voices.Single().cueId == "zh-voice",
            "Snapshot presentation language was inferred from an inactive parent's last dialogue.");
        session.SetLanguage("en"); session.Restore(saved);
        Check(session.interactiveStory!.dialogueText == "Second EN" && session.audio!.Capture().voices.Single().cueId == "en-voice",
            "A child voice restored from another language ignored current player preferences when its parent already matched them.");
        session.Advance(); session.Advance();
        Check(session.Rollback() && session.interactiveStory!.sourceNodeId == second.value && session.audio!.Capture().voices.Single().cueId == "en-voice",
            "Native rollback used parent language metadata instead of the checkpoint's actual presentation language.");
    }

    private static (StoryDraftState draft, GraphNodeId fork, GraphNodeId line, GraphNodeId input) Create(StoryNodeDataCodec codec)
    {
        var draft = FirstStoryTemplate.Create(codec); var edits = new StoryEditService(codec);
        var fork = edits.AddParallel(draft, "opening", new(100, 100)); var group = codec.Read<ParallelNode>(draft.graph.document.FindNode(fork)!);
        edits.Connect(draft, new(new("entry"), new("next")), fork); edits.Connect(draft, new(new(group.joinId), new("next")), new("hello"));
        var branches = group.branches.Select(branch => draft.graph.document.edges.Single(edge => edge.output.nodeId == fork && edge.output.portId.value == branch)).ToArray();
        edits.SetSettings(draft, branches[0].input.nodeId, new WaitNode { seconds = 0 }); edits.SetSettings(draft, branches[1].input.nodeId, new WaitNode { seconds = 4 });
        var line = edits.Add(draft, "opening", StoryNodeIds.dialogue, new DialogueNode { textId = "parallel-text", text = "ABCDEFGH", charactersPerSecond = 2 }, new(200, 0));
        var choice = edits.Add(draft, "opening", StoryNodeIds.choice, new ChoiceNode { prompt = "Pick", options = [new() { id = "yes", text = "Yes" }, new() { id = "no", text = "No" }] }, new(400, 0));
        var input = edits.Add(draft, "opening", StoryNodeIds.input, new InputNode { variableId = "player", defaultText = "名", maximumLength = 2, allowCancel = true }, new(600, 0));
        edits.Connect(draft, new(fork, new(group.branches[0])), line); edits.Connect(draft, new(line, new("next")), choice);
        edits.Connect(draft, new(choice, new("yes")), input); edits.Connect(draft, new(choice, new("no")), input);
        edits.Connect(draft, new(input, new("next")), branches[0].input.nodeId); edits.Connect(draft, new(input, new("cancel")), branches[0].input.nodeId);
        return (draft, fork, line, input);
    }
    private static NovelProjectAsset Project(
        StoryDraftState draft,
        StoryCompiler compiler
    ) {
        var chapter = draft.ToAsset(); var compiled = compiler.Compile(chapter);
        Check(compiled.success, string.Join("; ", compiled.diagnostics.Select(static item => item.message))); chapter.program = compiled.program;
        return new() { chapters = [chapter], startChapter = chapter.chapterId, screens = new[] { "title", "reading", "pause", "settings", "history", "credits" }
            .Select(static role => new NovelScreenBinding { role = role, screen = ShellScreenTemplates.Create(role, null!, "Clear") }).ToArray() };
    }
    private static NovelSessionState RoundTrip(
        NovelSession session,
        SerializationRegistry native
    ) => native.Deserialize<NovelSessionState>(native.Serialize(session.Capture()));
    private sealed class Output : INovelAudioOutput
    {
        internal readonly HashSet<long> active = [];
        private long m_next;
        public long Play(string cueId, double gain, bool loop, double position) { long token = ++m_next; active.Add(token); return token; }
        public void Stop(long token) => active.Remove(token);
        public void SetGain(long token, double gain) { }
        public void SetPaused(long token, bool paused) { }
        public bool IsActive(long token) => active.Contains(token);
    }
    private static void Reject(Action action)
    { try { action(); } catch (ArgumentException) { return; } catch (InvalidOperationException) { return; } throw new InvalidOperationException("An invalid interactive task snapshot was accepted."); }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
