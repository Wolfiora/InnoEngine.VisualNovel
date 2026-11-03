using InnoEngine.Scene;
using InnoEngine.Serialization;
using InnoEngine.Reflection;
using InnoEngine.Rendering;
using InnoEngine.Mathematics;
using Inno.VisualNovel.Model.Stages;

namespace Inno.VisualNovel.Integration.Components;

/// <summary>Draws one scene-owned image or solid rectangle using its native transform and hierarchy.</summary>
[StableTypeId("b0f32c52-b546-4259-9e9e-d4cd7ebc9c01")]
public sealed class NovelImageComponent : GameComponent
{
    /// <summary>Gets or sets whether this surface contributes to rendering and selection.</summary>
    [SerializableProperty] public bool visible { get; set; } = true;
    /// <summary>Gets or sets the imported image; null draws a solid rectangle.</summary>
    [SerializableProperty] public TextureAsset? texture { get; set; }
    /// <summary>Gets or sets the local width in design pixels before the native transform.</summary>
    [SerializableProperty] public float width { get; set; } = 320;
    /// <summary>Gets or sets the local height in design pixels before the native transform.</summary>
    [SerializableProperty] public float height { get; set; } = 180;
    /// <summary>Gets or sets the normalized pivot measured from the upper left.</summary>
    [SerializableProperty] public Vector2 pivot { get; set; } = new(.5f, .5f);
    /// <summary>Gets or sets the normalized source texture rectangle.</summary>
    [SerializableProperty] public Rect uv { get; set; } = new(0, 0, 1, 1);
    /// <summary>Gets or sets the normalized visible fraction of local geometry, independently of texture sampling.</summary>
    [SerializableProperty] public Rect crop { get; set; } = new(0, 0, 1, 1);
    /// <summary>Gets or sets the packed RRGGBBAA tint.</summary>
    [SerializableProperty] public uint color { get; set; } = 0xFFFFFFFF;
    /// <summary>Gets or sets the explicit paint band.</summary>
    [SerializableProperty] public StageLayerGroup group { get; set; }
    /// <summary>Gets or sets ordering within the paint band; hierarchy order resolves equal values.</summary>
    [SerializableProperty] public int order { get; set; }
    /// <summary>Gets or sets response to game camera translation; zero stays fixed, one follows the world. Editor navigation is unaffected.</summary>
    [SerializableProperty] public float parallax { get; set; } = 1;
    /// <summary>Gets or sets the linear-light blend operation.</summary>
    [SerializableProperty] public StageBlendMode blend { get; set; }
    /// <summary>Gets or sets the optional native mask texture.</summary>
    [SerializableProperty] public TextureAsset? mask { get; set; }
    /// <summary>Gets or sets how mask samples become coverage.</summary>
    [SerializableProperty] public StageMaskMode maskMode { get; set; }
}
