using System;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Stages;

/// <summary>Declares normalized texture sampling, nondestructive local cropping and composition policy.</summary>
[GenerateSerializationConverter]
public sealed class StageSurface : ISerializable
{
    /// <summary>Gets or sets the left edge of the texture source region.</summary>
    [SerializableProperty] public double sourceX { get; set; }
    /// <summary>Gets or sets the top edge of the texture source region.</summary>
    [SerializableProperty] public double sourceY { get; set; }
    /// <summary>Gets or sets the normalized texture source width.</summary>
    [SerializableProperty] public double sourceWidth { get; set; } = 1;
    /// <summary>Gets or sets the normalized texture source height.</summary>
    [SerializableProperty] public double sourceHeight { get; set; } = 1;
    /// <summary>Gets or sets the retained left edge relative to the full local geometry.</summary>
    [SerializableProperty] public double cropX { get; set; }
    /// <summary>Gets or sets the retained top edge relative to the full local geometry.</summary>
    [SerializableProperty] public double cropY { get; set; }
    /// <summary>Gets or sets the retained fraction of local width; zero hides the image.</summary>
    [SerializableProperty] public double cropWidth { get; set; } = 1;
    /// <summary>Gets or sets the retained fraction of local height; zero hides the image.</summary>
    [SerializableProperty] public double cropHeight { get; set; } = 1;
    /// <summary>Gets or sets composition against the already painted stage backdrop.</summary>
    [SerializableProperty] public StageBlendMode blend { get; set; }
    /// <summary>Gets or sets coverage interpretation when the owner assigns a mask image.</summary>
    [SerializableProperty] public StageMaskMode maskMode { get; set; }

    /// <summary>Creates an independent surface declaration without retaining mutable source values.</summary>
    /// <returns>The detached composition policy.</returns>
    public StageSurface Copy() => new()
    {
        sourceX = sourceX, sourceY = sourceY, sourceWidth = sourceWidth, sourceHeight = sourceHeight,
        cropX = cropX, cropY = cropY, cropWidth = cropWidth, cropHeight = cropHeight, blend = blend, maskMode = maskMode
    };

    /// <summary>Rejects nonfinite, out-of-image or unknown surface declarations.</summary>
    public void Validate()
    {
        if (!Region(sourceX, sourceWidth) || !Region(sourceY, sourceHeight) || !Region(cropX, cropWidth) || !Region(cropY, cropHeight)
            || !Enum.IsDefined(blend) || !Enum.IsDefined(maskMode))
            throw new InvalidOperationException("Stage source/crop regions must stay within 0–1 and use supported blend/mask modes.");
    }

    private static bool Region(
        double start,
        double length
    ) => double.IsFinite(start) && double.IsFinite(length) && start >= 0 && length >= 0 && start + length <= 1;
}
