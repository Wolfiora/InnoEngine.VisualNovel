using Inno.VisualNovel.Player.Ports;
using Inno.VisualNovel.Player.Interface.Actions;
using Inno.VisualNovel.Player.Interface.Data;

namespace Inno.VisualNovel.Player.Sessions;

/// <summary>Configures owner-local runtime policy independently of authored content and saved game state.</summary>
public sealed class NovelSessionOptions
{
    /// <summary>Gets the host's native state codec used to retain rollback as bounded native buffers; null retains count-bounded neutral objects only.</summary>
    public INovelStateCodec? checkpointCodec { get; init; }
    /// <summary>Gets the maximum retained native checkpoint payload bytes, defaulting to 64 MiB; zero disables retention.</summary>
    public long rollbackByteBudget { get; init; } = 64L * 1024 * 1024;
    /// <summary>Gets explicit custom UI action factories, frozen with the session before its save identity is computed.</summary>
    public NovelUiActionRegistry? uiActions { get; init; }
    /// <summary>Gets explicit concrete UI source factories, frozen with the session before any view is constructed.</summary>
    public NovelUiDataSourceRegistry? uiDataSources { get; init; }
    /// <summary>Gets an optional host build contribution for renderer, integration and external bridge dependencies outside the core runtime.</summary>
    public string hostCodeFingerprint { get; init; } = string.Empty;
    /// <summary>Gets immutable pure extension-channel declarations used by ordinary stage timelines and scene-owned consumers.</summary>
    public Performance.NovelPerformanceTargets? performanceTargets { get; init; }
}
