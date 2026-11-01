using System;
using System.Linq;
using Inno.Core.Graphs;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.LocalizationEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Editor.Localization;
using Inno.VisualNovel.Model.Localization;
using Inno.VisualNovel.Model.Story;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class CompilerDiagnosticContracts
{
    internal static void NativeIdentityAndOwnerFormatting(
        IStorySerialization native,
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var table = new LocalizationTableAsset { sourceLanguage = "zh-CN", languages = [new() { id = "zh-CN", name = "简体中文" }, new() { id = "en", name = "English" }] };
        Add("节点实现不可用：{0}。原始数据已保留。", "Node unavailable: {0}. Original data is preserved.");
        Add("该节点无法从任何入口到达。", "This node cannot be reached from any entry.");
        var english = new EditorTextCatalog(table, "en"); var chinese = new EditorTextCatalog(null);
        string custom = "custom/标题{0}##原始名称";
        var draft = FirstStoryTemplate.Create(codec);
        draft.graph.document.AddNode(new(new("missing"), custom));
        draft.fragments[0].nodes = [.. draft.fragments[0].nodes, "missing"];
        byte[] before = native.Serialize(draft);
        var result = compiler.Compile(draft.ToAsset());
        GraphDiagnostic issue = result.diagnostics.Single(static item => item.code == "VN_NODE_MISSING");
        Check(!result.success && !result.validation.isValid && issue.nodeId?.value == "missing" && issue.edgeId is null
            && result.FormatDiagnostic(issue, message => english.Format(message.Format, message.GetArguments())) == "Node unavailable: " + custom + ". Original data is preserved."
            && result.FormatDiagnostic(issue, message => chinese.Format(message.Format, message.GetArguments())) == issue.message
            && native.Serialize(draft).SequenceEqual(before), "Native diagnostics lost identity, literal user arguments or source isolation.");
        _ = result.FormatDiagnostic(issue, message => { message.GetArguments()[0] = "Changed"; return "Ignored"; });
        Check(result.FormatDiagnostic(issue, message => english.Format(message.Format, message.GetArguments())).Contains(custom, StringComparison.Ordinal),
            "A presentation callback changed the stored template arguments.");

        draft = FirstStoryTemplate.Create(codec);
        draft.graph.document.AddEdge(new(new("broken-edge"), new(new("hello"), new("inactive-port")), new(new("choice"), new("in"))));
        var invalid = compiler.Compile(draft.ToAsset());
        GraphDiagnostic edge = invalid.diagnostics.Single(static item => item.code == "GRAPH_MISSING_ENDPOINT_PORT");
        Check(edge.edgeId?.value == "broken-edge" && edge.nodeId is null
            && invalid.FormatDiagnostic(edge, _ => throw new InvalidOperationException("Native message must remain literal.")) == edge.message,
            "The compiler flattened native edge identity or guessed localization metadata for a host message.");
        bool foreign = false;
        try { _ = invalid.FormatDiagnostic(issue, static message => message.ToString()); } catch (ArgumentException) { foreign = true; }
        Check(foreign, "A diagnostic from another compilation was accepted by the presentation owner.");

        draft = FirstStoryTemplate.Create(codec); draft.graph.document.AddNode(new(new("orphan"), StoryNodeIds.end));
        draft.fragments[0].nodes = [.. draft.fragments[0].nodes, "orphan"];
        var warning = compiler.Compile(draft.ToAsset()); string fingerprint = warning.program!.fingerprint;
        var unreachable = warning.diagnostics.Single(static item => item.code == "VN_UNREACHABLE");
        Check(warning.success && warning.validation.isValid && warning.FormatDiagnostic(unreachable, message => english.Format(message.Format, message.GetArguments())) == "This node cannot be reached from any entry."
            && warning.program.fingerprint == fingerprint, "Diagnostic language changed program content or treated a warning as a compile failure.");

        void Add(
            string source,
            string translated
        ) {
            string id = EditorTextCatalog.Key(source, true); table.entries = [.. table.entries, new() { id = id, source = source }];
            LocalizationEditService.SetTranslation(table, id, "en", translated, TranslationStatus.Reviewed);
        }
    }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}
