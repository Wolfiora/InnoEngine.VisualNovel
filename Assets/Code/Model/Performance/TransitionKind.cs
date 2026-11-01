namespace Inno.VisualNovel.Model.Performance;

/// <summary>Identifies a composition-transition mechanism.</summary>
public enum TransitionKind
{
    /// <summary>Passes through an authored intermediate color.</summary>
    Fade = 0,
    /// <summary>Interpolates complete premultiplied images.</summary>
    Dissolve = 1,
    /// <summary>Reveals the new composition along a directional edge.</summary>
    Wipe = 2,
    /// <summary>Reveals using a deterministic procedural threshold mask.</summary>
    Mask = 3,
    /// <summary>Moves the old and new images across the viewport.</summary>
    Slide = 4,
    /// <summary>Reveals inside or outside an expanding circular aperture.</summary>
    Iris = 5
}
