using InnoEngine.Assets;
using InnoEngine.Reflection;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Interface.Actions;

/// <summary>Publishes native custom UI action forms without serializing CLR handlers or delegates.</summary>
[StableTypeId("fd0c7316-14a5-4d89-8492-bfd0a0b9f1b0")]
public sealed class UiActionCatalogAsset : AssetObject
{
    /// <summary>Gets or sets the author-facing catalog title.</summary>
    [SerializableProperty] public string title { get; set; } = "UI actions";
    /// <summary>Gets or sets explicit action metadata supplied by the extension author.</summary>
    [SerializableProperty] public UiActionDefinition[] definitions { get; set; } = [];
}
