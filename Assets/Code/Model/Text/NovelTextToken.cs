namespace Inno.VisualNovel.Model.Text;

/// <summary>Describes a literal range or a whole interpolation before variable resolution.</summary>
/// <param name="start">Inclusive authored offset.</param>
/// <param name="length">Authored length including interpolation braces.</param>
/// <param name="value">The literal text or stable variable identity.</param>
/// <param name="variable">Whether the value names a declared variable.</param>
public sealed record NovelTextToken(int start, int length, string value, bool variable);
