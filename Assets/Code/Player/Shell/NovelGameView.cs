using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Player.Binding;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.UI.Documents;
using Inno.VisualNovel.UI.Input;
using Inno.VisualNovel.UI.Theming;
using Inno.VisualNovel.Player.Persistence;
using Inno.VisualNovel.Player.Collections;
using Inno.VisualNovel.Player.Ports;
using Inno.VisualNovel.Player.Interface.Actions;
using Inno.VisualNovel.UI.Animation;

namespace Inno.VisualNovel.Player.Shell;

/// <summary>Routes editable game screens over one session with deterministic input ownership.</summary>
public sealed class NovelGameView : IDisposable
{
    private readonly NovelSession m_session;
    private readonly GameShellDataSource m_source;
    private readonly NovelUiActionHost m_uiActions;
    private readonly Interface.Data.NovelUiDataSourceHost m_dataSources;
    private readonly SavePageDataSource m_saveSource;
    private UiActionRequest? m_confirmation;
    private Dictionary<string, UiDocumentInstance> m_screens;
    private string m_theme;
    private string m_language;
    private bool m_highContrast;
    private readonly Stack<string> m_pages = new();
    private long m_revision;
    private readonly NovelProjectAsset m_project;
    private readonly Func<UiElementDefinition, string, (float width, float height)> m_measure;
    private readonly Inno.VisualNovel.UI.Text.UiTextMeasurement? m_measureRich;
    private readonly Func<INovelAudioOutput>? m_audioFactory;
    private readonly CollectionPageDataSource m_collections;
    private readonly MusicGalleryController m_music;
    private readonly HistoryPageDataSource m_history;
    private readonly AutomaticSaveController m_automaticSave;
    private ReplaySession? m_replay;
    private NovelGameView? m_replayView;
    private bool m_exitReplay;
    private string m_inputRole = string.Empty;
    private UiPageExit? m_outgoing;
    private bool m_pointerHeld;
    private bool m_discardPointer;
    /// <summary>Gets independent startup/global/settings IO, absent when the host has no native codec.</summary>
    public PlayerProfileController? profile { get; }
    private string ActiveRole => profile is { ready: false } ? "loading" : m_session.page;

    /// <summary>Instantiates the game's authored screen definitions.</summary>
    /// <param name="project">The imported game definition.</param>
    /// <param name="session">The isolated state owner.</param>
    /// <param name="measureText">The active engine text adapter.</param>
    /// <param name="saveService">An optional exact-build native storage service supplied by the host.</param>
    /// <param name="audioFactory">Creates an independent audio output owner for replay playback.</param>
    /// <param name="measureRichText">Optional width-aware text measurement shared with rendering.</param>
    /// <param name="profileStore">Optional native progress/settings persistence for this main game.</param>
    /// <param name="saveThumbnail">Optional exact-boundary image capture supplied by the rendering host.</param>
    public NovelGameView(
        NovelProjectAsset project,
        NovelSession session,
        Func<UiElementDefinition, string, (float width, float height)> measureText,
        SaveService? saveService = null,
        Func<INovelAudioOutput>? audioFactory = null,
        Inno.VisualNovel.UI.Text.UiTextMeasurement? measureRichText = null,
        GlobalProgressStore? profileStore = null,
        INovelSaveThumbnail? saveThumbnail = null
    ) {
        Model.Interface.Actions.UiActionContract.ValidateCatalog(project.uiActions);
        Model.Interface.Data.UiDataSourceContract.ValidateCatalog(project.uiDataSources);
        m_session = session;
        m_project = NovelProjectCopy.Copy(project); m_measure = measureText; m_audioFactory = audioFactory; m_measureRich = measureRichText;
        foreach (var binding in m_project.screens) binding.screen = UiDefinitionCopy.Copy(binding.screen ?? throw new ArgumentException("A system page is missing.", nameof(project)));
        m_revision = session.stateRevision;
        m_theme = session.themeId; m_language = session.localization.language; m_highContrast = session.preferences.highContrast; m_screens = CreateScreens();
        foreach (string role in new[] { "title", "reading", "pause", "settings", "history", "credits" })
            if (!m_screens.ContainsKey(role)) throw new ArgumentException("The project lacks its editable " + role + " page.", nameof(project));
        if (profileStore is not null && !m_screens.ContainsKey("loading")) throw new ArgumentException("Native profile startup requires an editable loading page.", nameof(project));
        profile = profileStore is null ? null : new(session, profileStore);
        persistence = saveService is null || session.isReplay ? null : new(session, saveService, saveThumbnail);
        m_automaticSave = new(session, persistence, project.autoSaveOnChoices);
        m_saveSource = new(persistence, project.manualSaveSlots, project.automaticSaveSlots);
        m_music = new(session); m_collections = new(session, m_music);
        m_history = new(session); m_source = new(session, m_saveSource, m_collections, m_history, profile);
        m_uiActions = new(m_project.uiActions, session.uiActions, session, m_source, Open);
        m_source.canInvoke = m_uiActions.CanInvoke; m_source.selectedAction = m_uiActions.IsSelected; m_source.actionError = () => m_uiActions.error;
        m_dataSources = new(m_project.uiDataSources, session.uiDataSources, session, m_source); m_source.customSources = m_dataSources;
        persistence?.Refresh();
        m_automaticSave.Observe();
    }

