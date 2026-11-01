using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Inno.VisualNovel.Model.Programs;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Narrative.Values;
using Inno.VisualNovel.Player.Persistence;
using Inno.VisualNovel.Player.Shell;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using Inno.VisualNovel.Stage.Composition;
using Inno.VisualNovel.Player.Audio;
using Inno.VisualNovel.Player.Ports;
using Inno.VisualNovel.Model.Story.Nodes.Audio;
using Inno.VisualNovel.Model.Story.Nodes.Interface;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Player.Localization;
using Inno.VisualNovel.Player.Collections;
using Inno.VisualNovel.Narrative.Extensions;
using Inno.VisualNovel.Narrative.Debugging;
using Inno.VisualNovel.Player.Interface;
using Inno.VisualNovel.Model.Extensions;
using Inno.VisualNovel.Player.Interface.Actions;
using Inno.VisualNovel.Player.Interface.Data;

namespace Inno.VisualNovel.Player.Sessions;

/// <summary>Owns one isolated game's reading policy, shell state and interactive rollback journal.</summary>
public sealed class NovelSession : IDisposable
{
    private readonly StoryProgram m_program;
    private readonly StoryProgramLibrary m_library;
    private readonly VariableStore m_globals;
    private readonly RollbackJournal m_checkpoints;
    private string? m_pendingCheckpoint;
    private bool m_pendingAutomaticSave;
    private bool m_skipInputHeld;
    private bool m_holdOwnsSkipping;
    private long m_automaticSaveSequence;
    private string m_automaticSaveSource = string.Empty;
    private long m_automaticSaveInteraction = -1;
    private readonly NovelThemeLibrary m_themes;
    private readonly NovelUiResourceClosure m_uiResources;
    private UI.Documents.UiFrame? m_lastUi;
    private TransitionUiState m_transitionUi = new();
    private readonly Dictionary<string, StageAsset> m_stages;
    private readonly Dictionary<string, CharacterAsset> m_characters;
    private readonly NovelSpeakerSource m_speakers;
    private readonly string m_initialStage;
    private readonly PerformanceClipAsset[] m_performances;
    private readonly IReadOnlyDictionary<string, PerformanceProperty[]> m_performanceTargets;
    private readonly Performance.NovelPerformanceTargets m_extensionTargets;
    private readonly VisualEffectAsset[] m_particles;
    private readonly VisualEffectAsset[] m_screenEffects;
    private readonly VisualEffectAsset[] m_transitions;
    private readonly List<ReadingRecord> m_history = [];
    private readonly HashSet<string> m_read = new(StringComparer.Ordinal);
    private long m_readRevision;
    private StoryMachine? m_story;
    private long m_observed = -1;
    private long m_observedEpoch;
    private double m_autoElapsed;
    private double m_playTime;
    private long m_stateRevision;
    private readonly bool m_hasTitleAudio;
    private readonly string m_title;
    private readonly NovelCommandRegistry m_commands;
    private readonly bool m_preview;
    private readonly NovelPresentationCoordinator m_presenter;
    private readonly StoryDebugger? m_debugger;
    private readonly HashSet<string> m_pages;
    private readonly string m_hostCodeFingerprint;
    internal NovelUiActionRegistry uiActions { get; }
    internal NovelUiDataSourceRegistry uiDataSources { get; }

