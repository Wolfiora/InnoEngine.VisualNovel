using System;
using System.Linq;
using System.Numerics;
using InnoEngine.Assets;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using InnoEditor.Rendering;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.Diagnostics;
using Inno.VisualNovel.Authoring.Preview;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Editor.Presentation;
using Inno.VisualNovel.Integration.Rendering;
using Inno.VisualNovel.Integration.Services;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Narrative.Debugging;
using Inno.VisualNovel.Narrative.Values;
using Inno.VisualNovel.Player.Audio;
using Inno.VisualNovel.Player.Ports;
using Inno.VisualNovel.UI.Input;

namespace Inno.VisualNovel.Editor.Debugging;

internal sealed class StoryPreviewView : IDisposable
{
    private readonly StoryDocumentsModule m_stories;
    private readonly ResourceDocumentsModule m_resources;
    private readonly EditorInteractions m_interactions;
    private readonly StoryInspectorFields m_fields;
    private readonly ResourceDocumentToolbar m_toolbar;
    private readonly NovelValueView m_values;
    private readonly NovelPreviewCanvas m_canvas;
    private readonly PreviewCanvasInput m_canvasInput = new();
    private readonly StoryDebuggerView m_debugView;
    private readonly INovelStateCodec m_stateCodec;
    private StoryDebugger? m_debugger;
    private NovelProjectAsset? m_context;
    private NovelPreviewCaseAsset m_temporary = new();
    private NovelPreviewSession? m_preview;
    private PreviewFreshnessTracker? m_freshness;
    private Guid m_previewCaseDocument;
    private bool m_paused, m_canvasFocus;
    private bool m_textBridgeActive;
    private bool m_visible;
    private string m_message = string.Empty;
    private int m_inputSourceCount, m_unsavedInputCount;
    private AssetPath? m_previewSource;
    internal StoryDebugger? debugger => m_preview is null ? null : m_debugger;
    internal StoryDebugLocation? debugLocation => m_preview is null ? null : m_debugger?.paused == true ? m_debugger.location : m_preview.session.interactiveStory?.debugLocation;
    internal bool executionPaused => m_paused || m_debugger?.paused == true || m_preview?.holdingRoute == true;
    internal bool ContainsSource(string path) => m_preview is not null && (m_previewSource?.ToString() == path || m_context?.chapters.Any(chapter => chapter.assetPath.ToString() == path) == true);
    private Preparation? m_preparation;
    private sealed record Preparation(
        PreviewProjectPreparation operation,
        StoryDraftState draft,
        NovelPreviewCaseAsset test,
        Guid caseDocument,
        NativeDocumentStamp? chapterStamp,
        NativeDocumentStamp? caseStamp
    );

    internal StoryPreviewView(
        StoryDocumentsModule stories,
        ResourceDocumentsModule resources,
        EditorInteractions interactions,
        StoryInspectorFields fields,
        IEditorPreviewService previews,
        INovelStateCodec stateCodec
    ) {
        m_stories = stories; m_resources = resources; m_interactions = interactions; m_fields = fields; m_stateCodec = stateCodec;
        m_toolbar = new(resources, interactions, fields.Finish); m_values = new(fields); m_canvas = new(previews);
        m_debugView = new(fields);
    }

