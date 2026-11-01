using System;
using System.Collections.Generic;
using System.Linq;
using Inno.Core.Graphs;
using Inno.Core.Serialization;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Audio;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Audio;
using Inno.VisualNovel.Model.Story.Nodes.Flow;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Player.Ports;
using Inno.VisualNovel.Player.Sessions;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class ParallelContracts
{
    internal static void TimingRestoreAndScope(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var edits = new StoryEditService(codec); var draft = FirstStoryTemplate.Create(codec); var groups = new ParallelEditService(edits, codec);
        var fork = groups.Create(draft, "opening", new(250, 250)); var definition = codec.Read<ParallelNode>(draft.graph.document.FindNode(fork)!);
        edits.Connect(draft, new(new("entry"), new("next")), fork); edits.Connect(draft, new(new(definition.joinId), new("next")), new("hello"));
        SetWait(draft, definition.branches[0], 0.25); SetWait(draft, definition.branches[1], 1);
        var chapter = draft.ToAsset(); chapter.program = Compile(chapter);
        using var session = new NovelSession(new() { chapters = [chapter], startChapter = chapter.chapterId }); session.StartNew(); session.Tick(0.4);
        Check(session.story!.status == StoryStatus.Parallel, "Wait-all resumed while a branch remained active.");
        var saved = native.Deserialize<NovelSessionState>(native.Serialize(session.Capture()));
        Check(saved.narrative.tasks.Length == 2 && saved.narrative.tasks.Count(task => task.execution.status == StoryStatus.Completed) == 1, "Parallel checkpoint lost completed versus pending child state.");
        session.Tick(0.7); Check(session.story.status == StoryStatus.Dialogue, "All completed tasks did not resume the main graph.");
        session.Restore(saved); session.Tick(0.5); Check(session.story!.status == StoryStatus.Parallel, "Restored parallel timer restarted or finished early.");
        session.Tick(0.1); Check(session.story.status == StoryStatus.Dialogue, "Restored parallel timers did not preserve remaining time.");
        string branch = groups.AddBranch(draft, fork); Check(compiler.Compile(draft.ToAsset()).success, "Adding a branch broke owned scope semantics.");
        groups.RemoveBranch(draft, fork, branch); Check(codec.Read<ParallelNode>(draft.graph.document.FindNode(fork)!).branches.Length == 2 && compiler.Compile(draft.ToAsset()).success, "Confirmed branch removal left orphan nodes or ports.");
        var members = definition.branches.SelectMany(port => groups.Members(draft, fork, port)).Append(fork.value).Append(definition.joinId).ToArray();
        var clipboard = new StoryClipboardService(new EditorStorySerialization(native), codec);
        string[] copied = clipboard.Paste(draft, "opening", clipboard.Capture(draft, members), new(0, 600));
        var copiedFork = copied.Select(id => draft.graph.document.FindNode(new(id))!).Single(node => node.definitionId == StoryNodeIds.parallel);
        var copiedDefinition = codec.Read<ParallelNode>(copiedFork);
        Check(copiedDefinition.joinId != definition.joinId && codec.Read<JoinNode>(draft.graph.document.FindNode(new(copiedDefinition.joinId))!).forkId == copiedFork.id.value, "Copying a scope retained the original fork/join identities.");
        var branchTarget = draft.graph.document.edges.First(edge => edge.output.nodeId == fork).input.nodeId;
        edits.Connect(draft, new(new("hello"), new("next")), branchTarget);
        Check(!compiler.Compile(draft.ToAsset()).success, "A control-flow edge entered a parallel branch from outside its owner.");

        void SetWait(StoryDraftState state, string branchId, double time)
        { var target = state.graph.document.edges.Single(edge => edge.output.nodeId == fork && edge.output.portId.value == branchId).input.nodeId; edits.SetSettings(state, target, new WaitNode { seconds = time }); }
        Model.Programs.StoryProgram Compile(StoryAsset asset)
        { var result = compiler.Compile(asset); Check(result.success, string.Join("; ", result.diagnostics.Select(static issue => issue.message))); return result.program!; }
    }
    internal static void AnyCancelsOwnedAudio(
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var edits = new StoryEditService(codec); var draft = FirstStoryTemplate.Create(codec);
        var fork = edits.AddParallel(draft, "opening", new(0, 300)); var definition = codec.Read<ParallelNode>(draft.graph.document.FindNode(fork)!); definition.waitAny = true; edits.SetSettings(draft, fork, definition);
        edits.Connect(draft, new(new("entry"), new("next")), fork); edits.Connect(draft, new(new(definition.joinId), new("next")), new("hello"));
        var first = draft.graph.document.edges.Single(edge => edge.output.nodeId == fork && edge.output.portId.value == definition.branches[0]).input.nodeId;
        var second = draft.graph.document.edges.Single(edge => edge.output.nodeId == fork && edge.output.portId.value == definition.branches[1]).input.nodeId;
        edits.SetSettings(draft, first, new WaitNode { seconds = 0.25 }); edits.SetSettings(draft, second, new WaitNode { seconds = 20 });
        var play = edits.Add(draft, "opening", StoryNodeIds.audio, new AudioNode { cueId = "loop", slot = "parallel-sound", operation = AudioOperation.Play }, new(0, 0));
        edits.Connect(draft, new(fork, new(definition.branches[1])), play); edits.Connect(draft, new(play, new("next")), second);
        var chapter = draft.ToAsset(); chapter.program = compiler.Compile(chapter).program ?? throw new InvalidOperationException("Parallel audio story did not compile.");
        var output = new Output(); using var session = new NovelSession(new() { chapters = [chapter], startChapter = chapter.chapterId, audio = [new() { resourceId = "loop", channel = NovelAudioChannel.Ambience, loop = true }] }, output);
        session.StartNew(); Check(output.voices.Count == 1 && session.story!.status == StoryStatus.Parallel, "Parallel presentation did not use the real session audio director.");
        session.Tick(0.3); Check(session.story!.status == StoryStatus.Dialogue && output.voices.Count == 0, "Wait-any did not cancel unfinished task-owned audio before resuming reading.");
        var duplicate = edits.Add(draft, "opening", StoryNodeIds.audio, new AudioNode { cueId = "loop", slot = "parallel-sound", operation = AudioOperation.Play }, new(0, 0));
        edits.Connect(draft, new(fork, new(definition.branches[0])), duplicate); edits.Connect(draft, new(duplicate, new("next")), first);
        Check(!compiler.Compile(draft.ToAsset()).success, "Two branches acquired the same audio replacement slot.");
    }
    private sealed class Output : INovelAudioOutput
    {
        private long m_next;
        internal readonly HashSet<long> voices = [];
        public long Play(string cueId, double gain, bool loop, double position) { long token = ++m_next; voices.Add(token); return token; }
        public void Stop(long token) => voices.Remove(token);
        public void SetGain(long token, double gain) { }
        public void SetPaused(long token, bool paused) { }
        public bool IsActive(long token) => voices.Contains(token);
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
