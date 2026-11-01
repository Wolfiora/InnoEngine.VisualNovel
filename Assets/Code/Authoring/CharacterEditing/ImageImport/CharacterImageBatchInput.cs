using InnoEngine.Assets;
using InnoEngine.Rendering;

namespace Inno.VisualNovel.Authoring.CharacterEditing.ImageImport;

/// <summary>Describes one explicitly prepared native image or an actionable preparation failure.</summary>
/// <param name="path">The exact native source path used for filename matching.</param>
/// <param name="image">The canonical image loaded from that path, or null on failure.</param>
/// <param name="error">The preparation failure; an empty value requests normal matching.</param>
public sealed record CharacterImageBatchInput(
    AssetPath path,
    TextureAsset? image,
    string error = ""
);
