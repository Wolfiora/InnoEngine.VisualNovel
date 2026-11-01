using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal static class EditorTextInventory
{
    private sealed record Occurrence(string text, string kind, string path, int line, string context);
    internal sealed record Entry(string id, string kind, string source);
    internal static IReadOnlyList<Entry> Write(string root)
    {
        var rows = new List<Occurrence>();
        var sourceFiles = Directory.EnumerateFiles(Path.Combine(root, "Assets", "Code", "Editor"), "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(root, "Assets", "Code", "Authoring"), "*.cs", SearchOption.AllDirectories))
            .Append(Path.Combine(root, "Assets", "Code", "Compiler", "StoryNodeCatalog.cs"))
            .Append(Path.Combine(root, "Assets", "Code", "Compiler", "StoryCompiler.cs"))
            .Append(Path.Combine(root, "Assets", "Code", "Compiler", "Analysis", "ParallelScopeAnalyzer.cs"))
            .Append(Path.Combine(root, "Assets", "Code", "Authoring", "BuiltInResources", "BuiltInResourceCatalog.cs"))
            .Append(Path.Combine(root, "Assets", "Code", "Authoring", "UiEditing", "BuiltInUiChoices.cs"))
            .Append(Path.Combine(root, "Assets", "Code", "Authoring", "Templates", "TutorialSampleCatalog.cs"))
            .Append(Path.Combine(root, "Assets", "Code", "Authoring", "Templates", "UiWidgetTemplates.cs"));
        foreach (string path in sourceFiles.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal))
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path), new CSharpParseOptions(LanguageVersion.Preview));
            var errors = tree.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
            if (errors.Length > 0) throw new InvalidOperationException("Cannot inventory invalid C# syntax: " + path + " / " + errors[0]);
            foreach (var node in tree.GetRoot().DescendantNodes())
            {
                string file = Path.GetFileName(path);
                if (file is "StoryCompiler.cs" or "ParallelScopeAnalyzer.cs"
                    && (node is not InterpolatedStringExpressionSyntax || !CompilerTemplate(node))) continue;
                bool diagnosticOnly = path.StartsWith(Path.Combine(root, "Assets", "Code", "Authoring") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    && file is not ("BuiltInResourceCatalog.cs" or "BuiltInUiChoices.cs" or "TutorialSampleCatalog.cs" or "UiWidgetTemplates.cs");
                if (diagnosticOnly && file != "LocalizationDelimitedText.cs" && (node is not InterpolatedStringExpressionSyntax || !(DiagnosticTemplate(node)
                    || file == "PreviewProjectPreparation.editor.cs" && PreviewProgressTemplate(node)))) continue;
                if (file is "TutorialSampleCatalog.cs" or "UiWidgetTemplates.cs"
                    && !node.AncestorsAndSelf().OfType<PropertyDeclarationSyntax>().Any(static property => property.Identifier.ValueText == "entries")) continue;
                if (node is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
                {
                    var owner = literal.Ancestors().OfType<InvocationExpressionSyntax>().FirstOrDefault();
                    bool composite = owner is not null && (owner.Expression.ToString() == "Format" || owner.Expression.ToString().EndsWith(".Format", StringComparison.Ordinal))
                        && owner.ArgumentList.Arguments.FirstOrDefault()?.Expression == literal;
                    string value = literal.Token.ValueText;
                    if (file == "BuiltInResourceCatalog.cs" && value.Contains('|', StringComparison.Ordinal)) value = value[(value.IndexOf('|') + 1)..];
                    Add(literal, value, composite ? "format" : "literal");
                }
                else if (node is InterpolatedStringExpressionSyntax interpolation)
                {
                    int index = 0; var format = new StringBuilder();
                    foreach (var part in interpolation.Contents)
                        if (part is InterpolatedStringTextSyntax text) format.Append(text.TextToken.ValueText);
                        else if (part is InterpolationSyntax slot)
                        {
                            format.Append('{').Append(index++);
                            if (slot.AlignmentClause is { } alignment) format.Append(',').Append(alignment.Value.ToString());
                            if (slot.FormatClause is { } specification) format.Append(':').Append(specification.FormatStringToken.ValueText);
                            format.Append('}');
                        }
                    Add(interpolation, format.ToString(), "format");
                }
            }
            void Add(
                SyntaxNode node,
                string text,
                string kind
            ) {
                if (!text.Any(static character => character is >= '\u3400' and <= '\u9fff')) return;
                var call = node.Ancestors().OfType<InvocationExpressionSyntax>().FirstOrDefault()?.Expression.ToString()
                    ?? node.Ancestors().OfType<AttributeSyntax>().FirstOrDefault()?.Name.ToString() ?? "declaration";
                rows.Add(new(text, kind, Path.GetRelativePath(root, path).Replace('\\', '/'), tree.GetLineSpan(node.Span).StartLinePosition.Line + 1, call));
            }
        }
        string output = Path.Combine(root, "artifacts", "Reports", "EditorTextInventory.tsv"); Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var lines = new List<string> { "id\tkind\tsource\tenglish\toccurrences\tlocations" };
        var entries = new List<Entry>();
        foreach (var group in rows.GroupBy(static row => (row.kind, row.text)).OrderBy(static group => group.First().path, StringComparer.Ordinal).ThenBy(static group => group.First().line))
        {
            string id = Inno.VisualNovel.Editor.Localization.EditorTextCatalog.Key(group.Key.text, group.Key.kind == "format");
            entries.Add(new(id, group.Key.kind, group.Key.text));
            lines.Add(string.Join('\t', id, group.Key.kind, Escape(group.Key.text), "", group.Count(), Escape(string.Join(" | ", group.Select(static row => row.path + ":" + row.line + " @ " + row.context)))));
        }
        File.WriteAllLines(output, lines, new UTF8Encoding(false));
        Console.WriteLine("Editor text inventory: " + rows.Count + " occurrences, " + (lines.Count - 1) + " unique literal/format entries. No source, native assets or user text were rewritten.");
        Console.WriteLine(output);
        return entries.AsReadOnly();
    }
    private static bool DiagnosticTemplate(SyntaxNode node)
    {
        SyntaxNode current = node;
        while (current.Parent is ParenthesizedExpressionSyntax or CastExpressionSyntax || current.Parent is ConditionalExpressionSyntax conditional && current != conditional.Condition) current = current.Parent;
        return current.Parent is ArgumentSyntax argument && argument.Parent?.Parent is ObjectCreationExpressionSyntax creation
            && creation.Type.ToString() is "AuthoringValidationException" or "AuthoringArgumentException" && creation.ArgumentList?.Arguments[0] == argument;
    }
    private static bool CompilerTemplate(SyntaxNode node) => node.Parent is ArgumentSyntax argument
        && argument.Parent?.Parent is InvocationExpressionSyntax call && call.Expression.ToString() is "Error" or "Warning"
        && call.ArgumentList.Arguments.Count > 1 && call.ArgumentList.Arguments[1] == argument;
    private static bool PreviewProgressTemplate(SyntaxNode node) => node.Parent is AssignmentExpressionSyntax assignment && assignment.Left.ToString() == "m_progress"
        || node.Parent is EqualsValueClauseSyntax initializer && initializer.Parent is VariableDeclaratorSyntax variable && variable.Identifier.ValueText == "m_progress";
    private static string Escape(string text) => text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\t", "\\t", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);
}
