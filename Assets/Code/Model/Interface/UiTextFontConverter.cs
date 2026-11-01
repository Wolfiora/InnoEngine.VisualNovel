using InnoEngine.Serialization;
using InnoEngine.Text;

namespace Inno.VisualNovel.Model.Interface;

/// <summary>Preserves nullable font references in text resource declarations.</summary>
public sealed class UiTextFontConverter : SerializationConverter<UiTextFont>
{
    /// <inheritdoc />
    public override void Write(
        SerializationWriter writer,
        UiTextFont value
    ) { writer.Write("key", value.key); writer.Write<FontAsset?>("font", value.font); }
    /// <inheritdoc />
    public override UiTextFont Read(SerializationReader reader) => new() { key = reader.Read<string>("key"), font = reader.Read<FontAsset?>("font") };
}
