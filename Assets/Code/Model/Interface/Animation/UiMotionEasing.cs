namespace Inno.VisualNovel.Model.Interface.Animation;

/// <summary>Identifies bounded timing functions for reversible UI state transitions.</summary>
public enum UiMotionEasing
{
    /// <summary>Uses uniform progress.</summary>
    Linear,
    /// <summary>Uses the engine's smooth interpolation with stationary endpoints.</summary>
    Smooth,
    /// <summary>Accelerates quadratically.</summary>
    EaseIn,
    /// <summary>Decelerates quadratically.</summary>
    EaseOut
}
