using InnoEngine.Reflection;
using InnoEngine.Scene;
using InnoEngine.Serialization;
using InnoEngine.Text;

namespace Inno.VisualNovel.Integration.Components.Interface;

/// <summary>Defines a scene-owned UI canvas; its actual descendant objects provide all controls.</summary>
[StableTypeId("b0f32c52-b546-4259-9e9e-d4cd7ebc9c20")]
public sealed class NovelUiRootComponent : GameComponent
{
    /// <summary>Gets or sets the engine UI template supplying the document language, fonts and base style.</summary>
    [SerializableProperty] public InnoEngine.UI.UiDocumentAsset? documentTemplate { get; set; }
    /// <summary>Gets or sets the template element replaced by the projection of actual scene children.</summary>
    [SerializableProperty] public string contentElement { get; set; } = "vn-scene";
    /// <summary>Gets or sets the logical canvas width; its center is the root's native origin.</summary>
    [SerializableProperty] public float width { get; set; } = 1920;
    /// <summary>Gets or sets the logical canvas height.</summary>
    [SerializableProperty] public float height { get; set; } = 1080;
    /// <summary>Gets or sets the inherited native font, overridable on individual text components.</summary>
    [SerializableProperty] public FontAsset? font { get; set; }
    /// <summary>Gets or sets the UI paint band; native sibling order resolves equal values.</summary>
    [SerializableProperty] public int order { get; set; }
}
