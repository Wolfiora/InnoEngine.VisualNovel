using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Stages;

namespace Inno.VisualNovel.Model.Characters;

/// <summary>Declares one independently positioned image in a layered character.</summary>
[GenerateSerializationConverter]
public sealed class CharacterPart : ISerializable
{
    /// <summary>Gets or sets the stable part identity.</summary>
    [SerializableProperty] public string id { get; set; } = string.Empty;
    /// <summary>Gets or sets a human-readable pose-local label without changing the semantic part identity.</summary>
    [SerializableProperty] public string name { get; set; } = string.Empty;
    /// <summary>Gets or sets the character texture index.</summary>
    [SerializableProperty] public int texture { get; set; }
    /// <summary>Gets or sets image width.</summary>
    [SerializableProperty] public double width { get; set; } = 600;
    /// <summary>Gets or sets image height.</summary>
    [SerializableProperty] public double height { get; set; } = 1000;
    /// <summary>Gets or sets placement relative to the character origin.</summary>
    [SerializableProperty] public StageTransform transform { get; set; } = new();
    /// <summary>Gets or sets nondestructive source, crop and composition values shared by animation frames.</summary>
    [SerializableProperty] public StageSurface surface { get; set; } = new();
    /// <summary>Gets or sets a character-library mask texture index, or -1 for no mask.</summary>
    [SerializableProperty] public int mask { get; set; } = -1;
    /// <summary>Gets or sets paint order.</summary>
    [SerializableProperty] public int order { get; set; }
    /// <summary>Gets or sets initial visibility.</summary>
    [SerializableProperty] public bool visible { get; set; } = true;
    /// <summary>Gets or sets a nondestructive packed RRGGBBAA part tint.</summary>
    [SerializableProperty] public uint color { get; set; } = 0xFFFFFFFF;
    /// <summary>Gets or sets deterministic sequence, eye or mouth frame selection.</summary>
    [SerializableProperty] public CharacterPartAnimation animation { get; set; } = new();

    /// <summary>Creates an independent pose part with the same semantic identity and borrowed image indices.</summary>
    /// <returns>A detached part declaration.</returns>
    public CharacterPart Copy() => new()
    {
        id = id, name = name, texture = texture, width = width, height = height, transform = transform.Copy(), surface = surface.Copy(), mask = mask,
        order = order, visible = visible, color = color, animation = animation.Copy()
    };
}
