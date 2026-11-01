using System;

namespace Inno.VisualNovel.Player.Persistence;

/// <summary>Separates manual saves, rotating automatic saves and the single quick slot.</summary>
public enum SaveSlotKind
{
    /// <summary>A user-selected numbered slot.</summary>
    Manual,
    /// <summary>A system-managed rotating slot.</summary>
    Automatic,
    /// <summary>The dedicated quick-save slot.</summary>
    Quick
}

/// <summary>Identifies one bounded logical slot independently of filesystem paths.</summary>
public readonly record struct SaveSlot
{
    /// <summary>Creates a slot identity; service configuration additionally bounds numbered slots.</summary>
    /// <param name="kind">The slot category.</param>
    /// <param name="index">Zero-based slot number.</param>
    public SaveSlot(
        SaveSlotKind kind,
        int index
    ) {
        if (!Enum.IsDefined(kind) || index < 0 || kind == SaveSlotKind.Quick && index != 0) throw new ArgumentOutOfRangeException(nameof(index));
        this.kind = kind;
        this.index = index;
    }
    /// <summary>Gets the independent slot category.</summary>
    public SaveSlotKind kind { get; }
    /// <summary>Gets the zero-based slot number.</summary>
    public int index { get; }
}
