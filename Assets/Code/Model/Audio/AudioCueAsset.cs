using InnoEngine.Assets;
using InnoEngine.Audio;
using InnoEngine.Reflection;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Audio;

/// <summary>Declares an imported sound and its reusable playback and attribution settings.</summary>
[StableTypeId("fd0c7316-14a5-4d89-8492-bfd0a0b9f150")]
public sealed class AudioCueAsset : AssetObject
{
    /// <summary>Gets or sets the explicit resource key.</summary>
    [SerializableProperty] public string resourceId { get; set; } = string.Empty;
    /// <summary>Gets or sets the displayed sound title.</summary>
    [SerializableProperty] public string title { get; set; } = string.Empty;
    /// <summary>Gets or sets the credited composer or source.</summary>
    [SerializableProperty] public string author { get; set; } = string.Empty;
    /// <summary>Gets or sets the imported clip, null when unassigned.</summary>
    [SerializableProperty] public AudioClipAsset? clip { get; set; }
    /// <summary>Gets or sets the semantic mixing category.</summary>
    [SerializableProperty] public NovelAudioChannel channel { get; set; } = NovelAudioChannel.Sfx;
    /// <summary>Gets or sets authored gain in the zero-to-one interval.</summary>
    [SerializableProperty] public double volume { get; set; } = 1;
    /// <summary>Gets or sets looping behavior.</summary>
    [SerializableProperty] public bool loop { get; set; }
    /// <summary>Gets or sets fade-in duration in seconds.</summary>
    [SerializableProperty] public double fadeIn { get; set; }
    /// <summary>Gets or sets replacement and stop fade duration in seconds.</summary>
    [SerializableProperty] public double fadeOut { get; set; } = 0.15;
}
