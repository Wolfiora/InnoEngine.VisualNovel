namespace Inno.VisualNovel.Authoring.BuiltInResources;

/// <summary>Groups immutable packaged presets by their implemented editing and preview workflow.</summary>
public enum BuiltInResourceKind
{
    /// <summary>Complete editable game interface families.</summary>
    Theme,
    /// <summary>Native scenery with original background images.</summary>
    Stage,
    /// <summary>Layered teaching actors.</summary>
    Character,
    /// <summary>Reusable pure stage timelines.</summary>
    Motion,
    /// <summary>Deterministic emitters with original sprites.</summary>
    Particle,
    /// <summary>Image transition boundaries.</summary>
    Transition,
    /// <summary>Parameterized image operations.</summary>
    ScreenEffect,
    /// <summary>Native reusable audio cues.</summary>
    Audio,
    /// <summary>Monochrome interface image resources.</summary>
    Icon,
    /// <summary>Reusable editable native UI subtrees.</summary>
    Widget
}
