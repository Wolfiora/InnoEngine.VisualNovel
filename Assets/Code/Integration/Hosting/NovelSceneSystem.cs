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
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.UI.Input;
using InnoEngine.Input;

namespace Inno.VisualNovel.Integration.Hosting;

/// <summary>Owns one game's session and UI lifecycle in an engine scene.</summary>
[StableTypeId("b0f32c52-b546-4259-9e9e-d4cd7ebc9c10")]
public sealed class NovelSceneSystem : GameSystem
{
    /// <summary>Gets the native scene that owns this system and its renderable objects.</summary>
    public GameScene owningScene => scene;
    /// <summary>Gets or sets the surface material used when no game definition supplies one.</summary>
    [SerializableProperty] public MaterialAsset? surfaceMaterial { get; set; }
    /// <summary>Gets or sets the effect material used for scene image blends and masks.</summary>
    [SerializableProperty] public MaterialAsset? imageEffectsMaterial { get; set; }
    /// <summary>Gets or sets the explicit game camera; the editor view has independent navigation.</summary>
    [SerializableProperty] public Components.NovelCameraComponent? camera { get; set; }
    /// <summary>Gets or sets the design width for a scene without a game definition.</summary>
    [SerializableProperty] public float designWidth { get; set; } = 1920;
    /// <summary>Gets or sets the design height for a scene without a game definition.</summary>
    [SerializableProperty] public float designHeight { get; set; } = 1080;
    /// <summary>Gets or sets whether this scene may provide the game output in an explicit content scope.</summary>
    [SerializableProperty] public bool gameOutput { get; set; } = true;
    /// <summary>Gets or sets explicit scene-local story targets. Deleted references remain missing and are never repaired by name.</summary>
    [SerializableProperty] public Components.NovelTargetComponent[] targets { get; set; } = [];
    /// <summary>Resolves a real component through this system's explicit binding scope for a command or another plugin.</summary>
    /// <typeparam name="TComponent">The native capability required by the caller.</typeparam>
    /// <param name="targetId">The exact logical identity declared on a referenced target component.</param>
    /// <returns>The real bound component, including inactive targets.</returns>
    public TComponent ResolveTarget<TComponent>(string targetId) where TComponent : GameComponent
        => NovelSceneTargets.Resolve<TComponent>(this, targetId);
    /// <summary>Gets the material selected for this scene's output.</summary>
    public MaterialAsset? material => surfaceMaterial ?? project?.material;
    /// <summary>Gets the logical game output width.</summary>
    public float width => project?.width ?? designWidth;
    /// <summary>Gets the logical game output height.</summary>
    public float height => project?.height ?? designHeight;
    /// <summary>Captures the native scene without running story, input, audio or object lifecycle.</summary>
    /// <param name="projection">An editor projection, or null to use the declared game camera.</param>
    /// <returns>Detached native-object draw commands.</returns>
    public NovelRenderFrame CaptureScene(Rendering.NovelSceneProjection? projection = null)
    {
        if (isDestroyed || !enabled || !scene.isLoaded) throw new InvalidOperationException("The Visual Novel scene system is not available.");
        if (camera is not null && (camera.isDestroyed || !ReferenceEquals(camera.gameObject.scene, scene)))
            throw new InvalidOperationException("The Visual Novel camera is missing or belongs to another scene.");
        var view = projection ?? Rendering.NovelSceneProjection.Game(width, height, camera);
        var uiView = projection ?? new Rendering.NovelSceneProjection(width, height, InnoEngine.Mathematics.Matrix.identity);
        var ui = new Inno.VisualNovel.UI.Documents.UiFrame(view.width, view.height, []);
        return m_composer.Compose(ui, material ?? throw new InvalidOperationException("The VN default surface material is unavailable. Import the plugin resources or assign a surface material. " + m_presentationInitializationFailure),
            Rendering.NovelSceneCapture.Capture(scene, view), view.width, view.height, imageEffectsMaterial ?? project?.effectsMaterial,
            nativeUi: m_sceneUi.Capture(scene, uiView, m_view?.dataSource));
    }
    private readonly Interface.Engine.NovelSceneUiPresentation m_sceneUi = new();
    /// <summary>Routes a single game-output sample through native canvases, using engine hit testing and one explicit input owner.</summary>
    /// <param name="input">Engine input in this system's logical output pixels; pass each sample only once.</param>
    /// <param name="source">Optional host data for bindings; null uses the already-active session when available.</param>
    /// <returns>Consumption and actions for the owning gameplay bridge to dispatch. This method never starts a session.</returns>
    public UiInputResult HandleSceneInput(
        InnoEngine.UI.UiInputSnapshot input,
        Inno.VisualNovel.UI.Binding.UiDataSource? source = null
    ) {
        if (isDestroyed || !enabled || !scene.isLoaded) throw new InvalidOperationException("The Visual Novel scene system is not available.");
        ArgumentNullException.ThrowIfNull(input);
        return m_sceneUi.Handle(scene, new(width, height, InnoEngine.Mathematics.Matrix.identity), input, source ?? m_view?.dataSource);
    }
    /// <summary>Releases native canvas input after host focus loss and prevents an abandoned press from activating another canvas.</summary>
    public void ReleaseSceneInput() => m_sceneUi.ReleaseInput();
    /// <summary>Applies one action already consumed by a native control, without replaying its input through story navigation.</summary>
    /// <param name="action">A request returned by HandleSceneInput, retaining its actual source object's identity.</param>
    public void InvokeSceneAction(UiActionRequest action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (action.action is "scene.page" or "scene.list")
        {
            if (!Guid.TryParse(action.ownerId, out var sourceId)) throw new InvalidOperationException("The navigation action has no native source identity.");
            var source = scene.GetObjects().FirstOrDefault(item => item.identity.persistentId == sourceId);
            if (source is null) throw new InvalidOperationException("The navigation action's source object is missing.");
            if (action.action == "scene.page" && source.TryGetComponent<Components.Interface.NovelUiPageLinkComponent>(out var link) && link is not null)
            { link.Apply(); return; }
            if (action.action == "scene.list" && source.TryGetComponent<Components.Interface.NovelUiListNavigationComponent>(out var navigation) && navigation is not null)
            { navigation.Apply(); return; }
            throw new InvalidOperationException("The navigation action's component is missing.");
        }
        if (m_view is null) throw new InvalidOperationException("This action requires a running VN project: " + action.action);
        m_view.InvokeAction(action);
    }
    /// <summary>Uses the same engine UI documents as passive scene rendering to resolve editor object selection.</summary>
    /// <param name="projection">The editor's current view.</param>
    /// <param name="position">Pointer position in output pixels.</param>
    /// <returns>The actual UI object, or null outside engine UI elements.</returns>
    public GameObject? PickSceneUi(
        Rendering.NovelSceneProjection projection,
        InnoEngine.Mathematics.Vector2 position
    )
        => m_sceneUi.Pick(scene, projection, position, m_view?.dataSource);
    private string m_presentationInitializationFailure = string.Empty;
    /// <inheritdoc />
    protected override void Reset()
    {
        surfaceMaterial = null; imageEffectsMaterial = null;
        m_presentationInitializationFailure = string.Empty;
        try
        {
            InnoEngine.Assets.Assets.TryLoad<MaterialAsset>(InnoEngine.Assets.Assets.LocalPath("BuiltIn/Shaders/Materials/NovelSurface.imaterial"), out var surface);
            InnoEngine.Assets.Assets.TryLoad<MaterialAsset>(InnoEngine.Assets.Assets.LocalPath("BuiltIn/Shaders/Materials/NovelPostProcess.imaterial"), out var effects);
            surfaceMaterial = surface; imageEffectsMaterial = effects;
        }
        catch (InvalidOperationException failure)
        {
            // Detached tooling hosts may intentionally have no asset lookup. Capture reports the missing material if it is needed.
            m_presentationInitializationFailure = failure.Message;
        }
    }
    private bool m_runtimeEnabled;
    private bool m_stopped;
    private GameScene? m_runtimeScene;
    private NovelProjectAsset? m_project;
    private NovelSession? m_session;
    private NovelGameView? m_view;
    private readonly NovelFrameComposer m_composer = new();
    private InnoNovelInput m_input = new();
    private ulong m_inputFrame = ulong.MaxValue;
    private IApplicationStorage? m_storage;
    private INovelStateCodec? m_stateCodec;
    private NovelRenderFrame? m_preparedFrame;
    private readonly Queue<(UiInputSnapshot reading, InnoEngine.UI.UiInputSnapshot? scene)> m_pendingInput = new();
    /// <summary>Gets the last native canvas action failure, with its source object identity; empty means the last dispatch succeeded.</summary>
    public string sceneUiError { get; private set; } = string.Empty;
    private float m_canvasWidth;
    private float m_canvasHeight;
    private InputSnapshot m_runtimeInput = InputSnapshot.empty;
    private long m_keyboardFrame = -1;
    /// <summary>Gets whether the native update lifecycle has prepared an immutable presentation frame.</summary>
    public bool hasPresentation => m_preparedFrame is not null;
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
            StopGame(); m_project = value; m_stopped = false;
        }
    }
    /// <summary>Gets the isolated session owned by this active component in its loaded engine scene.</summary>
    /// <exception cref="InvalidOperationException">The component is inactive, detached, destroyed or outside a loaded scene.</exception>
    public NovelSession session
    {
        get { EnsureSession(); return m_session!; }
    }

    /// <summary>Gets whether a host has already supplied both native persistence services.</summary>
    public bool hasPersistence => m_storage is not null && m_stateCodec is not null;

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

    /// <summary>Queues viewport input once and returns the last update-owned immutable game frame.</summary>
    /// <param name="output">The frame-local output context.</param>
    /// <returns>Detached VN composition commands.</returns>
    public NovelRenderFrame Prepare(RenderOutputSession output)
    {
        NovelRenderFrame prepared = m_preparedFrame ?? throw new InvalidOperationException("The active native component has not prepared a game frame yet.");
        if (m_inputFrame != output.frameIndex)
        {
            m_inputFrame = output.frameIndex;
            var fit = UiCanvasFit.Create(m_canvasWidth, m_canvasHeight, new(0, 0, width, height));
            if (m_pendingInput.Count >= 128) throw new InvalidOperationException("Viewport input exceeded the active component's update budget.");
            bool newKeyboard = m_runtimeInput.frameIndex != m_keyboardFrame;
            m_keyboardFrame = m_runtimeInput.frameIndex;
            var input = new RenderOutputInput(output.input.pointerPosition, output.input.pointerInside, output.input.scrollDelta,
                m_runtimeInput.modifiers, newKeyboard ? m_runtimeInput.keysPressed : [], newKeyboard ? m_runtimeInput.keysReleased : [],
                output.input.buttonsPressed, output.input.buttonsReleased, newKeyboard ? m_runtimeInput.textInput : [], output.input.interactionEnabled);
            m_pendingInput.Enqueue((m_input.Map(input, output.viewport, width, height, m_view?.activeSession.preferences).InCanvas(fit),
                InnoNovelSceneInput.Map(input, output.viewport, width, height)));
        }
        return prepared;
    }

    /// <summary>Starts a new story in this active system; an explicit stop remains effective until this call or native reactivation.</summary>
    public void StartGame()
    {
        bool wasStopped = m_stopped;
        m_stopped = false;
        try { EnsureSession(); m_session!.StartNew(); }
        catch
        {
            if (wasStopped) StopGame();
            throw;
        }
    }

    /// <summary>Stops the system-owned session and component updates, releases playback, and retains passive authoring output.</summary>
    public void StopGame()
    {
        m_stopped = true;
        if (m_runtimeScene is { isLoaded: true } owner)
            foreach (var item in owner.GetObjects().ToArray())
            {
                if (!item.isRuntimeValid || item.isDestroyed) continue;
                foreach (var list in item.GetComponents<Components.Interface.NovelUiListComponent>()) list.Clear();
                foreach (var audio in item.GetComponents<Components.NovelAudioComponent>()) audio.Stop(false);
            }
        m_sceneUi.Dispose();
        m_view?.Dispose();
        m_session?.Dispose();
        m_session = null; m_view = null; m_inputFrame = ulong.MaxValue;
        m_preparedFrame = null; m_pendingInput.Clear();
        sceneUiError = string.Empty;
        m_runtimeInput = InputSnapshot.empty; m_keyboardFrame = -1;
        m_input = new();
        if (m_storage is InnoNovelStorage storage) storage.Dispose();
        m_composer.Clear();
        m_screenshots?.Dispose(); m_screenshots = null;
    }

    /// <inheritdoc />
    protected override void OnUpdate()
    {
        if (m_stopped) return;
        NovelSceneSimulation.Tick(scene, Time.deltaTime);
        if (project is null)
        {
            ProcessInput();
            m_runtimeInput = Input.snapshot;
            m_canvasWidth = width; m_canvasHeight = height;
            if (material is not null) m_preparedFrame = CaptureScene();
            return;
        }
        EnsureSession();
        foreach (var owner in scene.GetObjects())
            foreach (var emitter in owner.GetComponents<Components.NovelAudioComponent>())
                if (emitter.cue is { } cue) emitter.SetPlayerGain(m_view!.activeSession.preferences.masterVolume * m_view.activeSession.preferences.GetVolume(cue.channel));
        if (m_storage is InnoNovelStorage storage) storage.Pump();
        ProcessInput();
        m_view!.Tick(Time.deltaTime);
        foreach (var owner in scene.GetObjects().ToArray())
            if (owner.isRuntimeValid && !owner.isDestroyed)
                foreach (var list in owner.GetComponents<Components.Interface.NovelUiListComponent>()) list.Refresh(m_view.dataSource);
        if (m_storage is InnoNovelStorage pendingStorage) pendingStorage.Pump();
        NovelProjectAsset game = project!;
        if (game.material is null) return;
        m_runtimeInput = Input.snapshot;
        var page = m_view.BuildFrame();
        m_canvasWidth = page.width; m_canvasHeight = page.height;
        m_preparedFrame = m_composer.Compose(page, game.material, m_view.activeSession.BuildStageFrame(game.width, game.height),
            game.width, game.height, game.effectsMaterial, m_view.activeSession.transitionUi,
            nativeUi: m_sceneUi.Capture(scene, new(game.width, game.height, InnoEngine.Mathematics.Matrix.identity), m_view.dataSource));
    }
    private void ProcessInput()
    {
        while (m_pendingInput.TryDequeue(out var input))
        {
            if (input.scene is null) { ReleaseSceneInput(); m_view?.ReleaseInput(); continue; }
            var result = HandleSceneInput(input.scene);
            foreach (var action in result.actions)
            {
                try
                {
                    InvokeSceneAction(action); sceneUiError = string.Empty;
                }
                catch (Exception failure) when (failure is not OutOfMemoryException)
                { sceneUiError = action.ownerId + ": " + failure.Message; }
            }
            if (!result.consumed) m_view?.HandleInput(input.reading);
        }
    }
    /// <inheritdoc />
    protected override void OnEnable() { m_runtimeScene = scene; m_runtimeEnabled = true; m_stopped = false; }
    /// <inheritdoc />
    protected override void OnDisable() { m_runtimeEnabled = false; StopGame(); m_runtimeScene = null; }
    /// <inheritdoc />
    protected override void OnDestroy() { m_runtimeEnabled = false; StopGame(); m_runtimeScene = null; }

    private void EnsureSession()
    {
        if (!m_runtimeEnabled || !isActiveAndEnabled || !scene.isLoaded || m_stopped)
            throw new InvalidOperationException("A Visual Novel session requires an enabled, running native scene system. Call StartGame after an explicit stop.");
        if (m_session is not null) return;
        _ = NovelSceneTargets.Validate(this);
        if (m_storage is InnoNovelStorage { isDisposed: true }) m_storage = new InnoNovelStorage();
        NovelProjectAsset game = project ?? throw new InvalidOperationException("Assign a Visual Novel project to this component.");
        var candidate = new NovelSession(game, new InnoNovelAudio(NovelAudioLibrary.Create(game)), commands: m_commands,
            options: new() { checkpointCodec = m_stateCodec, rollbackByteBudget = (long)m_rollbackBudgetMiB * 1024 * 1024,
                uiActions = m_uiActions, uiDataSources = m_uiDataSources, performanceTargets = m_performanceTargets,
                hostCodeFingerprint = NovelImplementationFingerprint.Combine([NovelImplementationFingerprint.TypeCode(typeof(NovelSceneSystem)),
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
