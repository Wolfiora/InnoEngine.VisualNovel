namespace Inno.VisualNovel.Model.Values;

/// <summary>
/// Identifies a closed, persistence-safe story value category.
/// </summary>
public enum NovelValueKind
{
    /// <summary>Represents a Boolean value.</summary>
    Boolean,
    /// <summary>Represents a signed integer.</summary>
    Integer,
    /// <summary>Represents a finite floating-point number.</summary>
    Number,
    /// <summary>Represents Unicode text.</summary>
    String,
    /// <summary>Represents one declared symbolic choice.</summary>
    Enum,
    /// <summary>Represents a homogeneous one-dimensional list.</summary>
    List
}
