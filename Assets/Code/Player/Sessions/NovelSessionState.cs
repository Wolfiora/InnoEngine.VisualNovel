using InnoEngine.Serialization;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Player.Persistence;
using Inno.VisualNovel.Stage.Composition;
using Inno.VisualNovel.Player.Audio;
using Inno.VisualNovel.Player.Interface;

namespace Inno.VisualNovel.Player.Sessions;

/// <summary>Contains restorable game state without device handles, live tasks or global progress.</summary>
[GenerateSerializationConverter]
public sealed class NovelSessionState : ISerializable
{
    /// <summary>Gets or sets the stable game identity.</summary>
    [SerializableProperty] public string gameId { get; set; } = string.Empty;
    /// <summary>Gets or sets the exact content, runtime implementation and explicit host build fingerprint.</summary>
    [SerializableProperty] public string buildFingerprint { get; set; } = string.Empty;
    /// <summary>Gets or sets the declared presentation theme at this exact narrative boundary.</summary>
    [SerializableProperty] public string themeId { get; set; } = string.Empty;
    /// <summary>Gets or sets the exact snapshot presentation language used for capture, without restoring player language preferences.</summary>
    [SerializableProperty] public string presentationLanguage { get; set; } = string.Empty;
    /// <summary>Gets or sets explicitly game-persistent page fields; transient controls and Local fields are excluded.</summary>
    [SerializableProperty] public UiPageStateSnapshot[] pageState { get; set; } = [];
    /// <summary>Gets or sets the complete narrative checkpoint.</summary>
    [SerializableProperty] public StoryExecutionState narrative { get; set; } = new();
    /// <summary>Gets or sets the flattened presentation checkpoint.</summary>
    [SerializableProperty] public StageState stage { get; set; } = new();
    /// <summary>Gets or sets the previous UI image associated with an active output transition.</summary>
    [SerializableProperty] public TransitionUiState transitionUi { get; set; } = new();
    /// <summary>Gets or sets voice and in-progress fade state.</summary>
    [SerializableProperty] public AudioPlaybackSnapshot audio { get; set; } = new();
    /// <summary>Gets or sets the active reading history.</summary>
    [SerializableProperty] public ReadingRecord[] history { get; set; } = [];
    /// <summary>Gets or sets active play time, in seconds.</summary>
    [SerializableProperty] public double playTime { get; set; }
}
