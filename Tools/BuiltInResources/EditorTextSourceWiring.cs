using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Inno.VisualNovel.Tools.BuiltInResources;

// This source maintenance command rewrites only declared presentation arguments, never asset data or user values.
internal static class EditorTextSourceWiring
{
    internal static void Apply(string root)
    {
        var report = new List<string>();
        string backup = Path.Combine(root, "artifacts", "SourceBackups", "EditorText-" + Guid.NewGuid().ToString("N"));
        foreach (string path in Directory.EnumerateFiles(Path.Combine(root, "Assets", "Code", "Editor"), "*.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            string source = File.ReadAllText(path);
            if (Path.GetFileName(path) == "StoryInspectorFields.editor.cs") continue;
            string? owner = source.Contains("StoryInspectorFields m_fields", StringComparison.Ordinal) ? "m_fields"
                : source.Contains("StoryInspectorFields fields", StringComparison.Ordinal) ? "fields"
                : source.Contains("EditorTextView m_text", StringComparison.Ordinal) ? "m_text"
                : source.Contains("EditorTextView text", StringComparison.Ordinal) ? "text" : null;
            if (owner is null) continue;
            var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));
            var writer = new Wiring(owner, report, Path.GetRelativePath(root, path));
            var updated = writer.Visit(tree.GetRoot())!;
            if (updated.ToFullString() == source) continue;
            string target = Path.Combine(backup, Path.GetRelativePath(root, path)); Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, source, new UTF8Encoding(false));
            File.WriteAllText(path, updated.ToFullString(), new UTF8Encoding(false));
        }
        string output = Path.Combine(root, "artifacts", "Reports", "EditorTextWiring.txt");
        File.WriteAllLines(output, report, new UTF8Encoding(false)); Console.WriteLine(output + ": " + report.Count + " wiring decisions.");
    }

    private sealed class Wiring(
        string owner,
        List<string> report,
        string path
    ) : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var call = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;
            string name = call.Expression is MemberAccessExpressionSyntax member ? member.Name.Identifier.ValueText : call.Expression.ToString();
            string receiver = call.Expression is MemberAccessExpressionSyntax access ? access.Expression.ToString() : string.Empty;
            if (receiver == "BuiltInUiChoices" && !InsideChoices(node)) return WrapChoices(call);
            bool imgui = receiver == "ImGui";
            bool widgets = receiver == "widgets" || receiver.EndsWith(".widgets", StringComparison.Ordinal);
            if (receiver == owner && name == "Select" && call.ArgumentList.Arguments.Count == 4)
            {
                var options = call.ArgumentList.Arguments[3];
                var rewritten = (ExpressionSyntax)new OptionNames(owner).Visit(options.Expression)!;
                if (rewritten.ToFullString() != options.Expression.ToFullString())
                {
                    report.Add("OPTIONS " + path + ": " + options.Expression);
                    call = call.WithArgumentList(call.ArgumentList.WithArguments(call.ArgumentList.Arguments.Replace(options, options.WithExpression(rewritten))));
                }
            }
            int index = -1; bool interactive = false;
            if (imgui)
            {
                interactive = name is "Button" or "SmallButton" or "Selectable" or "CollapsingHeader" or "BeginTabItem" or "BeginMenu" or "MenuItem"
                    or "InputText" or "Checkbox" or "ColorEditLinear4" or "BeginCombo" or "BeginTabBar" or "InvisibleButton";
                if (interactive || name is "Text" or "SeparatorText" or "SetTooltip" or "TableSetupColumn") index = 0;
                else if (name == "TextColored") index = 1;
            }
            else if (widgets)
            {
                interactive = name == "BeginBoundedCombo";
                if (interactive || name is "Hint" or "WrappedText" or "EmptyState") index = 0;
            }
            else if (receiver == owner && name is "Text" or "MultilineText" or "WritingText" or "Number" or "Boolean" or "Select" or "Color") index = 1;
            else if (name is "Edit" && receiver is "documents" or "m_documents" or "m_resources" or "m_stories") index = 1;
            if (index < 0 || call.ArgumentList.Arguments.Count <= index) return call;
            var expression = call.ArgumentList.Arguments[index].Expression;
            if (expression is InvocationExpressionSyntax done && done.Expression.ToString().StartsWith(owner + ".", StringComparison.Ordinal)) return call;
            if (!HasSource(expression)) return call;
            if (node.Ancestors().OfType<MethodDeclarationSyntax>().Any(method => method.Modifiers.Any(SyntaxKind.StaticKeyword) && !method.ParameterList.Parameters.Any(parameter => parameter.Identifier.ValueText == owner))
                || node.Ancestors().OfType<AnonymousFunctionExpressionSyntax>().Any(lambda => lambda.Modifiers.Any(SyntaxKind.StaticKeyword)))
            { report.Add("STATIC REVIEW " + path + ": " + expression); return call; }
            ExpressionSyntax translated = new CaptionParts(owner).Visit(expression) as ExpressionSyntax ?? expression;
            string replacement = interactive
                ? expression is LiteralExpressionSyntax ? owner + ".Label(" + expression + ")" : owner + ".Caption(" + expression + ", " + translated + ")"
                : translated.ToString();
            var next = SyntaxFactory.ParseExpression(replacement).WithTriviaFrom(expression);
            if (next.ContainsDiagnostics) throw new InvalidOperationException("Invalid presentation rewrite: " + path + " / " + replacement);
            report.Add("WIRED " + path + ": " + expression);
            return call.WithArgumentList(call.ArgumentList.WithArguments(call.ArgumentList.Arguments.Replace(call.ArgumentList.Arguments[index], call.ArgumentList.Arguments[index].WithExpression(next))));
        }

        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            var updated = (MemberAccessExpressionSyntax)base.VisitMemberAccessExpression(node)!;
            return updated.Expression.ToString() == "BuiltInUiChoices" && updated.Name.Identifier.ValueText is "Pages" or "Actions" or "Images" or "Arguments" or "Rows" or "SelectorRows"
                && !InsideChoices(node) ? WrapChoices(updated) : updated;
        }
        private ExpressionSyntax WrapChoices(ExpressionSyntax node)
        {
            report.Add("BUILTIN METADATA " + path + ": " + node);
            return SyntaxFactory.ParseExpression(owner + ".BuiltIn(" + node.WithoutTrivia() + ")").WithTriviaFrom(node);
        }
        private bool InsideChoices(SyntaxNode node) => node.Ancestors().OfType<InvocationExpressionSyntax>().FirstOrDefault()?.Expression.ToString() == owner + ".BuiltIn";

        public override SyntaxNode? VisitAssignmentExpression(AssignmentExpressionSyntax node)
        {
            var updated = (AssignmentExpressionSyntax)base.VisitAssignmentExpression(node)!;
            if (updated.Left is not IdentifierNameSyntax field || field.Identifier.ValueText is not ("m_message" or "m_error" or "m_result" or "m_moveError")
                || !HasSource(updated.Right) || StaticContext(node)) return updated;
            return updated.WithRight((ExpressionSyntax)new CaptionParts(owner).Visit(updated.Right)!);
        }
        public override SyntaxNode? VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            var updated = (ObjectCreationExpressionSyntax)base.VisitObjectCreationExpression(node)!;
            if (updated.Type.ToString() is not ("InvalidOperationException" or "ArgumentException") || updated.ArgumentList?.Arguments.FirstOrDefault() is not { } first
                || !HasSource(first.Expression) || StaticContext(node)) return updated;
            return updated.WithArgumentList(updated.ArgumentList.WithArguments(updated.ArgumentList.Arguments.Replace(first,
                first.WithExpression((ExpressionSyntax)new CaptionParts(owner).Visit(first.Expression)!))));
        }
        private bool StaticContext(SyntaxNode node) => node.Ancestors().OfType<MethodDeclarationSyntax>().Any(method => method.Modifiers.Any(SyntaxKind.StaticKeyword) && !method.ParameterList.Parameters.Any(parameter => parameter.Identifier.ValueText == owner))
            || node.Ancestors().OfType<LocalFunctionStatementSyntax>().Any(method => method.Modifiers.Any(SyntaxKind.StaticKeyword) && !method.ParameterList.Parameters.Any(parameter => parameter.Identifier.ValueText == owner))
            || node.Ancestors().OfType<AnonymousFunctionExpressionSyntax>().Any(lambda => lambda.Modifiers.Any(SyntaxKind.StaticKeyword));
    }

    private sealed class OptionNames(string owner) : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitTupleExpression(TupleExpressionSyntax node)
        {
            if (node.Arguments.Count != 2 || node.Arguments[1].Expression is not LiteralExpressionSyntax literal || !HasSource(literal)
                || node.Ancestors().OfType<AnonymousFunctionExpressionSyntax>().Any(lambda => lambda.Modifiers.Any(SyntaxKind.StaticKeyword))) return base.VisitTupleExpression(node);
            return node.WithArguments(node.Arguments.Replace(node.Arguments[1], node.Arguments[1].WithExpression(
                SyntaxFactory.ParseExpression(owner + ".Localize(" + literal + ")").WithTriviaFrom(literal))));
        }
    }

    private sealed class CaptionParts(string owner) : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
            => node.Expression.ToString().StartsWith(owner + ".", StringComparison.Ordinal) || node.Expression.ToString() is "Format" or "Label" or "Localize" or "Caption" ? node : base.VisitInvocationExpression(node);
        public override SyntaxNode? VisitLiteralExpression(LiteralExpressionSyntax node)
            => HasSource(node) ? SyntaxFactory.ParseExpression(owner + ".Localize(" + node.WithoutTrivia() + ")").WithTriviaFrom(node) : node;
        public override SyntaxNode? VisitInterpolatedStringExpression(InterpolatedStringExpressionSyntax node)
        {
            if (!HasSource(node)) return node;
            var format = new StringBuilder(); var arguments = new List<string>();
            foreach (var part in node.Contents)
                if (part is InterpolatedStringTextSyntax text) format.Append(text.TextToken.ValueText);
                else if (part is InterpolationSyntax slot)
                {
                    format.Append('{').Append(arguments.Count); arguments.Add(slot.Expression.ToString());
                    if (slot.AlignmentClause is { } alignment) format.Append(',').Append(alignment.Value);
                    if (slot.FormatClause is { } spec) format.Append(':').Append(spec.FormatStringToken.ValueText);
                    format.Append('}');
                }
            string literal = SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(format.ToString())).ToString();
            return SyntaxFactory.ParseExpression(owner + ".Format(" + literal + (arguments.Count == 0 ? "" : ", " + string.Join(", ", arguments)) + ")").WithTriviaFrom(node);
        }
    }
    private static bool HasSource(SyntaxNode node) => node.DescendantTokens().Any(static token => token.ValueText.Any(static character => character is >= '\u3400' and <= '\u9fff'));
}
