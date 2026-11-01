using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Compiler;

[GenerateSerializationConverter]
internal sealed class VariableDeclarationSnapshot : ISerializable
{
    [SerializableProperty]
    public VariableDefinition[] items { get; set; } = [];
}
