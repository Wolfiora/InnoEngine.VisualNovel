using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Stages;

/// <summary>Declares one stage image or solid-color layer.</summary>
[GenerateSerializationConverter]
public sealed class StageLayer : ISerializable
{
    /// <summary>Gets or sets the stable stage-local identity.</summary>
    [SerializableProperty] public string id { get; set; } = string.Empty;
    /// <summary>Gets or sets the display name.</summary>
    [SerializableProperty] public string name { get; set; } = string.Empty;
    /// <summary>Gets or sets the source texture index, or -1 for a solid.</summary>
    [SerializableProperty] public int texture { get; set; } = -1;
    /// <summary>Gets or sets logical width.</summary>
    [SerializableProperty] public double width { get; set; } = 1920;
    /// <summary>Gets or sets logical height.</summary>
    [SerializableProperty] public double height { get; set; } = 1080;
    /// <summary>Gets or sets local placement.</summary>
    [SerializableProperty] public StageTransform transform { get; set; } = new();
    /// <summary>Gets or sets source sampling, crop and blend policy.</summary>
    [SerializableProperty] public StageSurface surface { get; set; } = new();
    /// <summary>Gets or sets a mask texture index, or -1 for unmasked coverage.</summary>
    [SerializableProperty] public int mask { get; set; } = -1;
    /// <summary>Gets or sets the paint band.</summary>
    [SerializableProperty] public StageLayerGroup group { get; set; }
    /// <summary>Gets or sets ordering within the band.</summary>
    [SerializableProperty] public int order { get; set; }
    /// <summary>Gets or sets packed RRGGBBAA tint.</summary>
    [SerializableProperty] public uint color { get; set; } = 0xFFFFFFFF;
    /// <summary>Gets or sets relative camera parallax, zero for fixed overlays.</summary>
    [SerializableProperty] public double parallax { get; set; } = 1;
    /// <summary>Gets or sets initial visibility.</summary>
    [SerializableProperty] public bool visible { get; set; } = true;
}
