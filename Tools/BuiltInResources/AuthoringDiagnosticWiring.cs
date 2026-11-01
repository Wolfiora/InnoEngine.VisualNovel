using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Inno.VisualNovel.Tools.BuiltInResources;

// This maintenance operation retains exception families and separates declared text from runtime values.
internal static class AuthoringDiagnosticWiring
{
    internal static void Apply(string root)
    {
        string backup = Path.Combine(root, "artifacts", "SourceBackups", "AuthoringDiagnostics-" + Guid.NewGuid().ToString("N"));
        var report = new List<string>();
        foreach (string scope in new[] { "Authoring", "Editor" })
            foreach (string path in Directory.EnumerateFiles(Path.Combine(root, "Assets", "Code", scope), "*.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                string before = File.ReadAllText(path);
                var tree = CSharpSyntaxTree.ParseText(before, new CSharpParseOptions(LanguageVersion.Preview));
                var writer = new Wiring(scope == "Authoring", report, Path.GetRelativePath(root, path));
                var updated = (CompilationUnitSyntax)writer.Visit(tree.GetRoot())!;
                if (writer.conversions > 0 && !updated.Usings.Any(static item => item.Name?.ToString() == "Inno.VisualNovel.Authoring.Diagnostics"))
                    updated = updated.WithUsings(updated.Usings.Add(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("Inno.VisualNovel.Authoring.Diagnostics")).NormalizeWhitespace()
                        .WithTrailingTrivia(SyntaxFactory.EndOfLine(Environment.NewLine))));
                string after = updated.ToFullString();
                if (after == before) continue;
                if (CSharpSyntaxTree.ParseText(after).GetDiagnostics().Any(static item => item.Severity == DiagnosticSeverity.Error))
                    throw new InvalidOperationException("Diagnostic rewrite produced invalid syntax: " + path);
                string saved = Path.Combine(backup, Path.GetRelativePath(root, path)); Directory.CreateDirectory(Path.GetDirectoryName(saved)!);
                File.WriteAllText(saved, before, new UTF8Encoding(false)); File.WriteAllText(path, after, new UTF8Encoding(false));
            }
        string output = Path.Combine(root, "artifacts", "Reports", "AuthoringDiagnosticWiring.txt");
        File.WriteAllLines(output, report, new UTF8Encoding(false)); Console.WriteLine(output + ": " + report.Count + " source decisions; backups: " + backup);
    }

