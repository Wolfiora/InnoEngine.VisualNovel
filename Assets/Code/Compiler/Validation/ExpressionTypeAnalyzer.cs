using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Compiler.Validation;

internal sealed class ExpressionTypeAnalyzer(IEnumerable<VariableDefinition> variables)
{
    internal readonly record struct Shape(NovelValueKind kind, NovelValueKind element = NovelValueKind.Boolean);
    private readonly Dictionary<string, NovelValue> m_types = variables.ToDictionary(static item => item.id, static item => item.initialValue, StringComparer.Ordinal);

    internal NovelValueKind Infer(ExpressionDocument expression) => Describe(expression).kind;
    internal Shape Describe(
        ExpressionDocument expression,
        int depth = 0
    ) {
        if (expression is null || depth > 64 || !Enum.IsDefined(expression.operation))
            throw new InvalidOperationException("表达式为空、过深或包含未知操作。");
        int arity = ExpressionOperations.OperandCount(expression.operation);
        if (expression.operands.Length != arity)
            throw new InvalidOperationException($"{expression.operation} 需要 {arity} 个操作数。");
        if (expression.operation == ExpressionOperation.Literal)
            return Of(expression.literal);
        if (expression.operation == ExpressionOperation.Variable)
            return m_types.TryGetValue(expression.variableId, out NovelValue? type) ? Of(type)
                : throw new InvalidOperationException($"变量不存在：{expression.variableId}。");
        Shape left = Describe(expression.operands[0], depth + 1);
        if (expression.operation == ExpressionOperation.Not)
            return left.kind == NovelValueKind.Boolean ? left : Fail("Not 需要布尔值。");
        if (expression.operation == ExpressionOperation.Negate) return Numeric(left) ? left : Fail("取负需要数值。");
        if (expression.operation == ExpressionOperation.ListCount) return left.kind == NovelValueKind.List ? new(NovelValueKind.Integer) : Fail("计数需要列表。");
        Shape right = Describe(expression.operands[1], depth + 1);
        if (expression.operation is ExpressionOperation.ListGet or ExpressionOperation.ListContains or ExpressionOperation.ListAppend or ExpressionOperation.ListRemoveAt or ExpressionOperation.ListSet or ExpressionOperation.ListInsert)
        {
            if (left.kind != NovelValueKind.List) return Fail("列表操作需要声明元素类型的一维列表。");
            if (expression.operation is ExpressionOperation.ListContains or ExpressionOperation.ListAppend)
            {
                if (right.kind != left.element) return Fail("列表元素类型不匹配。");
                return expression.operation == ExpressionOperation.ListContains ? new(NovelValueKind.Boolean) : left;
            }
            if (right.kind != NovelValueKind.Integer) return Fail("列表索引必须是 Integer。");
            if (arity == 3 && Describe(expression.operands[2], depth + 1).kind != left.element) return Fail("列表元素类型不匹配。");
            return expression.operation == ExpressionOperation.ListGet ? new(left.element) : left;
        }
        if (expression.operation is ExpressionOperation.And or ExpressionOperation.Or)
            return left.kind == NovelValueKind.Boolean && right.kind == NovelValueKind.Boolean ? left
                : throw new InvalidOperationException("逻辑操作需要两个布尔值。");
        if (expression.operation is ExpressionOperation.Equal or ExpressionOperation.NotEqual)
            return left == right ? new(NovelValueKind.Boolean) : throw new InvalidOperationException("相等比较需要相同类型。");
        if (expression.operation == ExpressionOperation.Add && left.kind == NovelValueKind.String && right.kind == NovelValueKind.String)
            return left;
        if (!Numeric(left) || !Numeric(right))
            throw new InvalidOperationException("数值操作需要 Integer 或 Number。");
        if (expression.operation == ExpressionOperation.RandomInteger && (left.kind != NovelValueKind.Integer || right.kind != NovelValueKind.Integer)) return Fail("随机整数范围需要两个 Integer。");
        return new(expression.operation switch
        {
            ExpressionOperation.Less or ExpressionOperation.LessOrEqual or ExpressionOperation.Greater or ExpressionOperation.GreaterOrEqual => NovelValueKind.Boolean,
            ExpressionOperation.Divide or ExpressionOperation.RandomNumber => NovelValueKind.Number,
            _ => left.kind == NovelValueKind.Integer && right.kind == NovelValueKind.Integer ? NovelValueKind.Integer : NovelValueKind.Number
        });
    }
    private static Shape Of(NovelValue value) => new(value.kind, value.kind == NovelValueKind.List ? value.elementKind : NovelValueKind.Boolean);
    private static bool Numeric(Shape type) => type.kind is NovelValueKind.Integer or NovelValueKind.Number;
    private static Shape Fail(string reason) => throw new InvalidOperationException(reason);
}