    /// <summary>Gets the owner-thread save coordinator, absent when the host has no native codec.</summary>
    public SaveSlotController? persistence { get; }
    /// <summary>Gets the displayed session, including an isolated replay when active.</summary>
    public NovelSession activeSession => m_replayView?.activeSession ?? m_session;
    /// <summary>Gets the latest source-addressed custom action diagnostic without throwing through the renderer.</summary>
    public string actionError => m_replayView?.actionError ?? m_uiActions.error;
    /// <summary>Gets the latest custom binding diagnostic, including its exact authored path.</summary>
    public string dataSourceError => m_replayView?.dataSourceError ?? m_dataSources.error;
    /// <summary>Advances only the displayed owner; the caller's narrative, stage and audio remain suspended during replay.</summary>
    /// <param name="seconds">The nonnegative logical time interval.</param>
    public void Tick(double seconds)
    {
        profile?.Poll(seconds);
        if (m_replayView is not null)
        {
            m_replayView.Tick(seconds);
            if (m_replayView.m_exitReplay || m_replay!.completed) ReturnReplay();
            return;
        }
        SynchronizeInputOwner();
        m_screens[ActiveRole].SetReducedMotion(m_session.preferences.reducedMotion);
        m_screens[ActiveRole].Tick(seconds);
        if (m_session.preferences.reducedMotion) m_outgoing = null;
        else if (m_outgoing is not null) { m_outgoing.Tick(seconds); if (m_outgoing.complete) m_outgoing = null; }
        if (profile is { ready: false }) return;
        m_session.Tick(seconds);
        m_automaticSave.Observe();
        if (m_session.page == "music") m_music.Tick();
    }

    /// <summary>Builds the currently selected authored screen.</summary>
    /// <returns>A detached UI frame.</returns>
    public UiFrame BuildFrame()
    {
        if (m_replayView is not null) return m_replayView.BuildFrame();
        Synchronize();
        SynchronizeInputOwner();
        m_source.pageRole = ActiveRole; m_session.pageState.Enter(ActiveRole);
        PreparePresentation(ActiveRole);
        UiFrame frame = m_screens[ActiveRole].BuildFrame(m_source);
        if (!m_session.preferences.reducedMotion && m_outgoing is { complete: false }) frame = m_outgoing.Composite(frame);
        m_session.PublishUiFrame(frame);
        return frame;
    }
    /// <summary>Reads the full focused input value for an owned native text-entry bridge while preserving ordinary page bindings.</summary>
    /// <param name="text">Receives the complete current literal value.</param>
    /// <returns>Whether the active page has a focused text input.</returns>
    public bool TryGetFocusedInputText(out string text)
    {
        if (m_replayView is not null) return m_replayView.TryGetFocusedInputText(out text);
        _ = BuildFrame(); return m_screens[ActiveRole].TryGetFocusedInputText(out text);
    }
    /// <summary>Releases view-owned pointer capture and held skipping after host focus loss.</summary>
    /// <param name="preserveTextEntry">Whether an explicit native text bridge retains the active page's modal query; other pages always release their overlays.</param>
    public void ReleaseInput(bool preserveTextEntry = false)
    {
        m_replayView?.ReleaseInput(preserveTextEntry);
        foreach (var screen in m_screens.Values) screen.CancelInput(preserveTextEntry && screen == m_screens[ActiveRole]);
        m_discardPointer |= m_pointerHeld; m_pointerHeld = false;
        m_session.SetSkippingHeld(false, false);
    }

