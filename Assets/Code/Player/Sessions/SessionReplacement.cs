namespace Inno.VisualNovel.Player.Sessions;

/// <summary>Explains an owner revision change without retaining callbacks or publishing a second event bus.</summary>
public enum SessionReplacement
{
    /// <summary>No active game has yet been installed.</summary>
    None,
    /// <summary>A fresh explicit story entry was started.</summary>
    NewGame,
    /// <summary>A validated ordinary save replaced the active game.</summary>
    Load,
    /// <summary>An older interaction checkpoint replaced the abandoned future.</summary>
    Rollback
}
