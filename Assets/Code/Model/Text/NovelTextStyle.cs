namespace Inno.VisualNovel.Model.Text;

/// <summary>Describes immutable styling for a logical text range before font resolution.</summary>
/// <param name="bold">Whether to request emphasized weight.</param>
/// <param name="italic">Whether to request italic slant.</param>
/// <param name="size">An absolute logical size, or zero to inherit the control.</param>
/// <param name="color">An optional RRGGBBAA foreground override.</param>
/// <param name="font">A page-owned font resource key, empty to inherit.</param>
/// <param name="outline">Outline radius in logical pixels.</param>
/// <param name="outlineColor">Packed outline color.</param>
/// <param name="shadow">Shadow offset in logical pixels.</param>
/// <param name="shadowColor">Packed shadow color.</param>
public sealed record NovelTextStyle(bool bold = false, bool italic = false, float size = 0, uint? color = null,
    string font = "", float outline = 0, uint outlineColor = 0x000000FF, float shadow = 0, uint shadowColor = 0x000000A0);
