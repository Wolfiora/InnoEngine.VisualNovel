using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Interface;

namespace Inno.VisualNovel.Model.Project;

/// <summary>Preserves an explicitly unassigned system screen reference.</summary>
public sealed class NovelScreenBindingConverter : SerializationConverter<NovelScreenBinding>
{
    /// <inheritdoc />
    public override void Write(
        SerializationWriter writer,
        NovelScreenBinding value
    ) {
        writer.Write("role", value.role);
        writer.Write<ScreenAsset?>("screen", value.screen);
    }
    /// <inheritdoc />
    public override NovelScreenBinding Read(SerializationReader reader) => new()
    { role = reader.Read<string>("role"), screen = reader.Read<ScreenAsset?>("screen") };
}
