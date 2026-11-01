using System;
using System.Collections.Generic;
using System.Linq;
using Inno.Core.Logging;
using Inno.Core.Serialization;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Debugging;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Narrative.Execution;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class StoryLogContracts
{
    internal static void EngineRoutingRestoreAndPreviewPolicy(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        using var logs = new LogRouter(); using var scope = logs.EnterScope(); var sink = new Sink(); logs.RegisterSink(sink);
        var draft = FirstStoryTemplate.Create(codec); var edits = new StoryEditService(codec);
        var logId = edits.Add(draft, "opening", StoryNodeIds.log, new LogNode { message = "观察 {player} / {{player}}", level = NovelLogLevel.Warning }, new(0, 0));
        edits.Insert(draft, draft.graph.document.edges.Single(edge => edge.output.nodeId.value == "entry").id, logId);
        var program = compiler.Compile(draft.ToAsset()).program ?? throw new InvalidOperationException("A valid log graph did not compile.");
        program = native.Deserialize<Model.Programs.StoryProgram>(native.Serialize(program));
        using var player = new StoryMachine(program); player.Start(); logs.Flush();
        Check(player.status == StoryStatus.Dialogue && sink.entries.Count == 0, "A preview-only diagnostic was emitted by published play or changed its path.");
        using var preview = new StoryMachine(program, preview: true);
        program.instructions.Single(item => item.nodeId == logId.value).log!.message = "mutated outside session";
        preview.Start(); logs.Flush();
        Check(preview.status == StoryStatus.Dialogue && sink.entries.Count == 1 && sink.entries[0].level == LogLevel.Warn
            && sink.entries[0].message.Contains(draft.chapterId + "/" + logId.value, StringComparison.Ordinal)
            && sink.entries[0].message.EndsWith("观察 旅人 / {player}", StringComparison.Ordinal), "Engine log routing failed: " + preview.status + "; " + preview.error + "; "
                + string.Join(" | ", sink.entries.Select(static item => item.level + ": " + item.message)));
        var snapshot = native.Deserialize<StoryExecutionState>(native.Serialize(preview.Capture()));
        using var restored = new StoryMachine(program, preview: true); restored.Restore(snapshot); restored.Tick(.1); logs.Flush();
        Check(sink.entries.Count == 1, "Restoring a later narrative boundary replayed a prior log side effect.");
        var declaration = codec.Read<LogNode>(draft.graph.document.FindNode(logId)!); declaration.previewOnly = false; declaration.level = NovelLogLevel.Error; codec.Write(draft.graph.document.FindNode(logId)!, declaration);
        using var released = new StoryMachine(compiler.Compile(draft.ToAsset()).program!); released.Start(); logs.Flush();
        Check(released.status == StoryStatus.Dialogue && sink.entries.Count == 2 && sink.entries[1].level == LogLevel.Error, "An enabled player error log failed to route or incorrectly stopped execution.");
    }
    internal static void AuthoringReferencesAndDiagnostics(
        IStorySerialization native,
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var draft = FirstStoryTemplate.Create(codec); var edits = new StoryEditService(codec);
        var id = edits.Add(draft, "opening", StoryNodeIds.log, new LogNode { message = "检查 {player}，字面 {{player}}" }, new(0, 0));
        var references = new VariableEditService(codec);
        Check(references.FindUsages(draft, "player").Contains(id.value), "Log interpolation was not treated as a real variable reference.");
        var search = new StorySearchService(codec, new(native)); Check(search.Find(draft, "检查").Any(item => item.nodeId == id.value), "Log messages could not be found in the shared story search.");
        var clipboard = new StoryClipboardService(native, codec); byte[] bytes = clipboard.Capture(draft, [id.value]);
        var target = FirstStoryTemplate.Create(codec); target.variables = [new() { id = "destination", initialValue = NovelValue.String("Target") }];
        string pasted = clipboard.Paste(target, "opening", bytes, new(0, 0), variableTargets: new Dictionary<string, string> { ["player"] = "destination" }).Single();
        Check(codec.Read<LogNode>(target.graph.document.FindNode(new(pasted))!).message == "检查 {destination}，字面 {{player}}", "Cross-chapter copy did not remap log interpolation through the standard typed variable review.");
        var node = draft.graph.document.FindNode(id)!;
        codec.Write(node, new LogNode { message = "{unknown}" }); var result = compiler.Compile(draft.ToAsset());
        Check(!result.success && result.diagnostics.Any(issue => issue.code == "VN_TEXT_VARIABLE_MISSING" && issue.nodeId?.value == id.value), "An unknown log variable lacked a source-qualified compiler error.");
        codec.Write(node, new LogNode { level = (NovelLogLevel)999 }); result = compiler.Compile(draft.ToAsset());
        Check(!result.success && result.diagnostics.Any(issue => issue.code == "VN_LOG_INVALID"), "An unsupported log severity escaped compilation.");
    }
    private sealed class Sink : ILogSink
    {
        internal readonly List<LogEntry> entries = [];
        public void Receive(LogEntry entry) { if (entry.message.StartsWith("[VN ", StringComparison.Ordinal)) entries.Add(entry); }
    }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}
