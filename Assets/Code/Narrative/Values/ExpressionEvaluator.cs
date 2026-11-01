using System;
using System.Linq;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Narrative.Values;

/// <summary>Evaluates bounded typed expression trees without dynamic code execution.</summary>
public sealed class ExpressionEvaluator
{
    private readonly Func<string, NovelValue> m_resolve;
    private readonly Func<double>? m_random;

    /// <summary>Creates an evaluator for one invocation's variable lookup.</summary>
    /// <param name="resolve">Resolves a declared variable by stable identity.</param>
    /// <param name="random">An optional deterministic assignment-only source in the zero-to-one interval.</param>
    public ExpressionEvaluator(
        Func<string, NovelValue> resolve,
        Func<double>? random = null
    ) { m_resolve = resolve ?? throw new ArgumentNullException(nameof(resolve)); m_random = random; }

    /// <summary>Evaluates a well-formed expression; malformed or incompatible values fail explicitly.</summary>
    /// <param name="expression">The root expression.</param>
    /// <returns>A detached typed result.</returns>
    public NovelValue Evaluate(ExpressionDocument expression) => Evaluate(expression, 0);

    private NovelValue Evaluate(
        ExpressionDocument expression,
        int depth
    ) {
        ArgumentNullException.ThrowIfNull(expression);
        if (depth > 64)
            throw new InvalidOperationException("Expression depth exceeds 64.");
        int arity = ExpressionOperations.OperandCount(expression.operation);
        if (!Enum.IsDefined(expression.operation) || expression.operands.Length != arity)
            throw new InvalidOperationException("Expression operator or operand count is invalid.");
        NovelValue Operand(int index) => Evaluate(expression.operands[index], depth + 1);
        switch (expression.operation)
        {
            case ExpressionOperation.Literal: return expression.literal.Copy();
            case ExpressionOperation.Variable: return m_resolve(expression.variableId);
            case ExpressionOperation.Not: return NovelValue.Boolean(!Operand(0).boolean);
            case ExpressionOperation.And: return NovelValue.Boolean(Operand(0).boolean && Operand(1).boolean);
            case ExpressionOperation.Or: return NovelValue.Boolean(Operand(0).boolean || Operand(1).boolean);
            case ExpressionOperation.Negate:
                NovelValue number = Operand(0);
                return number.kind == NovelValueKind.Integer ? NovelValue.Integer(checked(-number.integer)) : NovelValue.Number(-number.number);
            case ExpressionOperation.ListCount: return NovelValue.Integer(Operand(0).items.LongLength);
        }
        NovelValue left = Operand(0);
        NovelValue right = Operand(1);
        if (expression.operation is ExpressionOperation.Equal or ExpressionOperation.NotEqual)
            return NovelValue.Boolean(left.Equals(right) == (expression.operation == ExpressionOperation.Equal));
        if (expression.operation is ExpressionOperation.ListGet or ExpressionOperation.ListContains or ExpressionOperation.ListAppend
            or ExpressionOperation.ListRemoveAt or ExpressionOperation.ListSet or ExpressionOperation.ListInsert)
        {
            NovelValue[] items = left.items;
            if (expression.operation == ExpressionOperation.ListContains) return NovelValue.Boolean(items.Contains(right));
            if (expression.operation == ExpressionOperation.ListAppend) return NovelValue.List(left.elementKind, [.. items, right]);
            long index = right.integer;
            long limit = expression.operation == ExpressionOperation.ListInsert ? items.LongLength : items.LongLength - 1;
            if (index < 0 || index > limit) throw new InvalidOperationException("List index is outside its declared range.");
            int at = checked((int)index);
            if (expression.operation == ExpressionOperation.ListGet) return items[at].Copy();
            if (expression.operation == ExpressionOperation.ListRemoveAt) return NovelValue.List(left.elementKind, [.. items[..at], .. items[(at + 1)..]]);
            NovelValue item = Operand(2);
            return NovelValue.List(left.elementKind, expression.operation == ExpressionOperation.ListInsert
                ? [.. items[..at], item, .. items[at..]] : [.. items[..at], item, .. items[(at + 1)..]]);
        }
        if (expression.operation == ExpressionOperation.Add && left.kind == NovelValueKind.String && right.kind == NovelValueKind.String)
            return NovelValue.String(left.text + right.text);
        if (expression.operation is ExpressionOperation.Less or ExpressionOperation.LessOrEqual or ExpressionOperation.Greater or ExpressionOperation.GreaterOrEqual)
        {
            int order = left.kind == NovelValueKind.Integer && right.kind == NovelValueKind.Integer ? left.integer.CompareTo(right.integer) : left.number.CompareTo(right.number);
            return NovelValue.Boolean(expression.operation switch { ExpressionOperation.Less => order < 0, ExpressionOperation.LessOrEqual => order <= 0, ExpressionOperation.Greater => order > 0, _ => order >= 0 });
        }
        if (expression.operation is ExpressionOperation.RandomInteger or ExpressionOperation.RandomNumber)
        {
            if (m_random is null) throw new InvalidOperationException("Random samples are allowed only in an explicit assignment.");
            double sample = m_random();
            if (!double.IsFinite(sample) || sample is < 0 or >= 1) throw new InvalidOperationException("The deterministic random source returned an invalid sample.");
            if (expression.operation == ExpressionOperation.RandomInteger)
            {
                long minimum = left.integer, maximum = right.integer;
                if (minimum >= maximum) throw new InvalidOperationException("Random integer range must have an increasing exclusive upper bound.");
                decimal fraction = (decimal)(ulong)(sample * 9007199254740992d) / 9007199254740992m;
                decimal value = minimum + decimal.Floor(fraction * ((decimal)maximum - minimum));
                return NovelValue.Integer((long)value);
            }
            if (left.number >= right.number) throw new InvalidOperationException("Random number range must have an increasing exclusive upper bound.");
            double number = left.number * (1 - sample) + right.number * sample;
            return NovelValue.Number(Math.Clamp(number, left.number, Math.BitDecrement(right.number)));
        }
        if (left.kind == NovelValueKind.Integer && right.kind == NovelValueKind.Integer && expression.operation != ExpressionOperation.Divide)
            return NovelValue.Integer(expression.operation switch
            {
                ExpressionOperation.Add => checked(left.integer + right.integer),
                ExpressionOperation.Subtract => checked(left.integer - right.integer),
                ExpressionOperation.Multiply => checked(left.integer * right.integer),
                ExpressionOperation.Remainder when right.integer != 0 => left.integer == long.MinValue && right.integer == -1 ? 0 : left.integer % right.integer,
                ExpressionOperation.Remainder => throw new DivideByZeroException("Story expression takes a remainder by zero."),
                _ => throw new InvalidOperationException("Unsupported integer operator.")
            });
        double a = left.number;
        double b = right.number;
        return NovelValue.Number(expression.operation switch
        {
            ExpressionOperation.Add => a + b,
            ExpressionOperation.Subtract => a - b,
            ExpressionOperation.Multiply => a * b,
            ExpressionOperation.Divide when b != 0 => a / b,
            ExpressionOperation.Divide => throw new DivideByZeroException("Story expression divides by zero."),
            ExpressionOperation.Remainder when b != 0 => a % b,
            ExpressionOperation.Remainder => throw new DivideByZeroException("Story expression takes a remainder by zero."),
            _ => throw new InvalidOperationException("Unsupported numeric operator.")
        });
    }
}