    /// <summary>Creates an isolated session over an imported game's initial compiled chapter.</summary>
    /// <param name="project">The imported content definition.</param>
    /// <param name="audioOutput">An optional owner-local audio adapter; audio instructions fail explicitly without it.</param>
    /// <param name="replay">Whether this session prohibits ordinary saves and owns throwaway progress.</param>
    /// <param name="commands">Explicit command factories owned by the current generation.</param>
    /// <param name="preview">Whether command preview policy is enforced.</param>
    /// <param name="debugger">An optional owner-local source debugger, excluded from saves.</param>
    /// <param name="options">Native checkpoint policy, frozen UI registrations and an optional host build contribution.</param>
    public NovelSession(
        NovelProjectAsset project,
        INovelAudioOutput? audioOutput = null,
        bool replay = false,
        NovelCommandRegistry? commands = null,
        bool preview = false,
        StoryDebugger? debugger = null,
        NovelSessionOptions? options = null
    ) {
        ArgumentNullException.ThrowIfNull(project);
        m_commands = (commands ?? new()).Snapshot(); m_preview = preview;
        uiActions = (options?.uiActions ?? new()).Snapshot(); uiDataSources = (options?.uiDataSources ?? new()).Snapshot();
        m_extensionTargets = options?.performanceTargets ?? new(new Dictionary<string, PerformanceProperty[]>());
        m_hostCodeFingerprint = options?.hostCodeFingerprint ?? string.Empty;
        if (m_hostCodeFingerprint.Length > 4096) throw new ArgumentException("The explicit host code fingerprint must be bounded to 4096 characters.", nameof(options));
        m_debugger = debugger;
        m_pages = project.screens.Select(static binding => binding.role).ToHashSet(StringComparer.Ordinal);
        pageState = new(project.screens.Concat(project.themes.SelectMany(static theme => theme.screens)));
        pageState.Enter("title");
        if (m_pages.Any(static role => !SystemNodeRules.IsPageRole(role))) throw new InvalidOperationException("Authored page roles must be bounded stable identifiers.");
        designWidth = project.width; designHeight = project.height;
        m_themes = new(project); themeId = project.defaultTheme;
        m_uiResources = new(project);
        if (project.rollbackLimit is < 1 or > 10000) throw new ArgumentException("Rollback limit must be between 1 and 10000.", nameof(project));
        m_checkpoints = new(project.rollbackLimit, options?.checkpointCodec, options?.rollbackByteBudget ?? 64L * 1024 * 1024);
        var library = m_library = new StoryProgramLibrary(project);
        m_program = library.program;
        uiPerformance = new(project);
        uiPerformance.ValidateProgram(m_program, project.performances);
        var effectTargets = Effects.NovelEffectPerformance.Targets(m_program, project.screenEffects);
        Effects.NovelEffectPerformance.ValidateProgram(m_program, project.performances, effectTargets);
        m_extensionTargets.ValidateBindings(m_program, project.performances);
        m_performanceTargets = NovelAudioPerformance.Targets(m_program).Concat(uiPerformance.GetChannels()).Concat(effectTargets)
            .Concat(m_extensionTargets.targets.Select(static pair => new KeyValuePair<string, PerformanceProperty[]>(pair.Key, pair.Value.ToArray())))
            .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
        m_globals = new(m_program.variables.Where(static item => item.scope == VariableScope.Global));
        m_stages = project.stages.ToDictionary(static item => item.resourceId, StageAssetCopy.Copy, StringComparer.Ordinal);
        if (project.characters.Length > 1024) throw new InvalidOperationException("A game character library is limited to 1024 declared characters.");
        foreach (var character in project.characters) CharacterDefinitionRules.Validate(character);
        m_characters = project.characters.ToDictionary(static item => item.resourceId, CharacterAssetCopy.Copy, StringComparer.Ordinal);
        m_speakers = new(m_characters);
        m_presenter = new(m_stages, m_characters, ApplyTransition, ApplySystem, designWidth, designHeight);
        m_initialStage = project.initialStage;
        m_performances = project.performances.Select(PerformanceClipCopy.Copy).ToArray();
        m_particles = project.particles.Select(VisualEffectCopy.Copy).ToArray();
        m_screenEffects = project.screenEffects.Select(VisualEffectCopy.Copy).ToArray(); m_transitions = project.transitions.Select(VisualEffectCopy.Copy).ToArray();
        localization = new(project.localization, project.defaultLanguage);
        collections = new(project.collections);
        stage = CreateStage();
        audio = audioOutput is null ? null : new NovelAudioDirector(NovelAudioLibrary.Create(project), audioOutput, m_characters.Keys);
        m_hasTitleAudio = project.titleAudio is not null;
        gameId = project.gameId;
        m_title = project.title;
        isReplay = replay;
        buildFingerprint = NovelImplementationFingerprint.Combine([
            "content/" + (project.buildFingerprint.Length > 0 ? project.buildFingerprint : m_program.fingerprint),
            "model/" + NovelImplementationFingerprint.TypeCode(typeof(StoryProgram)),
            "narrative/" + NovelImplementationFingerprint.TypeCode(typeof(StoryMachine)),
            "stage/" + NovelImplementationFingerprint.TypeCode(typeof(StageController)),
            "ui/" + NovelImplementationFingerprint.TypeCode(typeof(UI.Documents.UiDocumentInstance)),
            "player/" + NovelImplementationFingerprint.TypeCode(typeof(NovelSession)),
            "commands/" + m_commands.codeFingerprint, "actions/" + uiActions.codeFingerprint, "sources/" + uiDataSources.codeFingerprint,
            "host/" + m_hostCodeFingerprint, "performance-targets/" + m_extensionTargets.fingerprint]);
        entryId = library.startEntry;
    }

