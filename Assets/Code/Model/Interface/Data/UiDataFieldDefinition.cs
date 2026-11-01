using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Extensions;

namespace Inno.VisualNovel.Model.Interface.Data;

/// <summary>Declares one typed source or row field using the existing neutral parameter schema.</summary>
[GenerateSerializationConverter]
public sealed class UiDataFieldDefinition : ISerializable
{
    /// <summary>Gets or sets the stable local field identity, label, type and enum members.</summary>
    [SerializableProperty] public CommandParameter schema { get; set; } = new();
    /// <summary>Gets or sets whether controls may submit validated values to this field.</summary>
    [SerializableProperty] public bool writable { get; set; }
}