    /// <summary>Freezes the current reading screen for a save image without polling IO, navigating or publishing it as the displayed page.</summary>
    /// <returns>A detached reading image from the exact current narrative boundary.</returns>
    public UiFrame CaptureReadingFrame()
    {
        if (m_replayView is not null) throw new InvalidOperationException("Ordinary save capture is unavailable during replay.");
        SynchronizeTheme();
        PreparePresentation("reading");
        string? previous = m_source.pageRole; m_source.pageRole = "reading";
        try { return m_screens["reading"].BuildFrame(m_source, false); }
        finally { m_source.pageRole = previous; }
    }

    /// <summary>Dispatches one input sample exactly once.</summary>
    /// <param name="input">Viewport-local design coordinates.</param>
    public void HandleInput(UiInputSnapshot input)
    {
        if (m_replayView is not null)
        {
            m_replayView.HandleInput(input);
            if (m_replayView.m_exitReplay || m_replay!.completed) ReturnReplay();
            return;
        }
        Synchronize();
        SynchronizeInputOwner();
        if (input.pressed) m_discardPointer = false;
        if (m_discardPointer)
        {
            m_session.SetSkippingHeld(input.skipHeld, false);
            if (input.released) { m_discardPointer = m_pointerHeld = false; return; }
            if (input.held) return;
            m_discardPointer = false;
        }
        m_pointerHeld = !input.released && (input.pressed || input.held);
        m_source.pageRole = ActiveRole; m_session.pageState.Enter(ActiveRole);
        PreparePresentation(ActiveRole);
        UiInputResult result = m_screens[ActiveRole].HandleInput(input, m_source);
        m_session.SetSkippingHeld(input.skipHeld, ActiveRole == "reading" && !input.composing && !input.cancel && !input.confirm && !input.space
            && !input.pressed && !input.released && input.readingShortcut.Length == 0 && result.actions.Count == 0 && !m_screens[ActiveRole].popupOpen && !m_screens[ActiveRole].textInputFocused);
        if (result.actions.Count > 0)
        {
            foreach (UiActionRequest action in result.actions) Dispatch(action);
            m_automaticSave.Observe();
            return;
        }
        if (profile is { ready: false }) return;
        if (input.composing) return;
        if (!result.consumed && !input.cancel && !input.confirm && !input.space && !input.pressed && !input.released
            && !m_screens[ActiveRole].popupOpen && !m_screens[ActiveRole].textInputFocused && TryReadingShortcut(input.readingShortcut))
        { m_automaticSave.Observe(); return; }
        if (input.cancel && !result.cancelConsumed)
        {
            if (m_session.page == "confirm") { m_confirmation = null; Back(); return; }
            if (m_session.interactiveStory?.status == Narrative.Execution.StoryStatus.Input) { m_session.CancelInput(); return; }
            if (m_session.page == "reading") Open("pause");
            else if (m_session.page != "title") Back();
            return;
        }
        if (!result.consumed && (input.confirm || input.space || input.released && input.pointerInside)) m_session.Advance();
        m_automaticSave.Observe();
    }

    private bool TryReadingShortcut(string action)
    {
        if (ActiveRole != "reading" || m_session.interactiveStory is null || m_session.interactiveStory.status is Narrative.Execution.StoryStatus.Input or Narrative.Execution.StoryStatus.Faulted
            || m_session.interactiveStory.debugPaused || m_session.hidden && action != "hide") return false;
        bool available = action switch
        {
            "automatic" or "skip" or "hide" => m_session.interactiveStory.status == Narrative.Execution.StoryStatus.Dialogue,
            "history" => m_screens.ContainsKey("history"),
            "rollback" => m_session.canRollback,
            "quickSave" => m_source.TryRead("saves.canSave", out var save) && save.boolean,
            "quickLoad" => m_source.TryRead("saves.ready", out var load) && load.boolean && persistence!.slots.Any(static item => item.slot == new SaveSlot(SaveSlotKind.Quick, 0)),
            _ => false
        };
        if (!available) return false;
        Dispatch(action == "history" ? new("open", "history") : new(action, string.Empty));
        return true;
    }

