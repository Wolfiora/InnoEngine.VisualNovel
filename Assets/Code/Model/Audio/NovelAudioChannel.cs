namespace Inno.VisualNovel.Model.Audio;

/// <summary>Defines independent per-session audio mixing categories.</summary>
public enum NovelAudioChannel
{
    /// <summary>Background music, affected by voice ducking.</summary>
    Bgm,
    /// <summary>Looping environmental sound.</summary>
    Ambience,
    /// <summary>One-shot stage sound effects.</summary>
    Sfx,
    /// <summary>Spoken dialogue.</summary>
    Voice,
    /// <summary>Interface feedback.</summary>
    Ui
}
