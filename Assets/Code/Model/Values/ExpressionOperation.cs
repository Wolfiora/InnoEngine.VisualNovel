using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Values;

/// <summary>Identifies a typed, non-script expression operation.</summary>
public enum ExpressionOperation
{
    /// <summary>Returns the stored literal.</summary>
    Literal,
    /// <summary>Resolves a declared variable.</summary>
    Variable,
    /// <summary>Adds numeric values or joins strings.</summary>
    Add,
    /// <summary>Subtracts numeric values.</summary>
    Subtract,
    /// <summary>Multiplies numeric values.</summary>
    Multiply,
    /// <summary>Divides numeric values.</summary>
    Divide,
    /// <summary>Compares typed values.</summary>
    Equal,
    /// <summary>Compares numeric ordering.</summary>
    Less,
    /// <summary>Combines Boolean operands with short-circuit evaluation.</summary>
    And,
    /// <summary>Combines Boolean operands with short-circuit evaluation.</summary>
    Or,
    /// <summary>Negates one Boolean operand.</summary>
    Not,
    /// <summary>Compares typed values for inequality.</summary>
    NotEqual,
    /// <summary>Compares inclusive numeric ordering.</summary>
    LessOrEqual,
    /// <summary>Compares reverse numeric ordering.</summary>
    Greater,
    /// <summary>Compares inclusive reverse numeric ordering.</summary>
    GreaterOrEqual,
    /// <summary>Returns the remainder of a numeric division.</summary>
    Remainder,
    /// <summary>Negates a numeric value.</summary>
    Negate,
    /// <summary>Counts declared one-dimensional list items.</summary>
    ListCount,
    /// <summary>Reads a list item at a zero-based index.</summary>
    ListGet,
    /// <summary>Tests typed list membership.</summary>
    ListContains,
    /// <summary>Returns a list with an appended compatible item.</summary>
    ListAppend,
    /// <summary>Returns a list without one indexed item.</summary>
    ListRemoveAt,
    /// <summary>Returns a list with one indexed item replaced.</summary>
    ListSet,
    /// <summary>Returns a list with an item inserted at an index.</summary>
    ListInsert,
    /// <summary>Samples an inclusive lower, exclusive upper integer range during assignment.</summary>
    RandomInteger,
    /// <summary>Samples an inclusive lower, exclusive upper finite numeric range during assignment.</summary>
    RandomNumber
}