    private void Dispatch(
        UiActionRequest action,
        bool confirmed = false
    )
    {
        if (action.action == "profile.retry") { profile?.Retry(); m_source.confirmProfileDefaults = false; return; }
        if (action.action == "profile.defaults") { if (profile is { ready: false, loading: false, error.Length: > 0 }) m_source.confirmProfileDefaults = true; return; }
        if (action.action == "profile.defaults.cancel") { m_source.confirmProfileDefaults = false; return; }
        if (action.action == "profile.defaults.confirm")
        {
            if (m_source.confirmProfileDefaults) { profile?.UseDefaults(); m_source.confirmProfileDefaults = false; }
            return;
        }
        if (profile is { ready: false }) return;
        if (action.action == "page.reset") { m_session.pageState.Reset(m_session.page, action.argument); return; }
        if (action.action == "page.toggle")
        {
            if (!m_session.pageState.TryRead(m_session.page, action.argument, out var value) || value.kind != Model.Values.NovelValueKind.Boolean
                || !m_session.pageState.TryWrite(m_session.page, action.argument, Model.Values.NovelValue.Boolean(!value.boolean)))
                throw new InvalidOperationException("The page toggle target must be a declared Boolean field: " + action.argument);
            return;
        }
        if (!confirmed && action.action == "newGame" && m_session.interactiveStory is not null)
        { Ask(action, "开始新游戏？当前未保存的进度将被替换。"); return; }
        if (!confirmed && action.action == "title" && m_session.interactiveStory is not null)
        { Ask(action, "返回标题？请先保存需要保留的进度。"); return; }
        if (action.action.StartsWith("save.", StringComparison.Ordinal))
        {
            if (persistence is null || !m_saveSource.TrySlot(action.argument, out SaveSlot slot)) return;
            if (!confirmed && (action.action is "save.delete" or "save.load"
                || action.action == "save.write" && persistence.slots.Any(item => item.slot == slot)))
            { Ask(action, action.action == "save.delete" ? "删除此存档？此操作无法撤销。" : action.action == "save.load" ? "读取此存档并替换当前进度？" : "覆盖此槽位中的存档？"); return; }
            if (action.action == "save.write" && slot.kind == SaveSlotKind.Manual) persistence.Save(slot);
            else if (action.action == "save.load") persistence.Load(slot);
            else if (action.action == "save.delete") persistence.Delete(slot);
            return;
        }
        switch (action.action)
        {
            case "keyboard.reset": m_session.preferences.ResetKeyboardShortcuts(); break;
            case "image.zoomIn": m_screens[ActiveRole].ZoomImage(action.argument, 1); break;
            case "image.zoomOut": m_screens[ActiveRole].ZoomImage(action.argument, -1); break;
            case "image.reset": m_screens[ActiveRole].ResetImage(action.argument); break;
            case "collection.image.previous": m_collections.MoveImage(-1); m_screens[ActiveRole].ResetImageViews(); break;
            case "collection.image.next": m_collections.MoveImage(1); m_screens[ActiveRole].ResetImageViews(); break;
            case "collection.select":
                m_collections.Select(action.argument);
                var entry = m_session.collections.Get(action.argument);
                if (entry.kind == Model.Collections.CollectionKind.Replay) StartReplay(entry.id);
                else if (entry.kind == Model.Collections.CollectionKind.Music) m_music.Play(entry.id);
                else Open("collectionDetail");
                break;
            case "collections.previous": m_collections.Move(-1); break;
            case "collections.next": m_collections.Move(1); break;
            case "music.previous": m_music.Move(-1); break;
            case "music.next": m_music.Move(1); break;
            case "music.pause": m_music.TogglePause(); break;
            case "replay.exit": if (m_session.isReplay) m_exitReplay = true; break;
            case "newGame": m_pages.Clear(); m_session.StartNew(); break;
            case "advance": m_session.Advance(); break;
            case "choice": m_session.Choose(action.argument); break;
            case "input.submit": m_session.SubmitInput(); break;
            case "input.cancel": m_session.CancelInput(); break;
            case "command.cancel": m_session.CancelCommand(); break;
            case "language": m_session.SetLanguage(action.argument); break;
            case "theme": m_session.SetTheme(action.argument); break;
            case "open": Open(action.argument); break;
            case "resume": m_pages.Clear(); m_session.OpenPage(m_session.interactiveStory is null ? "title" : "reading"); break;
            case "back": Back(); break;
            case "title": m_pages.Clear(); m_session.OpenPage("title"); break;
            case "automatic": m_session.ToggleAutomatic(); break;
            case "skip": m_session.ToggleSkipping(); break;
            case "hide": m_session.ToggleHidden(); break;
            case "rollback": m_session.Rollback(); break;
            case "checkpoint.rollback": m_session.RollbackToCheckpoint(action.argument); break;
            case "history.voice":
                if (int.TryParse(action.argument, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int row)) m_session.ReplayVoice(row);
                break;
            case "history.rollback":
                if (int.TryParse(action.argument, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int target)) m_session.RollbackToHistory(target);
                break;
            case "history.previous": m_history.Move(-1); break;
            case "history.next": m_history.Move(1); break;
            case "slots.previous": m_saveSource.Move(-1); break;
            case "slots.next": m_saveSource.Move(1); break;
            case "slots.manual": m_saveSource.Select(SaveSlotKind.Manual); break;
            case "slots.automatic": m_saveSource.Select(SaveSlotKind.Automatic); break;
            case "slots.quick": m_saveSource.Select(SaveSlotKind.Quick); break;
            case "quickSave": persistence?.Save(new(SaveSlotKind.Quick, 0)); break;
            case "continue":
                if (m_session.page != "title" && !confirmed) { Ask(action, "读取最近存档并替换当前进度？"); return; }
                persistence?.ContinueLatest(); break;
            case "quickLoad":
                if (!confirmed) { Ask(action, "读取快速存档并替换当前进度？"); return; }
                persistence?.Load(new(SaveSlotKind.Quick, 0)); break;
            case "confirm.accept":
                UiActionRequest? pending = m_confirmation; m_confirmation = null; Back();
                if (pending is not null) Dispatch(pending, true);
                break;
            case "confirm.cancel": m_confirmation = null; Back(); break;
            default:
                if (action.action.StartsWith("custom.", StringComparison.Ordinal)) m_uiActions.Invoke(action);
                else throw new InvalidOperationException("UI action is not registered: " + action.action);
                break;
        }
    }

