using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Stages;

/// <summary>Identifies the authored composition band.</summary>
public enum StageLayerGroup
{
    /// <summary>Background scenery.</summary>
    Background,
    /// <summary>Character layers.</summary>
    Characters,
    /// <summary>Objects in front of characters.</summary>
    Foreground,
    /// <summary>Stage overlays, below game UI.</summary>
    Overlay
}
