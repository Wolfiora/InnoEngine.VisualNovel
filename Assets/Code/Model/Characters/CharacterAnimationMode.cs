namespace Inno.VisualNovel.Model.Characters;

/// <summary>Declares how a character part selects authored frame images.</summary>
public enum CharacterAnimationMode
{
    /// <summary>Keeps the pose or expression image.</summary>
    None,
    /// <summary>Plays the ordered frame sequence once or repeatedly.</summary>
    Sequence,
    /// <summary>Plays a short blink sequence after each declared idle interval.</summary>
    Blink,
    /// <summary>Repeats authored mouth frames only between explicit speaking markers.</summary>
    Talking
}
