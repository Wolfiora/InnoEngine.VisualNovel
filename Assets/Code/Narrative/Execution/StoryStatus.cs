using InnoEngine.Serialization;
using Inno.VisualNovel.Narrative.Values;

namespace Inno.VisualNovel.Narrative.Execution;

/// <summary>Identifies the interpreter's observable lifecycle.</summary>
public enum StoryStatus
{
    /// <summary>No entry has been started.</summary>
    Stopped,
    /// <summary>Immediate instructions are executing.</summary>
    Running,
    /// <summary>Dialogue is waiting for the player.</summary>
    Dialogue,
    /// <summary>A choice owns player input.</summary>
    Choice,
    /// <summary>A logical timer is pending.</summary>
    Timer,
    /// <summary>A declarative presentation request awaits the session coordinator.</summary>
    Presentation,
    /// <summary>The entry completed normally.</summary>
    Completed,
    /// <summary>An explicit error stopped execution.</summary>
    Faulted,
    /// <summary>A bounded player text input owns interaction.</summary>
    Input,
    /// <summary>A typed extension owns a neutral pending continuation.</summary>
    Command,
    /// <summary>Bounded parallel presentation tasks are pending.</summary>
    Parallel,
    /// <summary>An applied session action suspended execution before the next instruction.</summary>
    Suspended,
    /// <summary>An explicit player advance is required without revealing or recording a dialogue line.</summary>
    AdvanceWait
}
