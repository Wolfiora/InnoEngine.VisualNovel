namespace Inno.VisualNovel.Model.Performance;

/// <summary>Identifies the concrete payload owned by one native visual-effect preset.</summary>
public enum VisualEffectKind
{
    /// <summary>A deterministic particle emitter.</summary>
    Particles,
    /// <summary>A composited screen image effect.</summary>
    Screen,
    /// <summary>A transition between two explicitly captured compositions.</summary>
    Transition
}
