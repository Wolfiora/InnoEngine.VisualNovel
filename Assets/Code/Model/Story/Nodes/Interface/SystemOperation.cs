namespace Inno.VisualNovel.Model.Story.Nodes.Interface;

/// <summary>Identifies implemented no-code session presentation actions.</summary>
public enum SystemOperation
{
    /// <summary>Opens an authored page and suspends narrative before executing the next instruction.</summary>
    OpenPage,
    /// <summary>Changes the active declared theme without replacing narrative state.</summary>
    SetTheme,
    /// <summary>Returns to the title and suspends the current narrative continuation.</summary>
    ReturnTitle,
    /// <summary>Creates an explicitly named restorable narrative boundary before the next instruction.</summary>
    Checkpoint,
    /// <summary>Requests an automatic save of the exact post-action continuation.</summary>
    AutoSave,
    /// <summary>Prevents rollback across an author-declared irreversible boundary.</summary>
    RollbackBarrier
}