    /// <summary>Gets the stable game identity.</summary>
    public string gameId { get; }
    /// <summary>Gets the exact content, core runtime code, registered extension implementations and supplied host build fingerprint.</summary>
    public string buildFingerprint { get; }
    internal NovelSessionOptions ReplayOptions() => new() { uiActions = uiActions, uiDataSources = uiDataSources, hostCodeFingerprint = m_hostCodeFingerprint, performanceTargets = m_extensionTargets };
    /// <summary>Gets the game title.</summary>
    public string title => localization.Resolve("game/title", m_title);
    /// <summary>Gets the initial public entry.</summary>
    public string entryId { get; }
    /// <summary>Gets the active shell page.</summary>
    public string page { get; private set; } = "title";
    /// <summary>Gets this game's isolated typed page state, independent of narrative variables and global progress.</summary>
    public NovelPageStateStore pageState { get; private set; }
    /// <summary>Gets the active declared theme identity, or empty for the literal fallback pages.</summary>
    public string themeId { get; private set; }
    /// <summary>Gets frozen theme choices in authored library order.</summary>
    public IReadOnlyList<(string id, string title)> themes => m_themes.options;
    internal Model.Interface.Themes.ThemeAsset? activeTheme => m_themes.Get(themeId);
    /// <summary>Switches to a validated frozen theme without restarting narrative, audio or modal navigation.</summary>
    /// <param name="id">A declared theme identity, or empty for literal fallback pages.</param>
    public void SetTheme(string id)
    {
        _ = m_themes.Get(id); themeId = id;
    }
    /// <summary>Gets the active interpreter, or null before a game starts.</summary>
    public StoryMachine? story => m_story;
    /// <summary>Gets the single reading/input owner, or the root interpreter while no task owns interaction.</summary>
    public StoryMachine? interactiveStory => m_story?.interactiveOwner ?? m_story;
    /// <summary>Gets the current dialogue's frozen metadata, or null for unlinked speakers and narration.</summary>
    public CharacterPresentation? currentCharacter => interactiveStory is { characterId.Length: > 0 } current && m_characters.TryGetValue(current.characterId, out var character)
        ? new(character.resourceId, m_speakers.ResolveName(character.resourceId, localization), Array.AsReadOnly(character.aliases), character.nameColor, character.portrait) : null;
    /// <summary>Gets detached character names for player-owned voice settings in authored library order.</summary>
    public IReadOnlyList<(string id, string name)> characterNames => m_characters.Values.Select(character => (character.resourceId, m_speakers.ResolveName(character.resourceId, localization))).ToArray();
    /// <summary>Gets this session's GPU-independent composition owner.</summary>
    public StageController stage { get; private set; }
    /// <summary>Samples active extension writers on the session owner thread without advancing time or invoking a bridge.</summary>
    /// <returns>Detached, deterministically ordered samples; absent channels have no writer and should use the bridge's authored default.</returns>
    public IReadOnlyList<Stage.Timeline.PerformanceSample> CaptureExtensionPerformance() => stage.performances.Evaluate()
        .Where(pair => m_extensionTargets.targets.ContainsKey(pair.Key.target))
        .OrderBy(static pair => pair.Key.target, StringComparer.Ordinal).ThenBy(static pair => pair.Key.property)
        .Select(static pair => new Stage.Timeline.PerformanceSample(pair.Key.target, pair.Key.property, pair.Value)).ToArray();
    /// <summary>Reads one active declared extension writer without changing playback, narration or external scene state.</summary>
    /// <param name="target">The complete extension/ target identity.</param>
    /// <param name="property">One of its declared scalar channels.</param>
    /// <param name="value">The current finite value, or zero when no writer is active.</param>
    /// <returns>Whether this session currently owns a matching writer; false does not mean an active zero value.</returns>
    public bool TryGetExtensionPerformance(
        string target,
        PerformanceProperty property,
        out double value
    ) {
        ArgumentNullException.ThrowIfNull(target); value = 0;
        return m_extensionTargets.targets.TryGetValue(target, out var channels) && channels.Contains(property)
            && stage.performances.Evaluate().TryGetValue((target, property), out value);
    }
    /// <summary>Gets the frozen page identities and shared-clock UI performance projection.</summary>
    public Interface.Performance.NovelUiPerformance uiPerformance { get; }
    /// <summary>Gets the independently reconstructed previous UI image for the active output boundary.</summary>
    public UI.Documents.UiFrame? transitionUi => CaptureTransitionUi().sequence == 0 ? null : m_uiResources.Restore(m_transitionUi.frame);
    /// <summary>Extracts the current localized stage without changing authored objects, transforms or asset references.</summary>
    /// <param name="width">The logical design width.</param>
    /// <param name="height">The logical design height.</param>
    /// <returns>Current and previous transition images resolved for the selected language.</returns>
    public StageFrame BuildStageFrame(
        float width,
        float height
    ) => localization.resources.Stage(stage.BuildFrame(width, height));
    /// <summary>Records the exact detached UI image most recently prepared for display.</summary>
    /// <param name="frame">The host's resolved paint commands, without live control instances.</param>
    public void PublishUiFrame(UI.Documents.UiFrame frame) => m_lastUi = frame;
    /// <summary>Gets the optional owner-local audio policy.</summary>
    public NovelAudioDirector? audio { get; }
    /// <summary>Gets player preferences, separate from rollback.</summary>
    public PlayerPreferences preferences { get; private set; } = new();
    /// <summary>Gets this session's immutable selected game-language view.</summary>
    public NovelLocalizationService localization { get; private set; }
    /// <summary>Gets owner-local global collection progress, excluded from ordinary snapshots and rollback.</summary>
    public NovelCollectionService collections { get; }
    /// <summary>Gets whether this isolated session prohibits ordinary game saves.</summary>
    public bool isReplay { get; }
    /// <summary>Gets this session's frozen generation-local command registry.</summary>
    public NovelCommandRegistry commands => m_commands;
    /// <summary>Gets whether this owner enforces preview side-effect policy.</summary>
    public bool isPreview => m_preview;
    /// <summary>Gets whether a stable main-game state and all active extensions can be saved.</summary>
    public bool canSave => !isReplay && m_story is { canSave: true, status: not (StoryStatus.Stopped or StoryStatus.Running or StoryStatus.Faulted) }
        && page is not ("title" or "music" or "extras" or "gallery" or "replays" or "endings" or "glossary" or "characters" or "collectionDetail");
    /// <summary>Applies a validated settings candidate without altering game progress or rollback.</summary>
    /// <param name="candidate">Detached preferences from the independent settings store.</param>
    public void ApplyPreferences(PlayerPreferences candidate)
    {
        PlayerPreferences copy = candidate.Copy(); SetLanguage(copy.language); preferences = copy; stage.Tick(0, copy.reducedMotion, copy.allowFlashing);
    }