    private void Open(string role)
    {
        if (!m_screens.ContainsKey(role)) throw new InvalidOperationException("The requested page is missing: " + role);
        if (m_screens.TryGetValue(ActiveRole, out var previous)) previous.CancelInput();
        if (role == "collectionDetail") m_screens[role].ResetImageViews();
        if (m_session.page == "music" && role != "music") m_music.Leave();
        if (role is "gallery" or "music" or "replays" or "endings" or "glossary" or "characters") m_collections.Open(role);
        if (role == "history") { m_history.Open(); m_screens[role].FollowScrollEnds(); m_screens[role].RequestInitialFocus(); }
        m_pages.Push(m_session.page);
        m_session.OpenPage(role);
        if (role is "save" or "load") persistence?.Refresh();
    }

    private void Ask(
        UiActionRequest action,
        string message
    ) {
        if (!m_screens.ContainsKey("confirm")) throw new InvalidOperationException("This action requires an editable confirm screen.");
        m_confirmation = action; m_source.confirmation = message; Open("confirm");
    }

    private void Back()
    {
        if (m_screens.TryGetValue(ActiveRole, out var previous)) previous.CancelInput();
        if (m_session.page == "music") m_music.Leave();
        m_session.OpenPage(m_pages.TryPop(out string? role) ? role : m_session.interactiveStory is null ? "title" : "reading");
    }

    private void StartReplay(string id)
    {
        if (m_session.isReplay) throw new InvalidOperationException("Nested replay is unavailable.");
        var owner = new ReplaySession(m_project, m_session, id, m_audioFactory?.Invoke());
        try { m_replayView = new(m_project, owner.session, m_measure, measureRichText: m_measureRich); m_replay = owner; }
        catch { owner.Return(); throw; }
    }
    private void ReturnReplay()
    {
        m_replay!.Return(); m_replayView!.Dispose(); m_replayView = null; m_replay = null;
    }

