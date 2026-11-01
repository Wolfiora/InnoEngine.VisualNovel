using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Player.Audio;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Stage.Composition;
using Inno.VisualNovel.Player.Interface;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Player.Persistence;

/// <summary>Stores one closed native rollback payload without duplicating the complete reading history.</summary>
[GenerateSerializationConverter]
public sealed class RollbackCheckpoint : ISerializable
{
    /// <summary>Creates an empty contract for native restoration.</summary>
    public RollbackCheckpoint() { }
    internal RollbackCheckpoint(
        StoryExecutionState state,
        StageState stage,
        AudioPlaybackSnapshot audio,
        int history,
        TransitionUiState ui,
        UiPageStateSnapshot[] pageState,
        string theme,
        string name = "",
        string language = ""
    ) {
        this.state = state; this.stage = stage; this.audio = audio; this.history = history;
        this.ui = ui; this.pageState = pageState; this.theme = theme; this.name = name;
        this.language = language;
    }
    /// <summary>Gets or sets the complete narrative task tree.</summary>
    [SerializableProperty] public StoryExecutionState state { get; set; } = new();
    /// <summary>Gets or sets declarative stage and performance state.</summary>
    [SerializableProperty] public StageState stage { get; set; } = new();
    /// <summary>Gets or sets active voices and fade clocks.</summary>
    [SerializableProperty] public AudioPlaybackSnapshot audio { get; set; } = new();
    /// <summary>Gets or sets the retained history prefix length, avoiding a second copy of historical records.</summary>
    [SerializableProperty] public int history { get; set; }
    /// <summary>Gets or sets the frozen UI of an active output transition.</summary>
    [SerializableProperty] public TransitionUiState ui { get; set; } = new();
    /// <summary>Gets or sets explicitly persistent page values.</summary>
    [SerializableProperty] public UiPageStateSnapshot[] pageState { get; set; } = [];
    /// <summary>Gets or sets the selected theme identity.</summary>
    [SerializableProperty] public string theme { get; set; } = string.Empty;
    /// <summary>Gets or sets the optional authored checkpoint name.</summary>
    [SerializableProperty] public string name { get; set; } = string.Empty;
    /// <summary>Gets or sets the presentation language at this boundary, independently of the parent task's last dialogue.</summary>
    [SerializableProperty] public string language { get; set; } = string.Empty;

    internal static StoryExecutionState? InteractiveBoundary(StoryExecutionState state)
    {
        if (state.status is StoryStatus.Dialogue or StoryStatus.Choice or StoryStatus.Input or StoryStatus.AdvanceWait) return state;
        if (state.status == StoryStatus.Parallel)
            foreach (var task in state.tasks) if (InteractiveBoundary(task.execution) is { } boundary) return boundary;
        return null;
    }
}
