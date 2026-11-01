using InnoEngine.Rendering;
using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Model.Collections;

/// <summary>Preserves optional artwork without serializing arbitrary object values.</summary>
public sealed class CollectionEntryConverter : SerializationConverter<CollectionEntry>
{
    /// <inheritdoc />
    public override void Write(
        SerializationWriter writer,
        CollectionEntry value
    ) {
        writer.Write("id", value.id); writer.Write("kind", value.kind); writer.Write("title", value.title); writer.Write("description", value.description); writer.Write("author", value.author);
        writer.Write<TextureAsset?>("image", value.image); writer.Write("musicCueId", value.musicCueId); writer.Write("chapterId", value.chapterId); writer.Write("entryId", value.entryId);
        writer.Write("initiallyUnlocked", value.initiallyUnlocked); writer.Write("unlockCondition", value.unlockCondition);
        writer.Write("initialValues", value.initialValues);
        writer.Write("variants", value.variants);
    }
    /// <inheritdoc />
    public override CollectionEntry Read(SerializationReader reader) => new()
    {
        id = reader.Read<string>("id"), kind = reader.Read<CollectionKind>("kind"), title = reader.Read<string>("title"), description = reader.Read<string>("description"), author = reader.Read<string>("author"),
        image = reader.Read<TextureAsset?>("image"), musicCueId = reader.Read<string>("musicCueId"), chapterId = reader.Read<string>("chapterId"), entryId = reader.Read<string>("entryId"),
        initiallyUnlocked = reader.Read<bool>("initiallyUnlocked"), unlockCondition = reader.Read<ExpressionDocument>("unlockCondition"), initialValues = reader.Read<CollectionVariable[]>("initialValues"),
        variants = reader.Read<GalleryVariant[]>("variants")
    };
}
