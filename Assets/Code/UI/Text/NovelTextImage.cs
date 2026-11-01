using InnoEngine.Mathematics;
using InnoEngine.Rendering;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.UI.Text;

/// <summary>Describes a visible inline image positioned by the same line layout as glyphs.</summary>
/// <param name="image">The borrowed imported image.</param>
/// <param name="bounds">Text-local logical bounds.</param>
public sealed record NovelTextImage(TextureAsset image, Rect bounds);
