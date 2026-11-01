namespace Inno.VisualNovel.Model.Interface.Animation;

/// <summary>Identifies a pure presentation event available in the screen designer.</summary>
public enum UiAnimationEvent
{
    /// <summary>Travels from the hidden pose to the ordinary pose.</summary>
    Enter,
    /// <summary>Travels from the ordinary pose to the hidden pose.</summary>
    Exit,
    /// <summary>Travels to the pointer-hover pose.</summary>
    Hover,
    /// <summary>Travels to the keyboard-focus pose.</summary>
    Focus,
    /// <summary>Travels to the held-pointer pose.</summary>
    Pressed,
    /// <summary>Reverses continuously between ordinary and waiting poses.</summary>
    Waiting
}
