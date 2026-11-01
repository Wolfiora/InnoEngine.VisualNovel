using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Performance;

namespace Inno.VisualNovel.Stage.Timeline;

/// <summary>Captures a clip's logical playback position.</summary>
[GenerateSerializationConverter]
public sealed class PerformanceState : ISerializable
{
    /// <summary>Gets or sets elapsed logical seconds.</summary>
    [SerializableProperty]
    public double elapsed { get; set; }
    /// <summary>Gets or sets the pause state.</summary>
    [SerializableProperty]
    public bool paused { get; set; }
}
