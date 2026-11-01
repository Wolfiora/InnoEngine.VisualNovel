using System;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Values;

/// <summary>Serializes only the active closed value and validates restored data through its factories.</summary>
public sealed class NovelValueConverter : SerializationConverter<NovelValue>
{
    /// <inheritdoc />
    public override void Write(
        SerializationWriter writer,
        NovelValue value
    ) {
        writer.Write("kind", value.kind);
        switch (value.kind)
        {
            case NovelValueKind.Boolean: writer.Write("value", value.boolean); break;
            case NovelValueKind.Integer: writer.Write("value", value.integer); break;
            case NovelValueKind.Number: writer.Write("value", value.number); break;
            case NovelValueKind.String:
            case NovelValueKind.Enum: writer.Write("value", value.text); break;
            case NovelValueKind.List:
                writer.Write("elementKind", value.elementKind);
                writer.Write("value", value.items);
                break;
            default: throw new InvalidOperationException("Unknown story value kind.");
        }
    }

    /// <inheritdoc />
    public override NovelValue Read(SerializationReader reader) => reader.Read<NovelValueKind>("kind") switch
    {
        NovelValueKind.Boolean => NovelValue.Boolean(reader.Read<bool>("value")),
        NovelValueKind.Integer => NovelValue.Integer(reader.Read<long>("value")),
        NovelValueKind.Number => NovelValue.Number(reader.Read<double>("value")),
        NovelValueKind.String => NovelValue.String(reader.Read<string>("value")),
        NovelValueKind.Enum => NovelValue.Enum(reader.Read<string>("value")),
        NovelValueKind.List => NovelValue.List(reader.Read<NovelValueKind>("elementKind"), reader.Read<NovelValue[]>("value")),
        _ => throw new InvalidOperationException("Unknown restored story value kind.")
    };
}
