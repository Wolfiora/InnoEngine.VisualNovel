using System;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Stage.Characters;

/// <summary>Stores a resolved frame image identity and deterministic hold duration.</summary>
[GenerateSerializationConverter]
public sealed class CharacterFrameState : ISerializable
{
    /// <summary>Gets or sets the persistent imported image identity.</summary>
    [SerializableProperty] public Guid texture { get; set; }
    /// <summary>Gets or sets the positive logical frame duration.</summary>
    [SerializableProperty] public double seconds { get; set; }
}
