using InnoEngine.Assets;
using InnoEngine.Reflection;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Localization;

/// <summary>Stores editable game translations through the engine's native asset system.</summary>
[StableTypeId("fd0c7316-14a5-4d89-8492-bfd0a0b9f180")]
public sealed class LocalizationTableAsset : AssetObject
{
    /// <summary>Gets or sets the author-facing table title.</summary>
    [SerializableProperty] public string title { get; set; } = "Game text";
    /// <summary>Gets or sets the source language tag.</summary>
    [SerializableProperty] public string sourceLanguage { get; set; } = "zh-CN";
    /// <summary>Gets or sets available game languages in UI order.</summary>
    [SerializableProperty] public LanguageDefinition[] languages { get; set; } = [new()];
    /// <summary>Gets or sets uniquely identified source and translation rows.</summary>
    [SerializableProperty] public LocalizedText[] entries { get; set; } = [];
}