    /// <summary>Prepares localized reading and audio before committing a game-language change.</summary>
    /// <param name="language">A declared tag, or empty for the project's default.</param>
    public void SetLanguage(string language)
    {
        NovelLocalizationService texts = localization.ForLanguage(language);
        if (texts.language == localization.language) { preferences.language = language; return; }
        StoryMachine? candidate = null;
        if (m_story?.InspectTasks().Any(static task => task.status == StoryStatus.Command) == true)
        {
            m_story.SetTextSource(texts); localization = texts; preferences.language = language; return;
        }
        if (m_story is { status: not (StoryStatus.Faulted or StoryStatus.Stopped) })
        {
            candidate = new(m_program, globals: m_globals, textSource: texts, commands: m_commands, preview: m_preview, speakers: m_speakers); candidate.Restore(m_story.Capture()); candidate.RefreshLocalizedDialogue();
            if (candidate.interactiveOwner is { status: StoryStatus.Dialogue } reading && audio is not null) audio.Restore(audio.RetargetReadingVoice(audio.Capture(), reading.voiceCueId));
        }
        localization = texts; preferences.language = language;
        if (candidate is not null) { m_story?.Dispose(); m_story = candidate; m_observed = candidate.interactionSequence; RefreshCurrentHistory(); }
    }
    /// <summary>Gets whether automatic reading is active.</summary>
    public bool automatic { get; private set; }
    /// <summary>Gets whether read skipping is active.</summary>
    public bool skipping { get; private set; }
    /// <summary>Gets whether reading UI is hidden.</summary>
    public bool hidden { get; private set; }
    /// <summary>Gets the game's logical reading canvas width.</summary>
    public float designWidth { get; }
    /// <summary>Gets the game's logical reading canvas height.</summary>
    public float designHeight { get; }
    /// <summary>Gets completed and current history lines.</summary>
    public IReadOnlyList<ReadingRecord> history => m_history.Select(static line => new ReadingRecord
        { nodeId = line.nodeId, characterId = line.characterId, speaker = line.speaker, text = line.text, contentId = line.contentId, voiceCueId = line.voiceCueId }).ToArray();
    /// <summary>Gets the number of recorded lines without copying the backlog.</summary>
    public int historyCount => m_history.Count;
    /// <summary>Copies a bounded portion of history while retaining its original row indices.</summary>
    /// <param name="offset">The zero-based start, at most the current history count.</param>
    /// <param name="count">Requested rows, at most 10000.</param>
    /// <returns>Detached history rows suitable for paged UI.</returns>
    public IReadOnlyList<ReadingRecord> ReadHistoryRange(
        int offset,
        int count
    ) {
        if (offset < 0 || offset > m_history.Count || count is < 0 or > 10000) throw new ArgumentOutOfRangeException(nameof(offset));
        return m_history.Skip(offset).Take(count).Select(static line => new ReadingRecord
            { nodeId = line.nodeId, characterId = line.characterId, speaker = line.speaker, text = line.text, contentId = line.contentId, voiceCueId = line.voiceCueId }).ToArray();
    }
    /// <summary>Gets whether an older interactive boundary can be restored.</summary>
    public bool canRollback => PreviousInteractiveCheckpoint() >= 0;
    /// <summary>Gets retained explicit checkpoint names in most-recent order within the current rollback barrier.</summary>
    public IReadOnlyList<string> namedCheckpoints => m_checkpoints.Where(point => point.available && point.name.Length > 0 && point.epoch == m_story?.rollbackEpoch)
        .Reverse().Select(static point => point.name).Distinct(StringComparer.Ordinal).ToArray();
    /// <summary>Gets retained native checkpoint payload bytes, or null when the host has not supplied a native codec.</summary>
    public long? rollbackStoredBytes => m_checkpoints.storedBytes;
    /// <summary>Gets the configured native rollback payload budget; metadata is separately bounded by the checkpoint count.</summary>
    public long rollbackByteBudget => m_checkpoints.budget;
    /// <summary>Gets the last omitted-checkpoint explanation without faulting or replacing the running game.</summary>
    public string rollbackDiagnostic => m_checkpoints.diagnostic;
    /// <summary>Changes the owner-local retention budget immediately, releasing oldest native buffers first; zero disables retention.</summary>
    /// <param name="bytes">A value from zero through one GiB; object-only fallback cannot claim byte accounting.</param>
    public void SetRollbackBudget(long bytes) => m_checkpoints.SetBudget(bytes);
    /// <summary>Gets the owner-local count of explicit automatic-save requests, excluded from ordinary saves.</summary>
    public long automaticSaveSequence => m_automaticSaveSequence;
    /// <summary>Gets the source address of the latest explicit automatic-save request.</summary>
    public string automaticSaveSource => m_automaticSaveSource;
    internal bool isAutomaticSaveBoundary => m_story?.status == StoryStatus.Suspended && m_story.interactionSequence == m_automaticSaveInteraction;
    /// <summary>Gets accumulated active game seconds.</summary>
    public double playTime => m_playTime;
    /// <summary>Gets an owner-local revision that invalidates delayed loads when the game is restarted, restored or rolled back.</summary>
    public long stateRevision => m_stateRevision;
    /// <summary>Gets independent owner-local dirty counters for variables, reading and unlock progress.</summary>
    public (long variables, long reading, long collections) progressRevision => (m_globals.revision, m_readRevision, collections.revision);
    /// <summary>Gets the reason for the most recent owner revision change.</summary>
    public SessionReplacement lastReplacement { get; private set; }

    /// <summary>Starts a fresh game while retaining global progress and preferences.</summary>
    public void StartNew()
    {
        StartAtEntry(entryId, []);
    }

    /// <summary>Starts an explicit authored entry with declared initial values, retaining this isolated scope's progress.</summary>
    /// <param name="chapter">The authored chapter identity.</param>
    /// <param name="entry">The chapter's public entry identity.</param>
    /// <param name="initialValues">Explicit non-global initial values applied before the first instruction executes.</param>
    /// <param name="initialStage">Optional neutral preconditions restored before the first presentation instruction.</param>
    public void StartAt(
        string chapter,
        string entry,
        VariableState[] initialValues,
        StageState? initialStage = null
    ) => StartAtEntry(m_library.ResolveEntry(chapter, entry), initialValues, initialStage);

    private void StartAtEntry(
        string target,
        VariableState[] values,
        StageState? initialStage = null
    )
    {
        var candidate = new StoryMachine(m_program, globals: m_globals, textSource: localization, commands: m_commands, preview: m_preview, speakers: m_speakers);
        StageController initialPresentation = CreateStage();
        if (initialStage is not null) initialPresentation.Restore(initialStage);
        if (values.Select(static item => item.id).Distinct(StringComparer.Ordinal).Count() != values.Length) throw new InvalidOperationException("Initial values contain duplicate identities.");
        foreach (var item in values)
        {
            if (m_globals.Contains(item.id)) throw new InvalidOperationException("Replay initial values cannot replace global progress.");
            candidate.SetVariable(item.id, item.value);
        }
        m_story?.Dispose(); m_story = candidate;
        candidate.AttachDebugger(m_debugger);
        stage = initialPresentation;
        m_transitionUi = new();
        audio?.Restore(new(), NovelAudioPerformance.Mix(stage));
        m_checkpoints.Clear(); m_history.Clear(); m_observed = -1; m_observedEpoch = 0;
        ResetSystemBoundary();
        m_playTime = 0; m_autoElapsed = 0;
        automatic = skipping = hidden = false;
        pageState.ResetAll(); pageState.Enter("reading");
        page = "reading";
        m_story.Start(target);
        m_stateRevision++; lastReplacement = SessionReplacement.NewGame;
        PumpPresentation();
        Observe();
    }

