using System;
using System.Globalization;
using System.Linq;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Values;

/// <summary>
/// Stores a closed story value without arbitrary CLR references or mutable shared list storage.
/// </summary>
[RequiresSerializationConverter]
public sealed class NovelValue : ISerializable, IEquatable<NovelValue>
{
    [SerializableProperty]
    private NovelValueKind m_kind;
    [SerializableProperty]
    private bool m_boolean;
    [SerializableProperty]
    private long m_integer;
    [SerializableProperty]
    private double m_number;
    [SerializableProperty]
    private string m_text = string.Empty;
    [SerializableProperty]
    private NovelValueKind m_elementKind;
    [SerializableProperty]
    private NovelValue[] m_items = [];

    /// <summary>Creates the default false value for native restoration.</summary>
    public NovelValue() { }

    /// <summary>Gets the declared category.</summary>
    public NovelValueKind kind => m_kind;
    /// <summary>Gets the element category of a list.</summary>
    public NovelValueKind elementKind => Require(NovelValueKind.List).m_elementKind;
    /// <summary>Gets a defensive copy of the list elements.</summary>
    public NovelValue[] items => Require(NovelValueKind.List).m_items.Select(static item => item.Copy()).ToArray();
    /// <summary>Gets the Boolean value; incompatible categories throw.</summary>
    public bool boolean => Require(NovelValueKind.Boolean).m_boolean;
    /// <summary>Gets the integer value; incompatible categories throw.</summary>
    public long integer => Require(NovelValueKind.Integer).m_integer;
    /// <summary>Gets a numeric value from an Integer or Number; other categories throw.</summary>
    public double number => m_kind == NovelValueKind.Integer ? m_integer : Require(NovelValueKind.Number).m_number;
    /// <summary>Gets text from a String or Enum; other categories throw.</summary>
    public string text => m_kind is NovelValueKind.String or NovelValueKind.Enum ? m_text : throw new InvalidOperationException("A text value is required.");

    /// <summary>Creates a Boolean value.</summary>
    /// <param name="value">The logical value.</param>
    /// <returns>A detached value.</returns>
    public static NovelValue Boolean(bool value) => new() { m_kind = NovelValueKind.Boolean, m_boolean = value };
    /// <summary>Creates an integer value.</summary>
    /// <param name="value">The exact integer.</param>
    /// <returns>A detached value.</returns>
    public static NovelValue Integer(long value) => new() { m_kind = NovelValueKind.Integer, m_integer = value };
    /// <summary>Creates a finite number.</summary>
    /// <param name="value">The finite numeric value.</param>
    /// <returns>A detached value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The number is not finite.</exception>
    public static NovelValue Number(double value) => double.IsFinite(value)
        ? new() { m_kind = NovelValueKind.Number, m_number = value }
        : throw new ArgumentOutOfRangeException(nameof(value), "Story numbers must be finite.");
    /// <summary>Creates Unicode text.</summary>
    /// <param name="value">The non-null text.</param>
    /// <returns>A detached value.</returns>
    public static NovelValue String(string value) => new() { m_kind = NovelValueKind.String, m_text = value ?? throw new ArgumentNullException(nameof(value)) };
    /// <summary>Creates a symbolic enumeration member.</summary>
    /// <param name="value">The non-empty member identity.</param>
    /// <returns>A detached value.</returns>
    public static NovelValue Enum(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new() { m_kind = NovelValueKind.Enum, m_text = value };
    }

    /// <summary>Creates a homogeneous list; nested lists are rejected.</summary>
    /// <param name="elementKind">The declared category for every item.</param>
    /// <param name="values">The initial items, copied on entry.</param>
    /// <returns>A detached list.</returns>
    public static NovelValue List(
        NovelValueKind elementKind,
        NovelValue[] values
    ) {
        ArgumentNullException.ThrowIfNull(values);
        if (elementKind == NovelValueKind.List || !System.Enum.IsDefined(elementKind)
            || values.Any(item => item is null || item.kind != elementKind))
            throw new ArgumentException("A list requires one declared non-list element type.", nameof(values));
        return new() { m_kind = NovelValueKind.List, m_elementKind = elementKind, m_items = values.Select(static item => item.Copy()).ToArray() };
    }

    /// <summary>Creates a deep independent value.</summary>
    /// <returns>A value with no mutable shared storage.</returns>
    public NovelValue Copy() => new()
    {
        m_kind = m_kind, m_boolean = m_boolean, m_integer = m_integer,
        m_number = m_number, m_text = m_text, m_elementKind = m_elementKind,
        m_items = m_items.Select(static item => item.Copy()).ToArray()
    };

    /// <summary>Compares complete typed values.</summary>
    /// <param name="other">The other value, or null.</param>
    /// <returns>True when types and values are identical.</returns>
    public bool Equals(NovelValue? other) => other is not null && m_kind == other.m_kind && m_kind switch
    {
        NovelValueKind.Boolean => m_boolean == other.m_boolean,
        NovelValueKind.Integer => m_integer == other.m_integer,
        NovelValueKind.Number => m_number.Equals(other.m_number),
        NovelValueKind.String or NovelValueKind.Enum => m_text == other.m_text,
        NovelValueKind.List => m_elementKind == other.m_elementKind && m_items.SequenceEqual(other.m_items),
        _ => false
    };
    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is NovelValue value && Equals(value);
    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(m_kind);
        hash.Add(ToString(), StringComparer.Ordinal);
        return hash.ToHashCode();
    }
    /// <summary>Formats a value using culture-independent story interpolation.</summary>
    /// <returns>A readable invariant representation.</returns>
    public override string ToString() => m_kind switch
    {
        NovelValueKind.Boolean => m_boolean ? "true" : "false",
        NovelValueKind.Integer => m_integer.ToString(CultureInfo.InvariantCulture),
        NovelValueKind.Number => m_number.ToString("G", CultureInfo.InvariantCulture),
        NovelValueKind.String or NovelValueKind.Enum => m_text,
        NovelValueKind.List => "[" + string.Join(", ", m_items.Select(static value => value.ToString())) + "]",
        _ => throw new InvalidOperationException("Unknown story value category.")
    };

    private NovelValue Require(NovelValueKind expected) => m_kind == expected
        ? this : throw new InvalidOperationException($"Expected {expected}; received {m_kind}.");
}
