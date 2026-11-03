using InnoEngine.Assets;
using InnoEngine.Reflection;
using InnoEngine.Serialization;
using System.Linq;

namespace Inno.VisualNovel.Model.Values;

/// <summary>Owns reusable game/global declarations shared by explicitly referencing chapters.</summary>
[StableTypeId("00cfe82c-4e8a-41a9-83e3-024e8814b91c")]
public sealed class VariableTableAsset : AssetObject
{
    /// <summary>Gets or sets the author-facing library title.</summary>
    [SerializableProperty] public string title { get; set; } = "Shared variables";
    /// <summary>Gets or sets typed shared declarations; call-local values remain in chapter declarations.</summary>
    [SerializableProperty] public VariableDefinition[] variables { get; set; } = [];
    /// <summary>Copies declaration values while leaving persistent asset identity with the native source owner.</summary>
    /// <returns>An independently editable table without mutable default aliases.</returns>
    public VariableTableAsset Copy() => new() { title = title, variables = variables.Select(VariableValueRules.Copy).ToArray() };
}
