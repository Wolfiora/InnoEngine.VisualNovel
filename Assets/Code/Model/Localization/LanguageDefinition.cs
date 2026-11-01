using InnoEngine.Serialization;
using InnoEngine.Text;
using System.Linq;

namespace Inno.VisualNovel.Model.Localization;

/// <summary>Declares a game language independently of the editor's own locale.</summary>
[GenerateSerializationConverter]
public sealed class LanguageDefinition : ISerializable
{
    /// <summary>Gets or sets the stable language tag.</summary>
    [SerializableProperty] public string id { get; set; } = "zh-CN";
    /// <summary>Gets or sets the language's native display name.</summary>
    [SerializableProperty] public string name { get; set; } = "简体中文";
    /// <summary>Gets or sets the optional language-specific default UI font.</summary>
    [SerializableProperty] public FontAsset? font { get; set; }
    /// <summary>Gets or sets the ordered fallback chain, resolved through the engine text shaper.</summary>
    [SerializableProperty] public FontAsset[] fallbackFonts { get; set; } = [];
    /// <summary>Gets or sets explicit imported image replacements for this language.</summary>
    [SerializableProperty] public LocalizedImageVariant[] images { get; set; } = [];
    /// <summary>Copies language metadata and presentation arrays without duplicating imported resource identities.</summary>
    /// <returns>An independently editable language declaration.</returns>
    public LanguageDefinition Copy() => new() { id = id, name = name, font = font, fallbackFonts = fallbackFonts.ToArray(), images = images.Select(static item => item.Copy()).ToArray() };
}