    internal void Draw(
        StoryDraftState draft,
        string selected,
        Action<string> locate
    ) {
        SetVisible(true);
        if (ImGui.Button("Use selected game as preview context"))
        {
            m_context = NovelResourceSelection.Get<NovelProjectAsset>(m_interactions) ?? throw new InvalidOperationException("Select an .ivnproject game definition in File Browser.");
            Stop();
        }
        if (m_context is null)
        {
            EditorUiSession.current.widgets.Hint("Select a game first. Preview uses its resources and screens and compiles the current unsaved chapter. It does not read player saves or execute external side effects."); return;
        }
        ImGui.Text("Context · " + m_context.title);
        bool nativeCase = m_toolbar.Draw<NovelPreviewCaseAsset>("Preview case", ".ivntest", static () => new(), out var stored);
        NovelPreviewCaseAsset test = nativeCase ? stored! : m_temporary;
        bool changed = Settings(draft, selected, test);
        changed |= m_debugView.Settings(test, draft.chapterId, selected, draft.GetVariables());
        if (changed)
        {
            if (nativeCase) m_resources.Edit<NovelPreviewCaseAsset>(m_toolbar.documentId, "Edit preview case", target => CopySettings(test, target), m_fields.mergeKey);
            else m_temporary = test;
        }
        Guid caseDocument = nativeCase ? m_toolbar.documentId : Guid.Empty;
        if (m_preparation is { } pending && (caseDocument != pending.caseDocument || changed))
            CancelPreparation("Preview settings changed. Run the preview again.");
        if (m_preview is not null && (caseDocument != m_previewCaseDocument || changed && !nativeCase))
            m_freshness?.Invalidate("The current preview case or temporary settings changed.");
        if (ImGui.Button("Run this preview"))
            StartPreview(draft, test, caseDocument);
        if (DrawPreparation()) return;
        if (m_message.Length > 0) EditorUiSession.current.widgets.Hint(m_message);
        if (m_preview is null) return;
        ImGui.Text(string.Format(System.Globalization.CultureInfo.InvariantCulture,"Preview inputs · {0} resources, including {1} from current unsaved documents.", m_inputSourceCount, m_unsavedInputCount));
        ImGui.SameLine(); if (ImGui.Button("Stop preview")) { Stop(); return; }
        ImGui.SameLine(); if (ImGui.Button(m_paused ? "Resume time" : "Pause time")) m_paused = !m_paused;
        m_debugView.Draw(m_debugger!, m_preview, test, source => LocateSource(source, locate));
        TickPreview();
        if (m_paused && ImGui.Button("Step time by 1/60 second"))
        {
            m_preview.session.audio?.SetSuspended(false); m_preview.StepTime(1.0 / 60); m_preview.session.audio?.SetSuspended(true);
        }
        if (m_preview.routeRunning && ImGui.Button("Pause route playback")) m_preview.StopRoute();
        if (m_preview.holdingRoute && ImGui.Button("Continue playing from this state")) m_preview.ContinuePlaying();
        m_preview.session.audio?.SetSuspended(m_paused || m_preview.holdingRoute || m_debugger!.paused);
        if (m_preview.error.Length > 0) EditorUiSession.current.widgets.Hint(m_preview.failure is { } cause ? (cause).Message : m_preview.error);
        if (m_preview.targetReached) ImGui.Text("Route target reached. Inspect this state or continue playing manually.");
        var story = m_preview.session.interactiveStory!;
        ImGui.Text(story.status + " · " + story.sourceChapterId + "/" + story.sourceNodeId);
        if (story.status == StoryStatus.Faulted) EditorUiSession.current.widgets.Hint(story.error);
        if (ImGui.Button("Locate current node")) LocateSource(story.debugLocation, locate);
        RuntimeInput(story);
        DrawCanvas();
        if (ImGui.CollapsingHeader("Session variables (current preview only)"))
        {
            StoryMachine observed = m_debugger?.stoppedTask ?? story;
            foreach (var variable in observed.InspectVariables())
            {
                NovelValue value = variable.value;
                if (m_values.Draw("preview-observe/" + variable.id, ref value, false))
                { observed.SetVariable(variable.id, value); m_preview.InvalidateRouteRecording("Session variables were changed in the debugger. Save explicit local initial values, or record the route again from its entry."); }
            }
        }
        if (m_freshness?.stale != true && (story.status is StoryStatus.Dialogue or StoryStatus.Choice or StoryStatus.Input or StoryStatus.AdvanceWait)
            && ImGui.Button("Record the played route in this case") && ConfirmWriteBack())
        {
            var recorded = m_preview.CaptureRoute(); m_fields.Finish();
            if (nativeCase) m_resources.Edit<NovelPreviewCaseAsset>(m_toolbar.documentId, "Record played route", target => CopySettings(recorded, target)); else m_temporary = recorded;
        }
        if (m_preview.recordingError.Length > 0) EditorUiSession.current.widgets.Hint(m_preview.recordingError);
        if (m_freshness?.stale != true && story.canSave && ImGui.Button("Store current variables and stage in this case") && ConfirmWriteBack())
        {
            var current = story.Capture(); test.initialValues = current.gameVariables.Concat(current.localVariables).Concat(m_preview.session.CaptureProgress().variables).ToArray();
            test.stage = m_preview.session.stage.Capture(); test.useStageSnapshot = true; test.mode = PreviewRunMode.Local; test.nodeId = story.sourceNodeId;
            if (story.sourceChapterId != draft.chapterId) throw new InvalidOperationException("Open the current node's chapter before saving a local preview case.");
            m_fields.Finish();
            if (nativeCase) m_resources.Edit<NovelPreviewCaseAsset>(m_toolbar.documentId, "Save preview initial state", target => CopySettings(test, target)); else m_temporary = test;
        }
    }

