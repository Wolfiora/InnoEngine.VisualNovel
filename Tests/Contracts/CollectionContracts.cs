using System;
using System.Collections.Generic;
using System.Linq;
using Inno.Core.Serialization;
using Inno.Text;
using Inno.VisualNovel.Authoring.AssetImporting;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Audio;
using Inno.VisualNovel.Model.Collections;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Flow;
using Inno.VisualNovel.Model.Story.Nodes.Audio;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Player.Collections;
using Inno.VisualNovel.Player.Ports;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Player.Shell;
using Inno.VisualNovel.UI.Input;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class CollectionContracts
{
    internal static void ProgressAndReplay(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var draft = FirstStoryTemplate.Create(codec); var edits = new StoryEditService(codec);
        draft.variables = [.. draft.variables, new() { id = "global", scope = VariableScope.Global, initialValue = NovelValue.Integer(0) }];
        var unlock = edits.Add(draft, "opening", StoryNodeIds.unlock, new UnlockNode { entryId = "ending" }, new(0, 0));
        edits.Connect(draft, new(new("hello"), new("next")), unlock); edits.Connect(draft, new(unlock, new("next")), new("choice"));
        var chapter = draft.ToAsset(); chapter.program = compiler.Compile(chapter).program ?? throw new InvalidOperationException("Unlock node failed compilation.");
        var game = new NovelProjectAsset { chapters = [chapter], startChapter = chapter.chapterId, collections = new() { entries = [
            new() { id = "ending", kind = CollectionKind.Ending, title = "秘密结局" },
            new() { id = "auto", kind = CollectionKind.Glossary, unlockCondition = new() { operation = ExpressionOperation.Equal, operands = [new() { operation = ExpressionOperation.Variable, variableId = "player" }, new() { literal = NovelValue.String("明") }] } },
            new() { id = "replay", kind = CollectionKind.Replay, initiallyUnlocked = true, chapterId = chapter.chapterId, initialValues = [new() { id = "player", value = NovelValue.String("回想玩家") }] } ] } };
        CollectionProjectValidator.Validate(game);
        using var main = new NovelSession(game); main.StartNew();
        var initial = native.Deserialize<NovelSessionState>(native.Serialize(main.Capture()));
        main.Advance(); main.Advance();
        Check(main.story!.status == StoryStatus.Choice && main.collections.IsUnlocked("ending"), "Story unlock was not committed at its presentation boundary.");
        Check(main.Rollback() && main.collections.IsUnlocked("ending"), "Rollback erased monotonic unlock progress.");
        main.story.SetVariable("player", NovelValue.String("明")); main.Tick(0);
        Check(main.collections.IsUnlocked("auto"), "Pure typed auto-unlock did not observe current game variables.");
        main.Restore(initial); Check(main.collections.IsUnlocked("auto") && main.collections.IsUnlocked("ending"), "Ordinary loading replaced global unlocks.");
        var progress = native.Deserialize<Player.Persistence.GlobalProgressState>(native.Serialize(main.CaptureProgress()));
        using var fresh = new NovelSession(game); fresh.RestoreProgress(progress);
        Check(fresh.collections.IsUnlocked("ending") && fresh.collections.IsUnlocked("auto"), "Native global progress lost unlock identities.");
        var invalid = fresh.CaptureProgress(); invalid.unlocked = ["missing"]; invalid.variables[0].value = NovelValue.Integer(99);
        bool rejected = false; try { fresh.RestoreProgress(invalid); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && fresh.CaptureProgress().variables[0].value.integer == 0, "Invalid collection progress partially committed global variables.");
        string callerNode = main.story!.nodeId; double callerTime = main.playTime;
        using (var replay = new ReplaySession(game, main, "replay"))
        {
            Check(replay.session.isReplay && replay.session.story!.dialogueText.Contains("回想玩家", StringComparison.Ordinal), "Replay did not apply explicit initial values before its first instruction.");
            replay.session.story!.SetVariable("global", NovelValue.Integer(77)); replay.session.collections.Unlock("ending"); replay.session.Tick(2);
            bool noSave = false; try { replay.session.Capture(); } catch (InvalidOperationException) { noSave = true; }
            Check(noSave, "Replay allowed an ordinary game save."); replay.Return();
        }
        Check(main.story.nodeId == callerNode && main.playTime == callerTime && main.story.GetVariable("global").integer == 0, "Replay advanced or mutated its caller.");
        game.collections.entries.Single(entry => entry.id == "ending").title = "MUTATED";
        Check(main.collections.Get("ending").title == "秘密结局", "Running collections retained editable content aliases.");
    }
    internal static void GalleryAndMusic(
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var chapter = FirstStoryTemplate.Create(codec).ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        var game = new NovelProjectAsset { chapters = [chapter], startChapter = chapter.chapterId, audio = [new() { resourceId = "one", channel = NovelAudioChannel.Bgm, loop = true }, new() { resourceId = "two", channel = NovelAudioChannel.Bgm }],
            collections = new() { entries = [new() { id = "one", kind = CollectionKind.Music, initiallyUnlocked = true, musicCueId = "one", title = "曲目一" },
                new() { id = "two", kind = CollectionKind.Music, initiallyUnlocked = true, musicCueId = "two", title = "曲目二" },
                new() { id = "secret", kind = CollectionKind.Glossary, title = "未公开剧透", description = "秘密正文" },
                new() { id = "article", kind = CollectionKind.Glossary, title = "世界", description = "世界正文", initiallyUnlocked = true },
                new() { id = "replay", kind = CollectionKind.Replay, title = "序章回想", initiallyUnlocked = true, chapterId = chapter.chapterId }] },
            screens = new[] { "title", "reading", "pause", "settings", "history", "credits", "save", "load", "confirm", "extras", "gallery", "music", "replays", "endings", "glossary", "characters", "collectionDetail" }
                .Select(role => new NovelScreenBinding { role = role, screen = ShellScreenTemplates.Create(role, new FontAsset(), "Clear") }).ToArray() };
        var output = new Output(); using var session = new NovelSession(game, output);
        game.localization = StarterLocalizationTemplate.Create(game);
        using (var translated = new NovelSession(game))
        {
            translated.SetLanguage("en");
            using var translatedView = new NovelGameView(game, translated, static (_, text) => (text.Length * 10, 30));
            Check(translatedView.BuildFrame().items.Any(item => item.text == "New game"), "Starter content did not instantiate editable English translations.");
        }
        session.audio!.Apply(new() { slot = "bgm", cueId = "one", operation = AudioOperation.Play }); session.audio.Tick(3);
        var before = session.audio.Capture(); var music = new MusicGalleryController(session);
        output.fail = true; bool failed = false; try { music.Play("two"); } catch (InvalidOperationException) { failed = true; } output.fail = false;
        Check(failed && session.audio.Capture().voices.Single().slot == "bgm", "Failed gallery preparation destroyed caller audio.");
        music.Play("one"); Check(!output.voices.Values.Single().loop, "A looping BGM prevented playlist advancement.");
        music.TogglePause(); music.Tick(); Check(music.selected == "one" && music.paused, "Paused music advanced its playlist."); music.TogglePause();
        output.voices.Clear(); music.Tick(); Check(music.selected == "two", "Natural completion did not advance to the next unlocked track.");
        music.repeat = true; output.voices.Clear(); music.Tick(); Check(music.selected == "two", "Single-track repeat advanced the playlist.");
        music.Leave(); var restored = session.audio.Capture().voices.Single();
        Check(restored.cueId == before.voices[0].cueId && restored.position == before.voices[0].position && restored.loop, "Music room did not restore caller position and looping policy.");
        using var view = new NovelGameView(game, session, static (_, text) => (text.Length * 10, 30), audioFactory: static () => new Output());
        Click("extras"); Click("glossary");
        Check(view.BuildFrame().items.Any(item => item.text == "未解锁") && !view.BuildFrame().items.Any(item => item.text.Contains("未公开剧透", StringComparison.Ordinal)), "Locked collection metadata leaked into the game UI.");
        ClickText("世界"); Check(session.page == "collectionDetail" && view.BuildFrame().items.Any(item => item.text == "世界正文"), "Editable item and detail templates did not share selection data.");
        view.HandleInput(new() { cancel = true }); view.HandleInput(new() { cancel = true }); Click("replays"); ClickText("序章回想");
        Check(view.activeSession.isReplay && view.activeSession != session && session.page == "replays", "Real game-view replay replaced the caller session.");
        Click("replay-exit"); Check(view.activeSession == session && session.page == "replays", "Replay exit did not restore the caller's page.");

        void Click(string id)
        {
            var item = view.BuildFrame().items.Single(item => item.id == id); var x = item.bounds.x + item.bounds.width / 2; var y = item.bounds.y + item.bounds.height / 2;
            view.HandleInput(new() { x = x, y = y, pointerInside = true, pressed = true }); view.HandleInput(new() { x = x, y = y, pointerInside = true, released = true });
        }
        void ClickText(string text)
        {
            var item = view.BuildFrame().items.Single(item => item.text == text); Click(item.id);
        }
    }
    private sealed class Output : INovelAudioOutput
    {
        private long m_next;
        internal bool fail;
        internal readonly Dictionary<long, (string cue, bool loop)> voices = [];
        public long Play(string cueId, double gain, bool loop, double position)
        { if (fail) throw new InvalidOperationException("Injected audio preparation failure."); long id = ++m_next; voices.Add(id, (cueId, loop)); return id; }
        public void Stop(long token) => voices.Remove(token);
        public void SetGain(long token, double gain) { }
        public void SetPaused(long token, bool paused) { }
        public bool IsActive(long token) => voices.ContainsKey(token);
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
