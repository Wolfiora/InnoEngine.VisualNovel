using System;

namespace Inno.VisualNovel.Model.Values;

/// <summary>Shares operator shape between visual editing, validation and evaluation.</summary>
public static class ExpressionOperations
{
    /// <summary>Copies a bounded expression while validating its operator arity and eliminating mutable aliases.</summary>
    /// <param name="source">The caller-owned expression.</param>
    /// <returns>An independently owned tree with at most 4096 nodes and 64 nested levels.</returns>
    public static ExpressionDocument Copy(ExpressionDocument source)
    {
        int remaining = 4096;
        return CopyNode(source, 0);
        ExpressionDocument CopyNode(
            ExpressionDocument node,
            int depth
        ) {
            if (node is null || depth > 64 || --remaining < 0 || !Enum.IsDefined(node.operation)
                || node.operands is null || node.operands.Length != OperandCount(node.operation))
                throw new InvalidOperationException("Expression exceeds its shape or size bounds.");
            var operands = new ExpressionDocument[node.operands.Length];
            for (int index = 0; index < operands.Length; index++) operands[index] = CopyNode(node.operands[index], depth + 1);
            return new() { operation = node.operation, variableId = node.variableId, literal = node.literal.Copy(), operands = operands };
        }
    }
    /// <summary>Gets the exact ordered operand count for a supported operator.</summary>
    /// <param name="operation">The closed expression operator.</param>
    /// <returns>Zero, one, two or three operands; unknown operators throw.</returns>
    public static int OperandCount(ExpressionOperation operation) => operation switch
    {
        ExpressionOperation.Literal or ExpressionOperation.Variable => 0,
        ExpressionOperation.Not or ExpressionOperation.Negate or ExpressionOperation.ListCount => 1,
        ExpressionOperation.ListSet or ExpressionOperation.ListInsert => 3,
        _ when Enum.IsDefined(operation) => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    /// <summary>Reports whether an expression contains an assignment-only random sample.</summary>
    /// <param name="expression">The bounded root.</param>
    /// <returns>Whether evaluating the tree consumes random state.</returns>
    public static bool UsesRandom(ExpressionDocument expression) => UsesRandom(expression, 0);
    private static bool UsesRandom(
        ExpressionDocument expression,
        int depth
    ) {
        if (depth > 64) throw new InvalidOperationException("Expression exceeds the supported depth.");
        if (expression.operation is ExpressionOperation.RandomInteger or ExpressionOperation.RandomNumber) return true;
        foreach (ExpressionDocument operand in expression.operands) if (UsesRandom(operand, depth + 1)) return true;
        return false;
    }
}
