using System;
using System.Linq;
using InnoEngine.Serialization;
using InnoEngine.Rendering;
using InnoEngine.Text;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Text;
using Inno.VisualNovel.UI.Text;

namespace Inno.VisualNovel.UI.Documents;

/// <summary>Preserves a resolved UI image using neutral geometry, text and persistent resource identities.</summary>
[GenerateSerializationConverter]
public sealed class UiFrameSnapshot : ISerializable
{
    /// <summary>Gets or sets the captured logical width.</summary>
    [SerializableProperty] public float width { get; set; }
    /// <summary>Gets or sets the captured logical height.</summary>
    [SerializableProperty] public float height { get; set; }
    /// <summary>Gets or sets ordered, detached paint declarations.</summary>
    [SerializableProperty] public UiPaintSnapshot[] items { get; set; } = [];

    /// <summary>Freezes the exact visible UI without retaining live controls or graphics resources.</summary>
    /// <param name="frame">The most recently prepared UI image.</param>
    /// <returns>A native-serializable image declaration.</returns>
    public static UiFrameSnapshot Capture(UiFrame frame) => new()
    {
        width = frame.width, height = frame.height,
        items = frame.items.Select(UiPaintSnapshot.Capture).ToArray()
    };

    /// <summary>Validates and reconstructs paint commands against the current game's explicit resource closure.</summary>
    /// <param name="font">Resolves persistent font identities.</param>
    /// <param name="image">Resolves persistent image identities.</param>
    /// <returns>An independently reconstructed UI image.</returns>
    public UiFrame Restore(
        Func<string, FontAsset> font,
        Func<string, TextureAsset> image
    ) {
        if (!float.IsFinite(width) || !float.IsFinite(height) || width is < 1 or > 16384 || height is < 1 or > 16384 || items.Length > 16384)
            throw new InvalidOperationException("Captured UI exceeds its canvas or item budget.");
        return new(width, height, Array.AsReadOnly(items.Select(item => item.Restore(font, image)).ToArray()));
    }
}

