using System;
using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Stages;

namespace Inno.VisualNovel.Stage.Composition;

/// <summary>Stores the neutral, flattened state of one stage image or solid.</summary>
[GenerateSerializationConverter]
public sealed class StageLayerState : ISerializable
{
    /// <summary>Gets or sets the stable instance identity.</summary>
    [SerializableProperty] public string id { get; set; } = string.Empty;
    /// <summary>Gets or sets an imported texture identity, or empty for a solid.</summary>
    [SerializableProperty] public Guid textureId { get; set; }
    /// <summary>Gets or sets the imported mask identity, or empty for no mask.</summary>
    [SerializableProperty] public Guid maskTextureId { get; set; }
    /// <summary>Gets or sets the detached source, crop and composition policy.</summary>
    [SerializableProperty] public StageSurface surface { get; set; } = new();
    /// <summary>Gets or sets logical width.</summary>
    [SerializableProperty] public double width { get; set; }
    /// <summary>Gets or sets logical height.</summary>
    [SerializableProperty] public double height { get; set; }
    /// <summary>Gets or sets affine transform components in row-vector order.</summary>
    [SerializableProperty] public double[] matrix { get; set; } = [1, 0, 0, 1, 0, 0];
    /// <summary>Gets or sets alpha.</summary>
    [SerializableProperty] public double opacity { get; set; } = 1;
    /// <summary>Gets or sets the stage-space pivot shared by an animated owner's parts.</summary>
    [SerializableProperty] public double pivotX { get; set; }
    /// <summary>Gets or sets the vertical stage-space animation pivot.</summary>
    [SerializableProperty] public double pivotY { get; set; }
    /// <summary>Gets or sets packed tint.</summary>
    [SerializableProperty] public uint color { get; set; } = 0xFFFFFFFF;
    /// <summary>Gets or sets the composition band.</summary>
    [SerializableProperty] public StageLayerGroup group { get; set; }
    /// <summary>Gets or sets ordering within the band.</summary>
    [SerializableProperty] public int order { get; set; }
    /// <summary>Gets or sets the stable owning instance, keeping character parts contiguous.</summary>
    [SerializableProperty] public string owner { get; set; } = string.Empty;
    /// <summary>Gets or sets ordering within one character instance.</summary>
    [SerializableProperty] public int partOrder { get; set; }
    /// <summary>Gets or sets camera parallax.</summary>
    [SerializableProperty] public double parallax { get; set; } = 1;
    /// <summary>Gets or sets visibility.</summary>
    [SerializableProperty] public bool visible { get; set; } = true;
}
