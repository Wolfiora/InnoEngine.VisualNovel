using System;
using System.Collections.Generic;
using InnoEditor.Assets;
using Inno.VisualNovel.Model.Audio;

namespace Inno.VisualNovel.Authoring.AssetImporting;

/// <summary>Validates imported clip references and sound policy before publishing a native cue.</summary>
[AssetImporter("inno.vn.audio")]
public sealed class AudioCueImporter : NativeNovelAssetImporter<AudioCueAsset>
{
    /// <inheritdoc />
    public override IReadOnlyList<string> supportedExtensions { get; } = [".ivnaudio"];
    /// <inheritdoc />
    protected override void Validate(AudioCueAsset asset)
    {
        if (string.IsNullOrWhiteSpace(asset.resourceId) || asset.clip is null || asset.clip.isMissing || asset.clip.duration <= TimeSpan.Zero
            || !Enum.IsDefined(asset.channel) || !double.IsFinite(asset.volume) || asset.volume is < 0 or > 1
            || !double.IsFinite(asset.fadeIn) || asset.fadeIn < 0 || !double.IsFinite(asset.fadeOut) || asset.fadeOut < 0)
            throw new InvalidOperationException("An audio cue requires an imported nonempty clip, a unique resource key and finite playback parameters.");
    }
}