    private void Synchronize()
    {
        profile?.Poll();
        persistence?.Poll();
        m_automaticSave.Observe();
        SynchronizeTheme();
        if (m_revision == m_session.stateRevision) return;
        UiPageExit? outgoing = null;
        if (m_inputRole != ActiveRole && m_screens.TryGetValue(m_inputRole, out var previous))
        {
            previous.SetReducedMotion(m_session.preferences.reducedMotion);
            outgoing = previous.BeginExit();
        }
        foreach (var screen in m_screens.Values) screen.Enter();
        m_outgoing = outgoing; m_inputRole = ActiveRole;
        m_discardPointer |= m_pointerHeld;
        m_revision = m_session.stateRevision; m_pages.Clear(); m_confirmation = null;
    }

    private Dictionary<string, UiDocumentInstance> CreateScreens() => NovelThemeScreens.Resolve(m_project.screens, m_session.activeTheme).ToDictionary(static binding => binding.role, binding =>
    {
        ScreenAsset screen = UiDefinitionCopy.Copy(binding.screen ?? throw new InvalidOperationException("A themed system page is missing: " + binding.role));
        screen.font ??= m_project.font;
        if (m_session.localization.resources.primaryFont is { } languageFont)
        {
            screen.font = languageFont;
            foreach (var row in UiElementTree.Enumerate(screen.root)) row.element.font = languageFont;
        }
        var document = new UiDocumentInstance(screen, m_measure, m_measureRich, theme: m_session.activeTheme);
        document.SetHighContrast(m_session.preferences.highContrast); return document;
    }, StringComparer.Ordinal);

    private void SynchronizeTheme()
    {
        if (m_theme == m_session.themeId && m_language == m_session.localization.language && m_highContrast == m_session.preferences.highContrast) return;
        var candidate = CreateScreens();
        if (!candidate.ContainsKey(ActiveRole) || m_pages.Any(role => !candidate.ContainsKey(role)))
            throw new InvalidOperationException("The selected theme cannot preserve the active modal navigation.");
        string? previousRole = m_source.pageRole;
        try
        {
            foreach (var page in candidate)
            {
                m_source.pageRole = page.Key;
                page.Value.SetFontScale((float)m_session.preferences.fontScale);
                page.Value.BuildFrame(m_source);
                if (m_screens.TryGetValue(page.Key, out var previous)) page.Value.Focus(previous.focusedElement);
            }
        }
        finally { m_source.pageRole = previousRole; }
        foreach (var previous in m_screens.Values) previous.CancelInput();
        m_outgoing = null;
        m_discardPointer |= m_pointerHeld;
        m_screens = candidate; m_theme = m_session.themeId; m_language = m_session.localization.language; m_highContrast = m_session.preferences.highContrast;
    }

    private void SynchronizeInputOwner()
    {
        if (m_inputRole == ActiveRole) return;
        if (m_screens.TryGetValue(m_inputRole, out var previous))
        {
            previous.SetReducedMotion(m_session.preferences.reducedMotion);
            m_outgoing = previous.BeginExit();
        }
        m_screens[ActiveRole].Enter();
        m_discardPointer |= m_pointerHeld; m_inputRole = ActiveRole;
    }

    private void PreparePresentation(string role)
    {
        var document = m_screens[role];
        document.SetFontScale((float)m_session.preferences.fontScale);
        document.SetReducedMotion(m_session.preferences.reducedMotion);
        document.SetPerformanceValues(m_session.uiPerformance.GetValues(role, m_session.stage.performances.Evaluate()));
    }

    /// <summary>Cancels pending view-owned IO; the separately owned narrative session remains with its host.</summary>
    public void Dispose()
    {
        m_replayView?.Dispose(); m_replay?.Dispose(); m_replayView = null; m_replay = null;
        persistence?.Dispose();
        profile?.Dispose();
        m_uiActions.Dispose();
        m_dataSources.Dispose();
        foreach (var screen in m_screens.Values) screen.CancelInput();
        m_outgoing = null;
    }
}
