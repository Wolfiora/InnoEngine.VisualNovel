using InnoEngine.Scene;
using InnoEngine.Serialization;
using InnoEngine.Reflection;
using Inno.VisualNovel.Model.Characters;

namespace Inno.VisualNovel.Integration.Components;

/// <summary>Binds one actual image object to a semantic character part and its sprite animation.</summary>
[StableTypeId("b0f32c52-b546-4259-9e9e-d4cd7ebc9c06")]
public sealed class NovelCharacterPartComponent : GameComponent
{
    /// <summary>Gets or sets the owning character in this object's native ancestor chain.</summary>
    [SerializableProperty] public NovelCharacterComponent? character { get; set; }
    /// <summary>Gets or sets the semantic part key used by character pose and expression definitions.</summary>
    [SerializableProperty] public string partId { get; set; } = string.Empty;
    /// <summary>Gets or sets sprite timing; frame indices address the owning character's imported texture library.</summary>
    [SerializableProperty] public CharacterPartAnimation animation { get; set; } = new();
}
