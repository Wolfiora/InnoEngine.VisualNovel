using InnoEngine.Mathematics;
using Inno.VisualNovel.Model.Stages;

namespace Inno.VisualNovel.Authoring.StageEditing;

/// <summary>Describes one explicit relative transform and optional appearance overrides for a reviewed set of stage layers.</summary>
public sealed class StageLayerBatchChange
{
    /// <summary>Gets the design-space translation added to every selected local placement.</summary>
    public Vector2 offset { get; init; }
    /// <summary>Gets the positive scale multiplier, preserving each layer's existing mirror signs and pivot.</summary>
    public double scale { get; init; } = 1;
    /// <summary>Gets the clockwise angle added to each selected transform.</summary>
    public double rotation { get; init; }
    /// <summary>Gets an explicit alpha override, or null to retain individual values.</summary>
    public double? opacity { get; init; }
    /// <summary>Gets an explicit visibility override, or null to retain individual values.</summary>
    public bool? visible { get; init; }
    /// <summary>Gets an optional packed RRGGBBAA tint override.</summary>
    public uint? color { get; init; }
    /// <summary>Gets an optional paint-band override without changing order within the band.</summary>
    public StageLayerGroup? group { get; init; }
}