    private bool Settings(
        StoryDraftState draft,
        string selected,
        NovelPreviewCaseAsset test
    ) {
        bool changed = false; PreviewRunMode mode = test.mode;
        changed |= m_fields.SelectEnum("preview/mode", "Preview mode", ref mode); test.mode = mode;
        string chapter = test.chapterId, entry = test.entryId, node = test.nodeId;
        changed |= m_fields.Select("preview/chapter", "Starting chapter", ref chapter, new[] { (string.Empty, "Game's default chapter") }
            .Concat(m_context!.chapters.Select(static item => (item.chapterId, item.title)))); test.chapterId = chapter;
        var source = m_context.chapters.SingleOrDefault(item => item.chapterId == (chapter.Length == 0 ? m_context.startChapter : chapter));
        changed |= m_fields.Select("preview/entry", "Public entry", ref entry, new[] { (string.Empty, "Game's default entry") }
            .Concat(source?.program?.entries.Select(static item => (item.id, item.id)) ?? [])); test.entryId = entry;
        if (mode != PreviewRunMode.Complete)
        {
            if (m_fields.Text("preview/node", mode == PreviewRunMode.Local ? "Local starting node" : "Interaction target", ref node)) { test.nodeId = node; test.targetInteraction = 0; changed = true; }
            if (selected.Length > 0 && ImGui.Button("Use selected node as start / target")) { m_fields.Finish(); test.nodeId = selected; test.targetInteraction = 0; changed = true; }
            EditorUiSession.current.widgets.Hint(mode == PreviewRunMode.Local ? "Local preview uses the variables and stage specified here. Unspecified variables use their declared defaults. Past story state is not reconstructed." : "Replay recorded decisions from a public entry to a dialogue, choice or input node in the current chapter.");
        }
        if (mode == PreviewRunMode.Route)
        {
            ImGui.Text("Target interaction sequence · " + test.targetInteraction + " (0 uses the first match)");
            if (test.targetInteraction > 0 && ImGui.Button("Use the first matching target")) { m_fields.Finish(); test.targetInteraction = 0; changed = true; }
            string choices = string.Join('\n', test.choices);
            if (m_fields.MultilineText("preview/choices", "Route choice IDs (one per line)", ref choices)) { test.choices = choices.Split('\n').Where(static item => item.Length > 0).ToArray(); changed = true; }
            double maximum = test.maximumSeconds;
            changed |= m_fields.Number("preview/maximum", "Maximum route duration (logical seconds)", ref maximum, 0.1, 3600); test.maximumSeconds = maximum;
            int removeInput = -1;
            for (int index = 0; index < test.inputs.Length; index++)
            {
                PreviewInput input = test.inputs[index]; string prefix = "preview/input/" + index;
                string chapterId = input.chapterId, nodeId = input.nodeId, text = input.text;
                changed |= m_fields.Text(prefix + "/chapter", "Input chapter ID", ref chapterId); input.chapterId = chapterId;
                changed |= m_fields.Text(prefix + "/node", "Input node ID", ref nodeId); input.nodeId = nodeId;
                bool cancel = input.cancel; changed |= m_fields.Toggle(prefix + "/cancel", "Cancel this input", ref cancel); input.cancel = cancel;
                if (!cancel) { changed |= m_fields.Text(prefix + "/text", "Test input text", ref text); input.text = text; }
                if (ImGui.Button("Remove this input step##" + prefix)) removeInput = index;
            }
            if (removeInput >= 0) { m_fields.Finish(); test.inputs = test.inputs.Where((_, index) => index != removeInput).ToArray(); changed = true; }
            if (selected.Length > 0 && draft.graph.document.FindNode(new(selected))?.definitionId == Model.Story.StoryNodeIds.input && ImGui.Button("Add test text for the selected input node"))
            { m_fields.Finish(); test.inputs = [.. test.inputs, new() { chapterId = draft.chapterId, nodeId = selected }]; changed = true; }
        }
        if (ImGui.CollapsingHeader("Preview initial variable values"))
        {
            foreach (var definition in draft.GetVariables())
            {
                var initial = test.initialValues.SingleOrDefault(item => item.id == definition.id);
                bool enabled = initial is not null;
                if (m_fields.Toggle("preview/value/" + definition.id + "/enabled", definition.name + " · " + definition.id, ref enabled))
                {
                    m_fields.Finish(); test.initialValues = enabled ? [.. test.initialValues, new() { id = definition.id, value = definition.initialValue.Copy() }]
                        : test.initialValues.Where(item => item.id != definition.id).ToArray(); changed = true;
                }
                if (!enabled) continue;
                NovelValue value = initial?.value ?? definition.initialValue.Copy();
                if (m_values.Draw("preview/value/" + definition.id, ref value, false, definition.members))
                { test.initialValues.Single(item => item.id == definition.id).value = value; changed = true; }
            }
        }
        bool snapshot = test.useStageSnapshot;
        changed |= m_fields.Toggle("preview/stage-snapshot", "Use the saved stage snapshot", ref snapshot); test.useStageSnapshot = snapshot;
        return changed;
    }

