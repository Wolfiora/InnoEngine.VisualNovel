using InnoEngine.Assets;
using InnoEngine.Reflection;
using InnoEngine.Serialization;
using InnoEngine.Text;
using Inno.VisualNovel.Model.Project;

namespace Inno.VisualNovel.Model.Interface.Themes;

/// <summary>Stores an editable native style library and optional complete shell page mappings.</summary>
[StableTypeId("fd0c7316-14a5-4d89-8492-bfd0a0b9f1d0")]
public sealed class ThemeAsset : AssetObject
{
    /// <summary>Gets or sets the stable game library key.</summary>
    [SerializableProperty] public string resourceId { get; set; } = "theme";
    /// <summary>Gets or sets the author-facing name.</summary>
    [SerializableProperty] public string title { get; set; } = "New theme";
    /// <summary>Gets or sets the theme's intended appearance and use.</summary>
    [SerializableProperty] public string description { get; set; } = string.Empty;
    /// <summary>Gets or sets the optional default page font.</summary>
    [SerializableProperty] public FontAsset? font { get; set; }
    /// <summary>Gets or sets named packed colors.</summary>
    [SerializableProperty] public ThemeColorToken[] colors { get; set; } = [];
    /// <summary>Gets or sets named logical metrics.</summary>
    [SerializableProperty] public ThemeMetricToken[] metrics { get; set; } = [];
    /// <summary>Gets or sets named style classes with acyclic inheritance.</summary>
    [SerializableProperty] public ThemeStyle[] styles { get; set; } = [];
    /// <summary>Gets or sets optional native screen overrides by existing shell role.</summary>
    [SerializableProperty] public NovelScreenBinding[] screens { get; set; } = [];
}
