using System;
using System.Collections.Generic;
using System.Linq;
using Inno.Core.Graphs;
using Inno.Core.Serialization;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Audio;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Audio;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Player.Audio;
using Inno.VisualNovel.Player.Ports;
using Inno.VisualNovel.Player.Sessions;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class AudioContracts
{
    internal static void CrossfadeAndRestore(SerializationRegistry native)
    {
        var output = new Output();
        using var director = new NovelAudioDirector(Cues(), output);
        director.Apply(new() { operation = AudioOperation.Play, cueId = "one", slot = "bgm" });
        director.Tick(0.5);
        Check(Math.Abs(output.gains.Values.Single() - 0.5) < 1e-9, "Linear fade-in did not reach the midpoint.");
        director.Apply(new() { operation = AudioOperation.Play, cueId = "two", slot = "bgm" });
        director.Tick(0.5);
        Check(output.gains.Count == 2 && output.gains.Values.Order().SequenceEqual(new[] { 0.25, 0.5 }), "Replacement did not crossfade both voices independently.");
        AudioPlaybackSnapshot snapshot = native.Deserialize<AudioPlaybackSnapshot>(native.Serialize(director.Capture()));
        director.Tick(0.5);
        Check(output.gains.Count == 1 && output.gains.Values.Single() == 1, "Completed fade-out retained its native voice.");
        director.Restore(snapshot);
        Check(output.gains.Count == 2 && output.gains.Values.Order().SequenceEqual(new[] { 0.25, 0.5 }), "Native snapshot lost active fades.");
        director.Apply(new() { operation = AudioOperation.Pause, slot = "bgm" });
        double position = director.Capture().voices[0].position;
        director.Tick(1);
        Check(director.Capture().voices[0].position == position && output.paused.Values.All(static value => value), "Paused audio advanced its cursor or fade.");
        director.Apply(new() { operation = AudioOperation.Resume, slot = "bgm" });
        director.Tick(0.5);
        director.Apply(new() { operation = AudioOperation.Play, cueId = "voice", slot = "voice" });
        director.Tick(0.15);
        Check(output.gains.Values.Min() < 0.7, "Speaking did not duck background music.");
        var before = output.gains.ToDictionary();
        snapshot.voices[0].cueId = "missing";
        bool rejected = false;
        try { director.Restore(snapshot); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && output.gains.OrderBy(static pair => pair.Key).SequenceEqual(before.OrderBy(static pair => pair.Key)), "Invalid audio data changed current native voices.");
        director.Dispose();
        Check(output.gains.Count == 0, "Audio owner disposal left voices playing.");
    }

    internal static void NarrativeWait(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        StoryEditService edits
    ) {
        var draft = FirstStoryTemplate.Create(codec);
        GraphNodeId play = edits.Add(draft, "opening", StoryNodeIds.audio, new AudioNode { operation = AudioOperation.Play, slot = "voice", cueId = "voice" }, new(0, 300));
        GraphNodeId wait = edits.Add(draft, "opening", StoryNodeIds.audio, new AudioNode { operation = AudioOperation.Wait, slot = "voice" }, new(0, 350));
        edits.Connect(draft, new(new("entry"), new("next")), play);
        edits.Connect(draft, new(play, new("next")), wait);
        edits.Connect(draft, new(wait, new("next")), new("hello"));
        var chapter = draft.ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        var game = new NovelProjectAsset { chapters = [chapter], startChapter = chapter.chapterId, audio = Cues() };
        var output = new Output();
        using var session = new NovelSession(game, output); session.StartNew();
        Check(session.story!.status == StoryStatus.Presentation && output.gains.Count == 1, "Audio wait did not suspend narrative execution.");
        session.Tick(0.1); session.Tick(0.2);
        Check(output.playCount == 1 && session.story.status == StoryStatus.Presentation, "Waiting restarted the same sound each update.");
        var saved = session.Capture();
        output.gains.Clear();
        session.Tick(0);
        Check(session.story.status == StoryStatus.Dialogue, "Completed sound did not release the narrative wait.");
        session.Restore(saved);
        Check(session.story.status == StoryStatus.Presentation && output.gains.Count == 1, "Audio wait state did not restore.");
        output.failPlay = true;
        StoryMachine before = session.story;
        try { session.Restore(saved); } catch (InvalidOperationException) { }
        Check(ReferenceEquals(before, session.story) && output.gains.Count == 1, "Failed candidate audio preparation replaced the active game.");
        using var missing = new NovelSession(game); missing.StartNew();
        Check(missing.story!.status == StoryStatus.Faulted && missing.story.error.Contains(play.value, StringComparison.Ordinal), "Missing audio capability was silently ignored.");
    }

    internal static void DialogueVoice(
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var draft = FirstStoryTemplate.Create(codec);
        var hello = draft.graph.document.FindNode(new("hello"))!;
        var line = codec.Read<Inno.VisualNovel.Model.Story.Nodes.Reading.DialogueNode>(hello);
        line.voiceCueId = "voice"; codec.Write(hello, line);
        var chapter = draft.ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        var game = new NovelProjectAsset { chapters = [chapter], startChapter = chapter.chapterId, audio = Cues() };
        var output = new Output();
        using var session = new NovelSession(game, output); session.StartNew();
        Check(output.playCount == 1 && session.history[0].voiceCueId == "voice", "Entering a voiced line did not bind its history cue.");
        session.Tick(0); session.Advance();
        Check(output.playCount == 1 && output.gains.Count == 1, "Revealing text restarted or stopped its voice.");
        var saved = session.Capture();
        session.Advance();
        Check(session.story!.status == StoryStatus.Choice && output.gains.Count == 0, "Advance did not apply the stop-voice preference.");
        session.Restore(saved);
        int plays = output.playCount;
        session.Tick(0);
        Check(output.playCount == plays && session.history.Count == 1, "Restoring a voiced line replayed it or duplicated history.");
        session.preferences.stopVoiceOnAdvance = false;
        session.Advance();
        Check(output.gains.Count == 1, "Retain-voice preference was ignored.");
        StoryMachine before = session.story;
        Check(session.ReplayVoice(0) && output.playCount == plays + 1 && output.gains.Count == 1 && ReferenceEquals(before, session.story), "History replay overlapped voices or modified narrative state.");
        Check(!session.ReplayVoice(-1) && !session.ReplayVoice(1), "Invalid history rows were accepted.");
        session.Restore(saved); session.ToggleAutomatic();
        session.Tick(20);
        Check(session.story!.status == StoryStatus.Dialogue, "Auto reading advanced before its voice finished.");
        output.gains.Clear(); session.Tick(0.1);
        Check(session.story.status == StoryStatus.Choice, "Voice completion did not release automatic reading.");
        using var missing = new NovelSession(game); missing.StartNew();
        Check(missing.story!.status == StoryStatus.Faulted && missing.story.error.Contains("hello", StringComparison.Ordinal), "Missing dialogue audio capability lost source diagnostics.");
    }

    internal static void RepeatedBoundary(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        StoryEditService edits
    ) {
        var draft = FirstStoryTemplate.Create(codec);
        var choiceNode = draft.graph.document.nodes.First(node => node.definitionId == StoryNodeIds.choice);
        var choice = codec.Read<Inno.VisualNovel.Model.Story.Nodes.Reading.ChoiceNode>(choiceNode);
        choice.timeout = 0.05; choice.defaultOption = choice.options[0].id; codec.Write(choiceNode, choice);
        edits.Connect(draft, new(new("entry"), new("next")), choiceNode.id);
        edits.Connect(draft, new(choiceNode.id, new(choice.defaultOption)), choiceNode.id);
        var chapter = draft.ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        using var session = new NovelSession(new() { chapters = [chapter], startChapter = chapter.chapterId });
        session.StartNew(); session.Tick(0.1);
        Check(session.story!.status == StoryStatus.Choice && session.canRollback, "A timed loop into the same choice lost its interactive checkpoint.");
        Check(session.Rollback() && !session.canRollback, "Repeated-node rollback retained an abandoned future.");
    }

    internal static void TitleAmbience(
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var chapter = FirstStoryTemplate.Create(codec).ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        var game = new NovelProjectAsset { chapters = [chapter], startChapter = chapter.chapterId, titleAudio = new Inno.Audio.AudioClipAsset() };
        var output = new Output(); using var session = new NovelSession(game, output);
        session.Tick(0.25); session.Tick(0.25);
        Check(output.playCount == 1 && output.gains.Count == 1 && output.gains.Values.Single() > 0, "Title ambience restarted every frame or remained silent after its fade.");
        session.OpenPage("settings"); session.Tick(0.25); session.OpenPage("title"); session.Tick(0.25);
        Check(output.playCount == 1 && output.gains.Count == 1, "Visiting settings restarted or duplicated title ambience.");
        session.StartNew(); Check(output.gains.Count == 0, "Title audio leaked into new story playback.");
        session.OpenPage("title"); session.Tick(0.25);
        Check(output.playCount == 2, "Title audio did not restart after returning from the story.");
    }

    internal static void CharacterPolicies(SerializationRegistry native)
    {
        var output = new Output(); using var director = new NovelAudioDirector(Cues(), output, ["guide", "friend"]);
        var preferences = new Player.Shell.PlayerPreferences(); preferences.SetCharacterVoice(new() { characterId = "guide", volume = 0.25 });
        var savedPreferences = native.Deserialize<Player.Shell.PlayerPreferences>(native.Serialize(preferences));
        director.SetMix(0.8, [1, 1, 1, 0.5, 1]); director.SetCharacterMix(savedPreferences.characterVoices); director.PlayVoice("voice", "guide");
        Check(Math.Abs(output.gains.Values.Single() - 0.1) < 1e-9, "Character, voice-category and master gains were not multiplied independently.");
        var snapshot = native.Deserialize<AudioPlaybackSnapshot>(native.Serialize(director.Capture()));
        Check(snapshot.voices.Single().characterId == "guide", "The voiced character was lost across native snapshot serialization.");
        preferences.SetCharacterVoice(new() { characterId = "guide", enabled = false }); director.SetCharacterMix(preferences.characterVoices); director.Restore(snapshot);
        Check(output.gains.Values.Single() == 0, "Restoring narrative audio reverted the current player's character mute policy.");
        director.PlayVoice("voice", "friend"); Check(Math.Abs(output.gains.Values.Single() - 0.4) < 1e-9, "Muting one character affected a different character.");
        snapshot.voices[0].characterId = "missing";
        try { director.Restore(snapshot); throw new ArgumentException("Undeclared voiced character was accepted."); } catch (InvalidOperationException) { }
        Check(output.gains.Count == 1 && Math.Abs(output.gains.Values.Single() - 0.4) < 1e-9, "Invalid restore replaced the active voice.");
        var copy = preferences.characterVoices; copy[0].enabled = true;
        Check(!preferences.GetCharacterVoice("guide").enabled && savedPreferences.GetCharacterVoice("guide").volume == 0.25, "Character preferences exposed mutable storage or lost native persistence.");
    }

    private static AudioCueAsset[] Cues() => [
        new() { resourceId = "one", channel = NovelAudioChannel.Bgm, volume = 1, fadeIn = 1, fadeOut = 1 },
        new() { resourceId = "two", channel = NovelAudioChannel.Bgm, volume = 1, fadeIn = 1, fadeOut = 1 },
        new() { resourceId = "voice", channel = NovelAudioChannel.Voice, volume = 1 }];

    private sealed class Output : INovelAudioOutput
    {
        private long m_next;
        internal readonly Dictionary<long, double> gains = [];
        internal readonly Dictionary<long, bool> paused = [];
        internal int playCount;
        internal bool failPlay;
        public long Play(string cueId, double gain, bool loop, double position)
        {
            if (failPlay) throw new InvalidOperationException("Injected candidate playback failure.");
            long token = ++m_next; gains.Add(token, gain); paused.Add(token, false); playCount++; return token;
        }
        public void Stop(long token) { gains.Remove(token); paused.Remove(token); }
        public void SetGain(long token, double gain) { if (gains.ContainsKey(token)) gains[token] = gain; }
        public void SetPaused(long token, bool value) { if (paused.ContainsKey(token)) paused[token] = value; }
        public bool IsActive(long token) => gains.ContainsKey(token);
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
