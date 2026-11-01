using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Inno.VisualNovel.Model.Story.Nodes.Extensions;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Model.Extensions;

/// <summary>Freezes command declarations and compares exact contracts without retaining CLR metadata.</summary>
public static class CommandContract
{
    /// <summary>Validates bounded identities, parameter types and consistent lifecycle policy.</summary>
    /// <param name="definition">The complete declared contract.</param>
    public static void Validate(NovelCommandDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.id) || string.IsNullOrWhiteSpace(definition.title) || definition.parameters.Length > 64
            || ((int)definition.capabilities & ~31) != 0 || definition.parameters.Select(static item => item.id).Distinct(StringComparer.Ordinal).Count() != definition.parameters.Length
            || definition.capabilities.HasFlag(CommandCapabilities.ExternalEffects) && definition.capabilities.HasFlag(CommandCapabilities.Rollback))
            throw new InvalidOperationException("Command contract requires a stable identity, bounded unique parameters and consistent lifecycle guarantees.");
        foreach (var parameter in definition.parameters) parameter.Validate(parameter.defaultValue);
        if (definition.hasResult) new CommandParameter { id = "result", defaultValue = definition.resultDefault, members = definition.resultMembers }.Validate(definition.resultDefault);
    }
    /// <summary>Copies a declaration and all nested mutable value metadata.</summary>
    /// <param name="source">The source contract.</param>
    /// <returns>A detached contract.</returns>
    public static NovelCommandDefinition Copy(NovelCommandDefinition source) => new()
    {
        id = source.id, title = source.title, help = source.help, capabilities = source.capabilities, hasResult = source.hasResult,
        resultDefault = source.resultDefault.Copy(), resultMembers = source.resultMembers.ToArray(),
        parameters = source.parameters.Select(static parameter => new CommandParameter { id = parameter.id, label = parameter.label, help = parameter.help, defaultValue = parameter.defaultValue.Copy(), members = parameter.members.ToArray() }).ToArray()
    };
    /// <summary>Copies a call including its neutral schema and argument expression trees.</summary>
    /// <param name="source">The authored or compiled invocation.</param>
    /// <returns>A detached invocation.</returns>
    public static CommandNode Copy(CommandNode source) => new()
    {
        definition = Copy(source.definition), resultVariable = source.resultVariable,
        arguments = source.arguments.Select(static argument => new CommandArgument { id = argument.id, value = CopyExpression(argument.value) }).ToArray()
    };
    /// <summary>Computes an exact neutral contract identity, including defaults and enum members.</summary>
    /// <param name="definition">The validated contract.</param>
    /// <returns>A SHA-256 digest used to reject mismatched implementations.</returns>
    public static string Fingerprint(NovelCommandDefinition definition)
    {
        Validate(definition); var text = new StringBuilder();
        Part(definition.id); Part(definition.title); Part(definition.help); Part(((int)definition.capabilities).ToString(CultureInfo.InvariantCulture)); Part(definition.hasResult.ToString());
        Value(definition.resultDefault); Part(definition.resultMembers.Length.ToString(CultureInfo.InvariantCulture)); foreach (string member in definition.resultMembers) Part(member);
        Part(definition.parameters.Length.ToString(CultureInfo.InvariantCulture));
        foreach (var parameter in definition.parameters) { Part(parameter.id); Part(parameter.label); Part(parameter.help); Value(parameter.defaultValue); Part(parameter.members.Length.ToString(CultureInfo.InvariantCulture)); foreach (string member in parameter.members) Part(member); }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
        void Part(string value) { text.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value); }
        void Value(NovelValue value)
        {
            Part(value.kind.ToString());
            if (value.kind == NovelValueKind.List) { Part(value.elementKind.ToString()); Part(value.items.Length.ToString(CultureInfo.InvariantCulture)); foreach (var item in value.items) Value(item); }
            else Part(value.ToString());
        }
    }
    private static ExpressionDocument CopyExpression(
        ExpressionDocument source,
        int depth = 0
    ) => depth <= 64 ? new() { operation = source.operation, variableId = source.variableId, literal = source.literal.Copy(), operands = source.operands.Select(item => CopyExpression(item, depth + 1)).ToArray() }
        : throw new InvalidOperationException("Command expression exceeds its depth budget.");
}
