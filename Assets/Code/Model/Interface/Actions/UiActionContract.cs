using System;
using System.Globalization;
using System.Linq;
using Inno.VisualNovel.Model.Extensions;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Model.Interface.Actions;

/// <summary>Reuses typed command contract validation for bounded, synchronous custom UI actions.</summary>
public static class UiActionContract
{
    /// <summary>Rejects built-in identity collisions, inconsistent lifecycle policy and unsupported asynchronous action contracts.</summary>
    /// <param name="definition">The complete authoring declaration.</param>
    public static void Validate(UiActionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.id is null || definition.title is null || definition.help is null || definition.argument is null
            || !definition.id.StartsWith("custom.", StringComparison.Ordinal) || definition.id.Length is < 8 or > 128 || definition.title.Length > 256
            || definition.help.Length > 4096 || definition.capabilities.HasFlag(CommandCapabilities.Cancel) || !definition.capabilities.HasFlag(CommandCapabilities.Save))
            throw new InvalidOperationException("UI actions require a bounded custom. identity, a native-session-state guarantee and synchronous execution.");
        CommandContract.Validate(Project(definition));
    }

    /// <summary>Validates bounded, unique action declarations before any host resources are allocated.</summary>
    /// <param name="definitions">The complete game or catalog metadata.</param>
    public static void ValidateCatalog(UiActionDefinition[] definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        if (definitions.Length > 128) throw new InvalidOperationException("A UI action catalog supports at most 128 contracts.");
        foreach (var definition in definitions) Validate(definition);
        if (definitions.Select(static item => item.id).Distinct(StringComparer.Ordinal).Count() != definitions.Length)
            throw new InvalidOperationException("UI action stable identities must be unique.");
    }

    /// <summary>Copies every mutable argument default and enum member.</summary>
    /// <param name="source">The source declaration.</param>
    /// <returns>An independently editable action contract.</returns>
    public static UiActionDefinition Copy(UiActionDefinition source)
    {
        var contract = CommandContract.Copy(Project(source));
        return new() { id = source.id, title = source.title, help = source.help, capabilities = source.capabilities, argument = contract.parameters[0] };
    }

    /// <summary>Computes the exact neutral schema fingerprint using the existing command contract algorithm.</summary>
    /// <param name="definition">The validated action declaration.</param>
    /// <returns>A stable fingerprint used to reject implementation mismatches.</returns>
    public static string Fingerprint(UiActionDefinition definition) { Validate(definition); return CommandContract.Fingerprint(Project(definition)); }

    /// <summary>Resolves a typed bound argument or a literal authored value, validating enum and list element membership.</summary>
    /// <param name="definition">The declared action schema.</param>
    /// <param name="literal">The fixed neutral argument text.</param>
    /// <param name="bound">The optional exact typed field value.</param>
    /// <returns>A defensive validated value.</returns>
    public static NovelValue ResolveArgument(
        UiActionDefinition definition,
        string literal,
        NovelValue? bound = null
    ) {
        Validate(definition);
        NovelValue value = bound?.Copy() ?? (literal.Length == 0 && definition.argument.defaultValue.kind != NovelValueKind.String ? definition.argument.defaultValue.Copy()
            : definition.argument.defaultValue.kind switch
            {
                NovelValueKind.Boolean => NovelValue.Boolean(bool.Parse(literal)),
                NovelValueKind.Integer => NovelValue.Integer(long.Parse(literal, NumberStyles.Integer, CultureInfo.InvariantCulture)),
                NovelValueKind.Number => NovelValue.Number(double.Parse(literal, NumberStyles.Float, CultureInfo.InvariantCulture)),
                NovelValueKind.Enum => NovelValue.Enum(literal),
                NovelValueKind.String => NovelValue.String(literal),
                _ => throw new InvalidOperationException("List UI arguments require a typed field binding or the authored default.")
            });
        definition.argument.Validate(value); return value;
    }

    private static NovelCommandDefinition Project(UiActionDefinition definition) => new()
    { id = definition.id, title = definition.title, help = definition.help, capabilities = definition.capabilities, parameters = [definition.argument] };
}