    private void RuntimeInput(StoryMachine story)
    {
        if (story.status != StoryStatus.Input) return;
        string text = story.inputText;
        if (ImGui.InputText("Test input text##preview-live-input", ref text, 32768)) story.SetInputText(text);
        if (story.isInputValid && ImGui.Button("Submit test input")) { m_preview!.session.SubmitInput(); m_preview.ObserveInteraction(); }
    }
    private void LocateSource(
        StoryDebugLocation source,
        Action<string> select
    ) {
        Guid previous = m_stories.activeDocumentId;
        try
        {
            var context = m_context ?? throw new AuthoringValidationException($"Select an explicit game context before navigating across debug chapters.");
            if (m_freshness is not null && !m_freshness.CheckAll()) throw new AuthoringValidationException($"Preview sources changed. Run preview again before navigating.");
            var snapshot = PreviewSourceNavigator.Resolve(context, m_stories, source.chapter, source.node, m_previewSource);
            m_fields.Finish(); m_stories.Open(snapshot.path);
            if (m_stories.Inspect(snapshot.path).fingerprint != snapshot.fingerprint)
                throw new AuthoringValidationException($"The debug source changed during navigation. Locate it again.");
            select(source.node); m_message = string.Empty;
        }
        catch (Exception failure) when (failure is not OutOfMemoryException)
        { if (previous != Guid.Empty) m_stories.activeDocumentId = previous; m_message = (failure).Message; }
    }
    internal void DrawCompact(
        StoryDraftState draft,
        Action<string> locate,
        Action settings
    ) {
        SetVisible(true);
        if (ImGui.SmallButton("Preview settings and debugging")) { m_fields.Finish(); settings(); }
        if (m_preparation is not null && DrawPreparation()) return;
        if (m_message.Length > 0) EditorUiSession.current.widgets.Hint(m_message);
        if (m_preview is null)
        {
            if (ImGui.Button("Use selected game"))
                m_context = NovelResourceSelection.Get<NovelProjectAsset>(m_interactions) ?? throw new InvalidOperationException("Select an .ivnproject game definition as the explicit preview context.");
            if (m_context is null) { EditorUiSession.current.widgets.Hint("Choose a game definition to preview from its actual entry. Configure local initial values and route cases in preview settings."); return; }
            if (ImGui.Button("Run from game entry")) StartPreview(draft, new() { mode = PreviewRunMode.Complete });
            if (m_preparation is not null) return;
            if (m_preview is null) return;
        }
        ImGui.SameLine(); if (ImGui.SmallButton(m_paused ? "Continue" : "Pause")) m_paused = !m_paused;
        ImGui.SameLine(); if (ImGui.SmallButton("Stop")) { Stop(); return; }
        TickPreview();
        var story = m_preview.session.interactiveStory!;
        if (m_preview.holdingRoute && ImGui.SmallButton("Continue playing")) m_preview.ContinuePlaying();
        if (m_preview.routeRunning && ImGui.SmallButton("Pause route")) m_preview.StopRoute();
        if (ImGui.SmallButton("Locate current instruction")) LocateSource(story.debugLocation, locate);
        if (m_debugger?.paused == true) EditorUiSession.current.widgets.Hint("Paused at a breakpoint. Open preview settings to continue or step.");
        if (m_preview.error.Length > 0) EditorUiSession.current.widgets.Hint(m_preview.failure is { } cause ? (cause).Message : m_preview.error);
        if (story.status == StoryStatus.Faulted) EditorUiSession.current.widgets.Hint(story.error);
        RuntimeInput(story); DrawCanvas(true);
    }
    private void StartPreview(
        StoryDraftState draft,
        NovelPreviewCaseAsset test,
        Guid caseDocument = default
    ) {
        m_fields.Finish();
        var context = m_context ?? throw new InvalidOperationException("Choose a game definition for preview first.");
        CancelPreparation(string.Empty);
        var frozenCase = InnoEditor.Assets.EditorAssets.DecodeNative<NovelPreviewCaseAsset>(InnoEditor.Assets.EditorAssets.EncodeNative(test));
        var frozenDraft = m_stories.Read(m_stories.activeDocumentId);
        bool includeDraft = test.mode == PreviewRunMode.Local || context.chapters.Any(item => item.chapterId == frozenDraft.chapterId);
        m_preparation = new(PreviewDraftProjectBuilder.Begin(context, m_stories, m_resources, includeDraft ? frozenDraft.ResolveVariableTable() : null),
            frozenDraft, frozenCase, caseDocument, includeDraft ? m_stories.GetStamp(m_stories.activeDocumentId) : null,
            caseDocument == Guid.Empty ? null : m_resources.GetStamp(caseDocument));
        m_preview?.session.audio?.SetSuspended(true); m_message = string.Empty; m_canvasFocus = false;
    }
    private bool DrawPreparation()
    {
        if (m_preparation is not { } pending) return false;
        if (pending.chapterStamp is { } chapter && m_stories.GetStamp(chapter.documentId) != chapter
            || pending.caseStamp is { } test && m_resources.GetStamp(test.documentId) != test)
        { CancelPreparation("The preview chapter or case was edited, saved or closed during preparation. Run it again."); return false; }
        ImGui.Text(pending.operation.progress + " · Captured " + pending.operation.capturedSources + " resources");
        if (ImGui.Button("Cancel preview preparation")) { CancelPreparation("Preparation cancelled. The previous preview is retained."); return false; }
        if (!pending.operation.Advance()) return true;
        m_preparation = null;
        using var completed = pending.operation;
        if (completed.result is not { } prepared) { m_message = completed.failure is { } cause ? (cause).Message : completed.error; return false; }
        NovelPreviewSession? candidate = null;
        try
        {
            var debug = new StoryDebugger(); debug.SetBreakpoints(pending.test.breakpoints.Where(static item => item.enabled).Select(static item => item.Build()));
            var freshness = new PreviewFreshnessTracker(prepared, m_stories, m_resources, pending.chapterStamp?.documentId ?? Guid.Empty, pending.caseDocument);
            if (!freshness.CheckAll()) throw new InvalidOperationException(freshness.reason);
            var audio = new InnoNovelAudio(NovelAudioLibrary.Create(prepared.project));
            candidate = new(prepared.project, pending.draft, m_stories.compiler, pending.test, NovelPresentationFrame.Measure,
                audio, NovelPresentationFrame.MeasureRichText, debugger: debug, stateCodec: m_stateCodec,
                variableTable: pending.chapterStamp is null ? null : prepared.GetVariableTable(pending.draft.variableTableId));
            Stop(); m_preview = candidate; candidate = null; m_debugger = debug; m_freshness = freshness;
            m_previewSource = pending.chapterStamp is { } stamp ? AssetPath.Parse(stamp.path) : null;
            m_previewCaseDocument = pending.caseDocument; m_paused = false; m_message = string.Empty;
            m_inputSourceCount = prepared.sources.Count; m_unsavedInputCount = prepared.unsavedCount;
        }
        catch (Exception failure) when (failure is not OutOfMemoryException)
        { candidate?.Dispose(); m_message = (failure).Message; }
        return false;
    }
    private void CancelPreparation(string message)
    { m_preparation?.operation.Dispose(); m_preparation = null; m_message = message; }
    private void TickPreview()
    {
        m_freshness?.Probe();
        ShowFreshness();
        m_preview!.session.audio?.SetSuspended(m_paused || m_preview.holdingRoute || m_debugger?.paused == true);
        if (!m_canvasFocus || !ImGui.IsWindowFocused() || ImGui.GetInput().wantsText || m_paused)
            m_preview.view.ReleaseInput(m_textBridgeActive && ImGui.IsWindowFocused() && !m_paused);
        if (!m_paused) m_preview.Tick(Math.Clamp(ImGui.GetInput().deltaTime, 0, .25));
        if (m_preview.session.rollbackDiagnostic.Length > 0) EditorUiSession.current.widgets.Hint(m_preview.session.rollbackDiagnostic);
    }
    private bool ConfirmWriteBack()
    {
        bool current = m_freshness?.CheckAll() == true;
        if (!current) ShowFreshness();
        return current;
    }
    private void ShowFreshness()
    {
        if (m_freshness?.stale != true) return;
        string message = m_freshness.reason + " Run the preview again before recording a route or updating the case. The current session still uses its previously prepared story and screens.";
        if (m_preview!.recordingError.Length == 0) m_preview.InvalidateRouteRecording(message);
        EditorUiSession.current.widgets.Hint(message);
    }
    private void DrawCanvas(bool compact = false)
    {
        var owner = m_preview!; var frame = owner.view.BuildFrame(); Vector2 available = ImGui.GetContentRegionAvailable();
        float width = owner.project.width, height = owner.project.height;
        float reserved = !compact || owner.view.TryGetFocusedInputText(out _) ? 90 : 0;
        float scale = Math.Max(0.01f, Math.Min(available.X / width, Math.Max(compact ? 1 : 180, available.Y - reserved) / height));
        Vector2 size = new(width * scale, height * scale), origin = ImGui.GetCursorScreenPosition();
        m_canvas.Draw(frame, size, owner.session.BuildStageFrame(width, height), width, height, owner.session.transitionUi);
        Vector2 mouse = ImGui.GetMousePos(), local = (mouse - origin) / scale;
        bool inside = ImGui.IsWindowHovered() && local.X >= 0 && local.Y >= 0 && local.X < width && local.Y < height;
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left)) m_canvasFocus = inside;
        if (!ImGui.IsWindowFocused()) m_canvasFocus = false;
        owner.HandleInput(m_canvasInput.Map(local, inside, m_canvasFocus && !m_paused && m_debugger?.paused != true, owner.project.width, owner.project.height, owner.session.preferences)
            .InCanvas(Inno.VisualNovel.UI.Layout.UiCanvasFit.Create(frame.width, frame.height, new(0, 0, width, height))));
        m_textBridgeActive = false;
        if (owner.view.TryGetFocusedInputText(out string current))
        {
            bool changed = m_fields.Text("preview/live-input", "Preview text input", ref current);
            m_textBridgeActive = ImGui.IsItemActive();
            if (changed)
                owner.HandleInput(new() { selectAll = true, delete = current.Length == 0, text = current.Length == 0 ? [] : [current] });
            if (owner.session.page == "reading" && owner.session.interactiveStory is { status: StoryStatus.Input } playerInput)
            {
                ImGui.BeginDisabled(!playerInput.isInputValid);
                if (ImGui.Button("Submit preview input")) { m_fields.Finish(); owner.HandleInput(new() { textConfirm = true }); }
                ImGui.EndDisabled();
            }
        }
    }
    internal void SetVisible(bool visible)
    {
        if (m_visible == visible) return;
        m_visible = visible; if (!visible) { m_canvasFocus = m_textBridgeActive = false; m_preview?.view.ReleaseInput(); }
        if (m_preview is { } owner) owner.session.audio?.SetSuspended(!visible || m_preparation is not null || m_paused || owner.holdingRoute || m_debugger?.paused == true);
    }
    internal void Stop() { CancelPreparation(string.Empty); m_preview?.Dispose(); m_preview = null; m_debugger = null; m_freshness = null; m_previewSource = null; m_previewCaseDocument = Guid.Empty; m_canvasFocus = m_textBridgeActive = false; m_canvasInput.Reset(); }
    public void Dispose() { Stop(); m_canvas.Dispose(); m_context = null; }
    private static void CopySettings(NovelPreviewCaseAsset source, NovelPreviewCaseAsset target)
    {
        target.title = source.title; target.mode = source.mode; target.chapterId = source.chapterId; target.entryId = source.entryId; target.nodeId = source.nodeId;
        target.targetInteraction = source.targetInteraction;
        target.initialValues = source.initialValues; target.useStageSnapshot = source.useStageSnapshot; target.stage = source.stage;
        target.choices = source.choices; target.inputs = source.inputs; target.maximumSeconds = source.maximumSeconds; target.stepSeconds = source.stepSeconds;
        target.breakpoints = source.breakpoints.Select(static item => item.Copy()).ToArray();
    }
}
