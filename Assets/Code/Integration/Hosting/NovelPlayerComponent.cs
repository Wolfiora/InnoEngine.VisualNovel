using System;
using InnoEngine.Core;
using InnoEngine.Reflection;
using InnoEngine.Rendering;
using InnoEngine.Scene;
using InnoEngine.Serialization;
using Inno.VisualNovel.Integration.Services;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Player.Shell;
using Inno.VisualNovel.Rendering.Frames;
using Inno.VisualNovel.UI.Layout;
using InnoEngine.Storage;
using Inno.VisualNovel.Player.Ports;
using Inno.VisualNovel.Player.Persistence;
using Inno.VisualNovel.Player.Audio;
using Inno.VisualNovel.Narrative.Extensions;
using Inno.VisualNovel.Model.Extensions;

namespace Inno.VisualNovel.Integration.Hosting;

/// <summary>Owns one game's session and UI lifecycle in an engine scene.</summary>
[StableTypeId("fd0c7316-14a5-4d89-8492-bfd0a0b9f130")]
public sealed class NovelPlayerComponent : GameBehavior
{
    private NovelProjectAsset? m_project;
    private NovelSession? m_session;
    private NovelGameView? m_view;
    private readonly NovelFrameComposer m_composer = new();
    private readonly InnoNovelInput m_input = new();
    private ulong m_inputFrame = ulong.MaxValue;
    private IApplicationStorage? m_storage;
    private INovelStateCodec? m_stateCodec;
    private int m_rollbackBudgetMiB = 64;
    /// <summary>Gets or sets the native rollback payload budget in MiB; zero disables retention and an unavailable codec keeps explicit unknown byte accounting.</summary>
    [SerializableProperty]
    public int rollbackBudgetMiB
    {
        get => m_rollbackBudgetMiB;
        set
        {
            if (value is < 0 or > 1024) throw new ArgumentOutOfRangeException(nameof(value), "Rollback budget must be between zero and 1024 MiB.");
            m_session?.SetRollbackBudget((long)value * 1024 * 1024); m_rollbackBudgetMiB = value;
        }
    }
    private NovelCommandRegistry? m_commands;
    private string m_hostCodeFingerprint = string.Empty;
    private Player.Performance.NovelPerformanceTargets? m_performanceTargets;
    /// <summary>Binds immutable extension timeline channels before this native scene component creates its session.</summary>
    /// <param name="targets">Pure channel declarations; the bridge owner reads values and contributes its own code identity separately.</param>
    public void ConfigurePerformanceTargets(Player.Performance.NovelPerformanceTargets targets)
    {
        if (m_session is not null) throw new InvalidOperationException("Configure performance targets before creating the game session.");
        m_performanceTargets = targets ?? throw new ArgumentNullException(nameof(targets));
    }
    /// <summary>Includes explicitly known bridge or host dependencies in exact-build saves before this scene component starts its game.</summary>
    /// <param name="fingerprint">A bounded build contribution computed by the bridge owner, not live mutable gameplay state.</param>
    public void ConfigureHostCodeFingerprint(string fingerprint)
    {
        if (m_session is not null) throw new InvalidOperationException("Configure host code identity before creating the game session.");
        ArgumentNullException.ThrowIfNull(fingerprint);
        if (fingerprint.Length > 4096) throw new ArgumentOutOfRangeException(nameof(fingerprint));
        m_hostCodeFingerprint = fingerprint;
    }
    private Inno.VisualNovel.Player.Interface.Actions.NovelUiActionRegistry? m_uiActions;
    private Inno.VisualNovel.Player.Interface.Data.NovelUiDataSourceRegistry? m_uiDataSources;
    /// <summary>Binds typed source factories before the native component creates its owned view.</summary>
    /// <param name="sources">The explicit current-generation data source registry.</param>
    public void ConfigureUiDataSources(Inno.VisualNovel.Player.Interface.Data.NovelUiDataSourceRegistry sources)
    {
        if (m_session is not null) throw new InvalidOperationException("Configure UI data sources before creating the game session.");
        m_uiDataSources = (sources ?? throw new ArgumentNullException(nameof(sources))).Snapshot();
    }
    /// <summary>Binds explicit custom UI action factories before the component creates its scene-owned view.</summary>
    /// <param name="actions">The current generation's registry, copied into a frozen registration snapshot.</param>
    public void ConfigureUiActions(Inno.VisualNovel.Player.Interface.Actions.NovelUiActionRegistry actions)
    {
        if (m_session is not null) throw new InvalidOperationException("Configure UI actions before creating the game session.");
        m_uiActions = (actions ?? throw new ArgumentNullException(nameof(actions))).Snapshot();
    }
    private Inno.VisualNovel.Rendering.Capture.NovelScreenshotService? m_screenshots;
    internal Inno.VisualNovel.Rendering.Capture.NovelCaptureRequest[] DrainCaptures() => m_screenshots?.Drain() ?? [];
    /// <summary>Binds explicit current-generation command factories before session creation.</summary>
    /// <param name="commands">The host-owned registry; the session freezes a defensive snapshot.</param>
    public void ConfigureCommands(NovelCommandRegistry commands)
    {
        if (m_session is not null) throw new InvalidOperationException("Configure commands before creating the game session.");
        m_commands = commands.Snapshot();
    }