    /// <summary>Advances active logical time and applies bounded automatic reading.</summary>
    /// <param name="seconds">A finite nonnegative interval.</param>
    public void Tick(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (m_story?.debugPaused == true) return;
        audio?.SetMix(preferences.masterVolume, preferences.channelVolumes);
        audio?.SetCharacterMix(preferences.characterVoices);
        if (page == "title" && m_hasTitleAudio && audio is not null && !audio.IsActive("title"))
            audio.Apply(new() { operation = AudioOperation.Play, slot = "title", cueId = NovelAudioLibrary.titleCueId });
        audio?.Tick(seconds);
        if (m_story is null || page != "reading") return;
        m_playTime += seconds;
        stage.Tick(seconds, preferences.reducedMotion, preferences.allowFlashing);
        NovelAudioPerformance.Apply(stage, audio);
        m_story.Tick(seconds, preferences.textSpeed);
        PumpPresentation();
        Observe();
        if (m_story.debugPaused) return;
        collections.Evaluate(m_story.GetVariable);
        var reading = interactiveStory!;
        if (reading.status == StoryStatus.Dialogue)
        {
            string content = CurrentContent();
            if (skipping && (!preferences.skipReadOnly || m_read.Contains(content)))
            {
                if (!reading.isTextComplete) m_story.Advance();
                Advance();
            }
            else if (skipping) skipping = false;
            else if (automatic && reading.isTextComplete)
            {
                m_autoElapsed += seconds;
                int count = StringInfo.ParseCombiningCharacters(reading.dialogueText).Length;
                if (m_autoElapsed >= preferences.autoDelay + count * 0.025 && !(audio?.IsActive("voice") ?? false)) Advance();
            }
        }
        else { m_autoElapsed = 0; if (reading.status is StoryStatus.Choice or StoryStatus.Input or StoryStatus.AdvanceWait) skipping = false; }
        MarkRead();
    }

    /// <summary>Consumes one manual advance, revealing text before continuing.</summary>
    /// <returns>Whether the current reading state accepted it.</returns>
    public bool Advance()
    {
        if (hidden) { hidden = false; return true; }
        if (page != "reading" || m_story is null || m_story.debugPaused) return false;
        MarkRead();
        if (interactiveStory is { status: StoryStatus.Dialogue, isTextComplete: true } && preferences.stopVoiceOnAdvance) audio?.StopVoice();
        bool accepted = m_story.Advance();
        PumpPresentation();
        m_autoElapsed = 0;
        Observe();
        return accepted;
    }

    /// <summary>Accepts a visible enabled option without reusing its input.</summary>
    /// <param name="id">Stable option identity.</param>
    /// <returns>Whether the choice was accepted.</returns>
    public bool Choose(string id)
    {
        if (page != "reading" || m_story is null) return false;
        bool accepted = m_story.Choose(id);
        PumpPresentation();
        Observe();
        return accepted;
    }

    /// <summary>Commits a valid modal player text draft without advancing the following dialogue.</summary>
    /// <returns>Whether the input action was accepted.</returns>
    public bool SubmitInput()
    {
        if (page != "reading" || m_story is null || !m_story.SubmitInput()) return false;
        PumpPresentation(); Observe(); return true;
    }

    /// <summary>Cancels an explicitly cancellable player input without writing its variable.</summary>
    /// <returns>Whether the cancellation was accepted.</returns>
    public bool CancelInput()
    {
        if (page != "reading" || m_story is null || !m_story.CancelInput()) return false;
        PumpPresentation(); Observe(); return true;
    }
    /// <summary>Requests cancellation through the active command's explicit graph output.</summary>
    /// <returns>Whether the command accepted cancellation.</returns>
    public bool CancelCommand()
    {
        if (page != "reading" || !(m_story?.CancelCommand() ?? false)) return false;
        PumpPresentation(); Observe(); return true;
    }

    /// <summary>Opens an implemented system page, suspending story time.</summary>
    /// <param name="role">The stable system page role.</param>
    public void OpenPage(string role)
    {
        if (role is not ("title" or "reading" or "pause" or "settings" or "history" or "credits" or "save" or "load" or "confirm" or "extras" or "gallery" or "music" or "replays" or "endings" or "glossary" or "characters" or "collectionDetail")
            && !m_pages.Contains(role)) throw new ArgumentException("The requested shell page is unavailable.", nameof(role));
        if (role == "reading" && m_story is null) throw new InvalidOperationException("Start a game before returning to reading.");
        if (role == "title" && page != "title" && !(audio?.IsActive("title") ?? false)) audio?.Restore(new(), new Dictionary<string, double>());
        pageState.Enter(role); page = role;
        hidden = false;
        skipping = false;
    }

    /// <summary>Toggles automatic progression.</summary>
    public void ToggleAutomatic() { automatic = !automatic; skipping = false; m_holdOwnsSkipping = false; m_autoElapsed = 0; }
    /// <summary>Toggles skipping according to read policy.</summary>
    public void ToggleSkipping() { skipping = !skipping; m_holdOwnsSkipping = false; automatic = false; }
    /// <summary>Applies hold-to-skip edges without rearming after an unread line, modal interaction or script barrier.</summary>
    /// <param name="held">Whether the physical viewport-local input is still held.</param>
    /// <param name="allowed">Whether the current UI, composition and navigation owners permit a new reading shortcut.</param>
    public void SetSkippingHeld(
        bool held,
        bool allowed
    ) {
        bool pressed = held && !m_skipInputHeld; m_skipInputHeld = held;
        if (!held || !allowed || page != "reading" || hidden || interactiveStory is not { status: StoryStatus.Dialogue, debugPaused: false })
        {
            if (m_holdOwnsSkipping) skipping = false;
            m_holdOwnsSkipping = false;
            return;
        }
        if (!pressed) return;
        skipping = true; automatic = false; m_autoElapsed = 0; m_holdOwnsSkipping = true;
    }
    /// <summary>Hides or restores the reading overlay.</summary>
    public void ToggleHidden() => hidden = !hidden;

