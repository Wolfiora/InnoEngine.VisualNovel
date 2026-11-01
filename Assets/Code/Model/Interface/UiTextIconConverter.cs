using InnoEngine.Serialization;
using InnoEngine.Rendering;

namespace Inno.VisualNovel.Model.Interface;

/// <summary>Preserves nullable image references in text resource declarations.</summary>
public sealed class UiTextIconConverter : SerializationConverter<UiTextIcon>
{
    /// <inheritdoc />
    public override void Write(
        SerializationWriter writer,
        UiTextIcon value
    ) { writer.Write("key", value.key); writer.Write<TextureAsset?>("image", value.image); }
    /// <inheritdoc />
    public override UiTextIcon Read(SerializationReader reader) => new() { key = reader.Read<string>("key"), image = reader.Read<TextureAsset?>("image") };
}
