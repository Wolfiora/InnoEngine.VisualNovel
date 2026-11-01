using InnoEngine.Serialization;

namespace Inno.VisualNovel.Model.Performance;

/// <summary>Identifies interpolation from a keyframe to its successor.</summary>
public enum PerformanceInterpolation
{
    /// <summary>Interpolates uniformly.</summary>
    Linear,
    /// <summary>Holds the previous value.</summary>
    Hold,
    /// <summary>Uses a smooth cubic ease with zero endpoint slopes.</summary>
    Smooth,
    /// <summary>Accelerates quadratically from rest.</summary>
    EaseIn,
    /// <summary>Decelerates quadratically to rest.</summary>
    EaseOut,
    /// <summary>Accelerates and decelerates symmetrically.</summary>
    EaseInOut,
    /// <summary>Uses the keyframe's editable normalized cubic timing handles.</summary>
    CubicBezier
}