    private sealed class Wiring(
        bool authoring,
        List<string> report,
        string path
    ) : CSharpSyntaxRewriter
    {
        internal int conversions;
        public override SyntaxNode? VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            var updated = (ObjectCreationExpressionSyntax)base.VisitObjectCreationExpression(node)!;
            if (!authoring || updated.ArgumentList?.Arguments.Count is not > 0 || updated.Type.ToString() is not ("InvalidOperationException" or "ArgumentException")) return updated;
            var argument = updated.ArgumentList.Arguments[0];
            if (!HasChinese(argument.Expression)) return updated;
            ExpressionSyntax? message = Message(argument.Expression);
            if (message is null) { report.Add(path + " SKIP " + argument.Expression); return updated; }
            conversions++; report.Add(path + " MESSAGE " + message);
            return updated.WithType(SyntaxFactory.IdentifierName(updated.Type.ToString() == "ArgumentException" ? "AuthoringArgumentException" : "AuthoringValidationException").WithTriviaFrom(updated.Type))
                .WithArgumentList(updated.ArgumentList.WithArguments(updated.ArgumentList.Arguments.Replace(argument, argument.WithExpression(message.WithTriviaFrom(argument.Expression)))));
        }
        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            if (authoring || node.Name.Identifier.ValueText != "Message" || node.Expression is not IdentifierNameSyntax error) return base.VisitMemberAccessExpression(node);
            var caught = node.Ancestors().OfType<CatchClauseSyntax>().FirstOrDefault();
            if (caught?.Declaration?.Identifier.ValueText != error.Identifier.ValueText) return base.VisitMemberAccessExpression(node);
            string? owner = Owner(node);
            if (owner is null) return base.VisitMemberAccessExpression(node);
            report.Add(path + " PRESENT " + owner + ".Error(" + error + ")");
            return SyntaxFactory.ParseExpression(owner + ".Error(" + error + ")").WithTriviaFrom(node);
        }
        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var updated = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;
            if (updated.Expression is not MemberAccessExpressionSyntax call || call.Name.Identifier.ValueText != "Localize"
                || updated.ArgumentList.Arguments.Count != 1 || updated.ArgumentList.Arguments[0].Expression is not InvocationExpressionSyntax nested
                || nested.Expression.ToString() != call.Expression + ".Error") return updated;
            return nested.WithTriviaFrom(updated);
        }
    }

    private static string? Owner(SyntaxNode node)
    {
        var type = node.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (type is null) return null;
        string? owner = type.Members.OfType<FieldDeclarationSyntax>().Where(static field => field.Declaration.Type.ToString() is "StoryInspectorFields" or "EditorTextView")
            .SelectMany(static field => field.Declaration.Variables).Select(static variable => variable.Identifier.ValueText).FirstOrDefault()
            ?? type.Members.OfType<PropertyDeclarationSyntax>().Where(static property => property.Type.ToString() == "EditorTextView").Select(static property => property.Identifier.ValueText).FirstOrDefault()
            ?? type.ParameterList?.Parameters.Where(static parameter => parameter.Type?.ToString() is "StoryInspectorFields" or "EditorTextView").Select(static parameter => parameter.Identifier.ValueText).FirstOrDefault();
        if (owner is null) return null;
        if (node.Ancestors().OfType<MethodDeclarationSyntax>().Any(method => method.Modifiers.Any(SyntaxKind.StaticKeyword)
            && !method.ParameterList.Parameters.Any(parameter => parameter.Identifier.ValueText == owner))) return null;
        if (node.Ancestors().OfType<LocalFunctionStatementSyntax>().Any(method => method.Modifiers.Any(SyntaxKind.StaticKeyword)
            && !method.ParameterList.Parameters.Any(parameter => parameter.Identifier.ValueText == owner))) return null;
        return owner;
    }

    private static ExpressionSyntax? Message(ExpressionSyntax expression)
    {
        if (expression is ConditionalExpressionSyntax conditional)
        {
            var yes = Message(conditional.WhenTrue); var no = Message(conditional.WhenFalse);
            return yes is null || no is null ? null : conditional
                .WithWhenTrue(SyntaxFactory.CastExpression(SyntaxFactory.ParseTypeName("FormattableString"), yes))
                .WithWhenFalse(SyntaxFactory.CastExpression(SyntaxFactory.ParseTypeName("FormattableString"), no));
        }
        if (expression is InterpolatedStringExpressionSyntax) return expression;
        if (!StringExpression(expression)) return null;
        var parts = new List<InterpolatedStringContentSyntax>(); Add(expression);
        return SyntaxFactory.InterpolatedStringExpression(SyntaxFactory.Token(SyntaxKind.InterpolatedStringStartToken), SyntaxFactory.List(parts), SyntaxFactory.Token(SyntaxKind.InterpolatedStringEndToken));
        void Add(ExpressionSyntax item)
        {
            if (item is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.AddExpression) && StringExpression(item)) { Add(binary.Left); Add(binary.Right); }
            else if (item is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                string value = literal.Token.ValueText;
                string token = SyntaxFactory.Literal(value).Text[1..^1].Replace("{", "{{", StringComparison.Ordinal).Replace("}", "}}", StringComparison.Ordinal);
                parts.Add(SyntaxFactory.InterpolatedStringText(SyntaxFactory.Token(default, SyntaxKind.InterpolatedStringTextToken, token, value, default)));
            }
            else if (item is InterpolatedStringExpressionSyntax interpolation) parts.AddRange(interpolation.Contents);
            else parts.Add(SyntaxFactory.Interpolation(item.WithoutTrivia()));
        }
    }
    private static bool StringExpression(ExpressionSyntax expression) => expression is InterpolatedStringExpressionSyntax
        || expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression)
        || expression is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.AddExpression) && (StringExpression(binary.Left) || StringExpression(binary.Right));
    private static bool HasChinese(SyntaxNode node) => node.DescendantTokens().Any(static token => token.ValueText.Any(static character => character is >= '\u3400' and <= '\u9fff'));
}