    /// <summary>Restores the previous interactive state and discards the abandoned future.</summary>
    /// <returns>Whether a previous boundary existed.</returns>
    public bool Rollback()
    {
        if (!canRollback || m_story is null) return false;
        return RestoreCheckpoint(PreviousInteractiveCheckpoint());
    }
    /// <summary>Restores the most recent retained named checkpoint and discards the abandoned future.</summary>
    /// <param name="name">The exact authored checkpoint name.</param>
    /// <returns>False for an expired or barrier-separated name.</returns>
    public bool RollbackToCheckpoint(string name)
    {
        int index = m_checkpoints.FindLastIndex(point => point.available && point.name == name && name.Length > 0 && point.epoch == m_story?.rollbackEpoch);
        return index >= 0 && RestoreCheckpoint(index);
    }
    private int PreviousInteractiveCheckpoint()
    {
        if (m_story is null) return -1;
        for (int index = m_checkpoints.Count - 2; index >= 0; index--)
            if (m_checkpoints[index].available && m_checkpoints[index].epoch == m_story.rollbackEpoch && m_checkpoints[index].boundary is not null) return index;
        return -1;
    }

    /// <summary>Reports whether an older backlog line still has a retained checkpoint within the current rollback barrier.</summary>
    /// <param name="index">The original zero-based history row.</param>
    /// <returns>False for expired, current or irreversible positions.</returns>
    public bool CanRollbackToHistory(int index) => FindHistoryCheckpoint(index) >= 0;

    /// <summary>Restores a retained older backlog boundary and discards the abandoned future.</summary>
    /// <param name="index">The original zero-based history row.</param>
    /// <returns>Whether that exact reversible checkpoint is still retained.</returns>
    public bool RollbackToHistory(int index)
    {
        int checkpoint = FindHistoryCheckpoint(index); return checkpoint >= 0 && RestoreCheckpoint(checkpoint);
    }

    private int FindHistoryCheckpoint(int index)
    {
        if (index < 0 || index >= m_history.Count || m_story is null) return -1;
        for (int checkpoint = m_checkpoints.Count - 2; checkpoint >= 0; checkpoint--)
            if (m_checkpoints[checkpoint].available && m_checkpoints[checkpoint].history == index + 1 && m_checkpoints[checkpoint].boundary == StoryStatus.Dialogue
                && m_checkpoints[checkpoint].epoch == m_story.rollbackEpoch) return checkpoint;
        return -1;
    }

    private bool RestoreCheckpoint(int index)
    {
        if (m_story is null) return false;
        var checkpoint = m_checkpoints.Read(index);
        NovelPageStateStore pageCandidate = pageState.Fork(checkpoint.pageState);
        _ = m_themes.Get(checkpoint.theme);
        StageController stageCandidate = CreateStage(false);
        stageCandidate.Restore(checkpoint.stage);
        TransitionUiState uiCandidate = PrepareTransitionUi(checkpoint.ui, stageCandidate);
        stageCandidate.Tick(0, preferences.reducedMotion, preferences.allowFlashing);
        var narrativeCandidate = new StoryMachine(m_program, globals: m_globals, textSource: localization, commands: m_commands, preview: m_preview, speakers: m_speakers);
        try
        {
            narrativeCandidate.Restore(checkpoint.state);
            narrativeCandidate.RefreshLocalizedDialogue();
            RestoreAudio(checkpoint.audio, narrativeCandidate, checkpoint.language != localization.language, stageCandidate);
        }
        catch { narrativeCandidate.Dispose(); throw; }
        m_story.Dispose(); m_story = narrativeCandidate;
        narrativeCandidate.AttachDebugger(m_debugger);
        stage = stageCandidate;
        themeId = checkpoint.theme;
        pageState = pageCandidate; pageState.Enter("reading");
        m_transitionUi = uiCandidate;
        m_checkpoints.RemoveRange(index + 1, m_checkpoints.Count - index - 1);
        if (m_history.Count > checkpoint.history) m_history.RemoveRange(checkpoint.history, m_history.Count - checkpoint.history);
        RefreshCurrentHistory();
        m_observed = m_story.interactionSequence;
        m_observedEpoch = m_story.rollbackEpoch;
        page = "reading"; hidden = false; automatic = skipping = false; m_autoElapsed = 0;
        ResetSystemBoundary();
        m_stateRevision++; lastReplacement = SessionReplacement.Rollback;
        return true;
    }

    /// <summary>Reports whether the current content revision was already read in this progress scope.</summary>
    /// <returns>Whether the active line's identity and content are known.</returns>
    public bool IsCurrentRead() => m_story is not null && m_read.Contains(CurrentContent());

    /// <summary>Captures detached game state independently of global progress and preferences.</summary>
    /// <returns>A complete checkpoint of the implemented narrative and presentation domains.</returns>
    public NovelSessionState Capture()
    {
        if (!canSave)
            throw new InvalidOperationException("A stable active game is required to save.");
        return new() { gameId = gameId, buildFingerprint = buildFingerprint, narrative = m_story!.Capture(), stage = stage.Capture(), audio = audio?.Capture() ?? new(),
            history = history.ToArray(), playTime = m_playTime, transitionUi = CaptureTransitionUi(), themeId = themeId, presentationLanguage = localization.language, pageState = pageState.Capture() };
    }

