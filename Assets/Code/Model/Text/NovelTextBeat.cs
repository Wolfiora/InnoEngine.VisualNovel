namespace Inno.VisualNovel.Model.Text;

/// <summary>Declares a pacing change at a UTF-16 plain-text boundary.</summary>
/// <param name="offset">The boundary preceding the affected text.</param>
/// <param name="pause">A logical pause before the following grapheme.</param>
/// <param name="speed">A positive reveal-rate multiplier.</param>
public sealed record NovelTextBeat(int offset, double pause, double speed);
