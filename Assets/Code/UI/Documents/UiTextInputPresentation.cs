using InnoEngine.Mathematics;

namespace Inno.VisualNovel.UI.Documents;

/// <summary>Contains transient single-line input decorations in the same logical coordinates as ordinary UI paint.</summary>
/// <param name="selection">The visible selection rectangle, absent when no range is selected.</param>
/// <param name="caret">The focused insertion caret; it is excluded from save capture.</param>
public sealed record UiTextInputPresentation(Rect? selection, Rect? caret);
