using System;
using System.Collections.Generic;
using InnoEditor.Assets;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Stage.Effects;

namespace Inno.VisualNovel.Authoring.AssetImporting;

/// <summary>Publishes native visual-effect declarations after validating their implemented semantics.</summary>
[AssetImporter("inno.vn.effect")]
public sealed class VisualEffectImporter : NativeNovelAssetImporter<VisualEffectAsset>
{
    /// <inheritdoc />
    public override IReadOnlyList<string> supportedExtensions { get; } = [".ivneffect"];
    /// <inheritdoc />
    protected override void Validate(VisualEffectAsset asset)
    {
        switch (asset.kind)
        {
            case VisualEffectKind.Particles: _ = new ParticleSimulator(asset); break;
            case VisualEffectKind.Screen: VisualEffectRules.Validate(asset.screen); break;
            case VisualEffectKind.Transition: VisualEffectRules.Validate(asset.transition); break;
            default: throw new InvalidOperationException("Visual effect kind is unavailable.");
        }
        if (string.IsNullOrWhiteSpace(asset.resourceId)) throw new InvalidOperationException("Visual effects require a stable resource identity.");
    }
}