    /// <summary>Gets or sets the imported game definition, stopping the old session when the binding changes.</summary>
    [SerializableProperty]
    public NovelProjectAsset? project
    {
        get => m_project;
        set
        {
            if (ReferenceEquals(m_project, value)) return;
            StopGame(); m_project = value;
        }
    }
    /// <summary>Gets the isolated session owned by this active component in its loaded engine scene.</summary>
    /// <exception cref="InvalidOperationException">The component is inactive, detached, destroyed or outside a loaded scene.</exception>
    public NovelSession session
    {
        get { EnsureSession(); return m_session!; }
    }

    /// <summary>Binds host-owned native persistence before starting this component's session.</summary>
    /// <param name="storage">The application's atomic storage sandbox.</param>
    /// <param name="codec">A generation-owned engine-native codec; no alternate data format is accepted.</param>
    public void ConfigurePersistence(
        IApplicationStorage storage,
        INovelStateCodec codec
    ) {
        if (m_session is not null) throw new InvalidOperationException("Configure persistence before creating the game session.");
        m_storage = storage ?? throw new ArgumentNullException(nameof(storage));
        m_stateCodec = codec ?? throw new ArgumentNullException(nameof(codec));
    }

    /// <summary>Prepares the actual game frame and consumes each host input frame once.</summary>
    /// <param name="output">The frame-local output context.</param>
    /// <returns>Detached VN composition commands.</returns>
    public NovelRenderFrame Prepare(RenderOutputSession output)
    {
        EnsureSession();
        NovelProjectAsset game = project!;
        if (m_inputFrame != output.frameIndex)
        {
            m_inputFrame = output.frameIndex;
            var page = m_view!.BuildFrame();
            var fit = UiCanvasFit.Create(page.width, page.height, new(0, 0, game.width, game.height));
            m_view.HandleInput(m_input.Map(output.input, output.viewport, game.width, game.height, m_view.activeSession.preferences).InCanvas(fit));
        }
        return m_composer.Compose(m_view!.BuildFrame(), game.material ?? throw new InvalidOperationException("The game has no VN composition material."), m_view.activeSession.BuildStageFrame(game.width, game.height), game.width, game.height, game.effectsMaterial, m_view.activeSession.transitionUi);
    }

    /// <summary>Stops the component-owned session and drops generation-local presentation state.</summary>
    public void StopGame()
    {
        m_view?.Dispose();
        m_session?.Dispose();
        m_session = null; m_view = null; m_inputFrame = ulong.MaxValue;
        m_composer.Clear();
        m_screenshots?.Dispose(); m_screenshots = null;
    }

    /// <inheritdoc />
    protected override void Update() { EnsureSession(); m_view!.Tick(Time.deltaTime); }
    /// <inheritdoc />
    protected override void OnDisable() => StopGame();
    /// <inheritdoc />
    protected override void OnDestroy() => StopGame();

    private void EnsureSession()
    {
        if (!isActiveAndEnabled || !gameObject.scene.isLoaded)
            throw new InvalidOperationException("A Visual Novel session requires an active component attached to a loaded engine scene.");
        if (m_session is not null) return;
        NovelProjectAsset game = project ?? throw new InvalidOperationException("Assign a Visual Novel project to this component.");
        var candidate = new NovelSession(game, new InnoNovelAudio(NovelAudioLibrary.Create(game)), commands: m_commands,
            options: new() { checkpointCodec = m_stateCodec, rollbackByteBudget = (long)m_rollbackBudgetMiB * 1024 * 1024,
                uiActions = m_uiActions, uiDataSources = m_uiDataSources, performanceTargets = m_performanceTargets,
                hostCodeFingerprint = NovelImplementationFingerprint.Combine([NovelImplementationFingerprint.TypeCode(typeof(NovelPlayerComponent)),
                    NovelImplementationFingerprint.TypeCode(typeof(NovelFrameComposer)), "bridge/" + m_hostCodeFingerprint]) });
        var screenshots = new Inno.VisualNovel.Rendering.Capture.NovelScreenshotService();
        try
        {
            var saves = m_storage is not null && m_stateCodec is not null ? new SaveService(m_storage, m_stateCodec, candidate.gameId, candidate.buildFingerprint, game.manualSaveSlots, game.automaticSaveSlots) : null;
            var profile = m_storage is not null && m_stateCodec is not null ? new GlobalProgressStore(candidate.gameId, m_storage, m_stateCodec) : null;
            var thumbnail = new InnoNovelSaveThumbnail(screenshots, PrepareSaveImage);
            var view = new NovelGameView(game, candidate, NovelFrameComposer.Measure, saves, () => new InnoNovelAudio(NovelAudioLibrary.Create(game)), NovelFrameComposer.MeasureRichText, profile, thumbnail);
            m_session = candidate; m_view = view; m_screenshots = screenshots;
        }
        catch { screenshots.Dispose(); candidate.Dispose(); throw; }
    }

    private NovelRenderFrame PrepareSaveImage(NovelSessionState state)
    {
        NovelProjectAsset game = project!;
        return m_composer.Compose(m_view!.CaptureReadingFrame(), game.material ?? throw new InvalidOperationException("Save capture requires the VN surface material."),
            m_session!.localization.ForLanguage(state.presentationLanguage).resources.Stage(m_session.stage.Fork(state.stage).BuildFrame(game.width, game.height)), game.width, game.height, game.effectsMaterial, m_session.transitionUi);
    }
}
