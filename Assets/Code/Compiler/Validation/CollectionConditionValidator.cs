using System;
using System.Collections.Generic;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Compiler.Validation;

/// <summary>Checks collection rules against shared variable declarations without executing or consuming random state.</summary>
public static class CollectionConditionValidator
{
    /// <summary>Requires a pure Boolean rule with declared, compatible variable references.</summary>
    /// <param name="condition">The authored unlock rule.</param>
    /// <param name="variables">Shared game declarations.</param>
    public static void Validate(
        ExpressionDocument condition,
        IEnumerable<VariableDefinition> variables
    ) {
        if (ExpressionOperations.UsesRandom(condition) || new ExpressionTypeAnalyzer(variables).Infer(condition) != NovelValueKind.Boolean)
            throw new InvalidOperationException("Collection unlock conditions must be pure Boolean expressions.");
    }
}