    /// <summary>Validates a candidate game completely before replacing the active narrative and stage.</summary>
    /// <param name="snapshot">Detached state for the exact current content build.</param>
    public void Restore(NovelSessionState snapshot)
    {
        if (isReplay) throw new InvalidOperationException("Ordinary loading is unavailable during replay.");
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.gameId != gameId || snapshot.buildFingerprint != buildFingerprint) throw new InvalidOperationException("The save belongs to a different game build.");
        _ = m_themes.Get(snapshot.themeId);
        NovelPageStateStore pageCandidate = pageState.Fork(snapshot.pageState);
        if (!double.IsFinite(snapshot.playTime) || snapshot.playTime < 0 || snapshot.history.Length > 100000
            || snapshot.history.Any(static item => item is null || item.nodeId.Length == 0 || item.contentId.Length != 64)
            || snapshot.narrative.status is StoryStatus.Stopped or StoryStatus.Running or StoryStatus.Faulted)
            throw new InvalidOperationException("The save contains invalid session metadata.");
        var candidate = new StoryMachine(m_program, globals: m_globals, textSource: localization, commands: m_commands, preview: m_preview, speakers: m_speakers);
        StageController presentation; ReadingRecord[] records; TransitionUiState ui;
        try
        {
            candidate.Restore(snapshot.narrative); candidate.RefreshLocalizedDialogue();
            presentation = CreateStage(false); presentation.Restore(snapshot.stage);
            ui = PrepareTransitionUi(snapshot.transitionUi, presentation);
            presentation.Tick(0, preferences.reducedMotion, preferences.allowFlashing);
            records = snapshot.history.Select(static line => new ReadingRecord { nodeId = line.nodeId, characterId = line.characterId, speaker = line.speaker, text = line.text, contentId = line.contentId, voiceCueId = line.voiceCueId }).ToArray();
            if (records.Any(line => line.characterId.Length > 0 && !m_characters.ContainsKey(line.characterId))) throw new InvalidOperationException("History contains an undeclared voiced character.");
            RestoreAudio(snapshot.audio, candidate, snapshot.presentationLanguage != localization.language, presentation);
        }
        catch { candidate.Dispose(); throw; }
        m_story?.Dispose(); m_story = candidate; stage = presentation;
        themeId = snapshot.themeId;
        pageState = pageCandidate; pageState.Enter("reading");
        candidate.AttachDebugger(m_debugger);
        m_transitionUi = ui;
        m_history.Clear(); m_history.AddRange(records);
        RefreshCurrentHistory();
        m_checkpoints.Clear();
        ResetSystemBoundary();
        if (candidate.interactiveOwner is not null) m_checkpoints.Add(new(candidate.Capture(), presentation.Capture(), audio?.Capture() ?? new(), records.Length, CaptureTransitionUi(), pageState.Capture(), themeId, language: localization.language));
        m_playTime = snapshot.playTime; m_observed = candidate.interactionSequence; m_observedEpoch = candidate.rollbackEpoch; m_autoElapsed = 0;
        page = "reading"; hidden = automatic = skipping = false;
        m_stateRevision++; lastReplacement = SessionReplacement.Load;
    }

    /// <summary>Captures global progress separately from ordinary saves and rollback.</summary>
    /// <returns>Detached declared globals and read content identifiers.</returns>
    public GlobalProgressState CaptureProgress() => new() { gameId = gameId, readContent = m_read.Order(StringComparer.Ordinal).ToArray(), variables = m_globals.Capture(), unlocked = collections.Capture() };

    /// <summary>Atomically replaces explicitly loaded global progress without changing game state.</summary>
    /// <param name="snapshot">A matching game's native progress packet.</param>
    public void RestoreProgress(GlobalProgressState snapshot)
    {
        ValidateProgressSnapshot(snapshot);
        m_globals.Restore(snapshot.variables);
        collections.Restore(snapshot.unlocked);
        m_read.Clear(); m_read.UnionWith(snapshot.readContent); m_readRevision++;
    }

    /// <summary>Validates both independent startup packets before applying either to an unstarted main game.</summary>
    /// <param name="progress">Loaded progress, or null for a first run.</param>
    /// <param name="playerPreferences">Loaded settings, or null for project defaults.</param>
    public void RestoreProfile(
        GlobalProgressState? progress,
        PlayerPreferences? playerPreferences
    ) {
        if (m_story is not null || isReplay) throw new InvalidOperationException("Initial profile application requires an unstarted main game.");
        if (progress is not null) ValidateProgressSnapshot(progress);
        PlayerPreferences? candidate = playerPreferences?.Copy();
        if (candidate is not null) _ = localization.ForLanguage(candidate.language);
        if (candidate is not null) ApplyPreferences(candidate);
        if (progress is not null) RestoreProgress(progress);
    }

    private void ValidateProgressSnapshot(GlobalProgressState snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.gameId != gameId || snapshot.readContent.Length > 1000000
            || snapshot.readContent.Any(static item => item.Length != 64 || !item.All(static ch => ch is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F'))) throw new InvalidOperationException("Global progress is invalid for this game.");
        var globalsCandidate = new VariableStore(m_program.variables.Where(static item => item.scope == VariableScope.Global));
        globalsCandidate.Restore(snapshot.variables);
        collections.ValidateProgress(snapshot.unlocked);
    }

    private void Observe()
    {
        if (m_story is null) return;
        var reading = interactiveStory!;
        collections.Evaluate(m_story.GetVariable);
        if (m_observedEpoch != m_story.rollbackEpoch) { automatic = skipping = false; m_observedEpoch = m_story.rollbackEpoch; }
        if (reading.status is StoryStatus.Choice or StoryStatus.Input or StoryStatus.AdvanceWait or StoryStatus.Completed or StoryStatus.Faulted) skipping = false;
        if (m_observed == m_story.interactionSequence) return;
        m_observed = m_story.interactionSequence;
        m_autoElapsed = 0;
        if (reading.status == StoryStatus.Dialogue)
        {
            string content = CurrentContent();
            try
            {
                if (reading.voiceCueId.Length > 0)
                {
                    (audio ?? throw new InvalidOperationException("Dialogue voice requires an audio output adapter.")).PlayVoice(reading.voiceCueId, reading.characterId);
                    reading.TrackReadingVoice();
                }
            }
            catch (Exception failure) when (failure is not OutOfMemoryException) { m_story.Fail(failure.Message); return; }
            m_history.Add(new() { nodeId = reading.nodeId, characterId = reading.characterId, speaker = reading.speaker, text = reading.dialogueText, contentId = content, voiceCueId = reading.voiceCueId });
        }
        if (reading.status is StoryStatus.Input or StoryStatus.AdvanceWait) { automatic = skipping = hidden = false; }
        if (reading.status is StoryStatus.Dialogue or StoryStatus.Choice or StoryStatus.Input or StoryStatus.AdvanceWait || m_story.status == StoryStatus.Suspended && m_pendingCheckpoint is not null)
        {
            m_checkpoints.Add(new(m_story.Capture(), stage.Capture(), audio?.Capture() ?? new(), m_history.Count, CaptureTransitionUi(), pageState.Capture(), themeId, m_pendingCheckpoint ?? string.Empty, localization.language));
            if (m_pendingAutomaticSave && !isReplay) { m_automaticSaveSequence = checked(m_automaticSaveSequence + 1); m_automaticSaveInteraction = m_story.interactionSequence; }
            m_pendingCheckpoint = null; m_pendingAutomaticSave = false;
        }
    }

    /// <summary>Replays an existing history line's voice without advancing or altering story state.</summary>
    /// <param name="index">The zero-based row in the current history.</param>
    /// <returns>Whether a voiced row and output adapter were available.</returns>
    public bool ReplayVoice(int index)
    {
        if (index < 0 || index >= m_history.Count || m_history[index].voiceCueId.Length == 0 || audio is null) return false;
        audio.PlayVoice(m_history[index].voiceCueId, m_history[index].characterId); return true;
    }

    private string CurrentContent()
    {
        if (interactiveStory is not { status: StoryStatus.Dialogue } reading) return string.Empty;
        string chapter = reading.sourceChapterId, line = reading.dialogueTextId;
        string identity = chapter.Length.ToString(CultureInfo.InvariantCulture) + ":" + chapter
            + line.Length.ToString(CultureInfo.InvariantCulture) + ":" + line + reading.dialogueSourceText;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
    }

    internal void MarkRead()
    {
        if (interactiveStory is { status: StoryStatus.Dialogue, isTextComplete: true } && m_read.Add(CurrentContent())) m_readRevision++;
    }

    private StageController CreateStage(bool applyPolicy = true)
    {
        var result = new StageController(m_stages.Values.SelectMany(static item => item.textures).Concat(m_characters.Values.SelectMany(static item => item.textures)),
            m_performances, m_particles, m_screenEffects, m_transitions, m_performanceTargets);
        if (m_initialStage.Length > 0) result.SetStage(m_stages[m_initialStage]);
        if (applyPolicy) result.Tick(0, preferences.reducedMotion, preferences.allowFlashing);
        return result;
    }

    private void PumpPresentation()
    {
        m_presenter.Pump(m_story, stage, audio, collections); NovelAudioPerformance.Apply(stage, audio);
    }

    private void ApplySystem(SystemNode action)
    {
        SystemNodeRules.Validate(action);
        switch (action.operation)
        {
            case SystemOperation.Checkpoint: m_pendingCheckpoint = action.argument; break;
            case SystemOperation.AutoSave:
                m_pendingCheckpoint = string.Empty; m_pendingAutomaticSave = true; m_automaticSaveSource = m_story!.sourceChapterId + "/" + m_story.sourceNodeId; break;
            case SystemOperation.RollbackBarrier: m_story!.BlockRollback(action.argument); break;
            case SystemOperation.SetTheme: SetTheme(action.argument); break;
            case SystemOperation.ReturnTitle: OpenPage("title"); break;
            case SystemOperation.OpenPage:
                if (!m_pages.Contains(action.argument)) throw new InvalidOperationException("The requested authored page is not declared: " + action.argument);
                OpenPage(action.argument); break;
        }
    }
    private void ResetSystemBoundary()
    {
        m_pendingCheckpoint = null; m_pendingAutomaticSave = false; m_automaticSaveSequence = 0; m_automaticSaveSource = string.Empty; m_automaticSaveInteraction = -1;
    }

    private void ApplyTransition(
        TransitionNode request,
        StageController owner
    ) {
        UI.Documents.UiFrameSnapshot? previous = request.operation == TransitionOperation.Begin && request.scope == TransitionScope.Output
            ? UI.Documents.UiFrameSnapshot.Capture(m_lastUi ?? new(designWidth, designHeight, [])) : null;
        if (previous is not null) m_uiResources.Restore(previous);
        owner.ApplyTransition(request, request.nextStage.Length == 0 ? null : m_stages[request.nextStage]);
        if (previous is not null) m_transitionUi = new() { sequence = owner.transitions.sequence, frame = previous };
    }

    private TransitionUiState CaptureTransitionUi() => stage.transitions.active && stage.transitions.scope == TransitionScope.Output
        && m_transitionUi.sequence == stage.transitions.sequence ? new() { sequence = m_transitionUi.sequence, frame = UI.Documents.UiFrameSnapshot.Capture(m_uiResources.Restore(m_transitionUi.frame)) } : new();

    private TransitionUiState PrepareTransitionUi(
        TransitionUiState snapshot,
        StageController candidate
    ) {
        if (!candidate.transitions.active || candidate.transitions.scope != TransitionScope.Output)
        {
            if (snapshot.sequence != 0) throw new InvalidOperationException("Saved UI has no matching output transition.");
            return new();
        }
        if (snapshot.sequence != candidate.transitions.sequence) throw new InvalidOperationException("Saved output transition is missing its exact previous UI image.");
        return new() { sequence = snapshot.sequence, frame = UI.Documents.UiFrameSnapshot.Capture(m_uiResources.Restore(snapshot.frame)) };
    }

    /// <summary>Releases the session-owned audio output.</summary>
    public void Dispose() { m_story?.Dispose(); m_checkpoints.Clear(); audio?.Dispose(); }

    private void RefreshCurrentHistory()
    {
        if (interactiveStory is not { status: StoryStatus.Dialogue } reading || m_history.Count == 0 || m_history[^1].nodeId != reading.nodeId) return;
        m_history[^1] = new() { nodeId = reading.nodeId, characterId = reading.characterId, speaker = reading.speaker, text = reading.dialogueText, contentId = CurrentContent(), voiceCueId = reading.voiceCueId };
    }

    private void RestoreAudio(
        AudioPlaybackSnapshot snapshot,
        StoryMachine candidate,
        bool languageChanged,
        StageController presentation
    )
    {
        if (audio is not null) audio.Restore(languageChanged && candidate.interactiveOwner is { status: StoryStatus.Dialogue } reading ? audio.RetargetReadingVoice(snapshot, reading.voiceCueId) : snapshot, NovelAudioPerformance.Mix(presentation));
        else if (snapshot.voices.Length > 0) throw new InvalidOperationException("Audio state cannot be restored without an output adapter.");
    }
}
