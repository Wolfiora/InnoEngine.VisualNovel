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
        if (ImGui.Button(m_fields.Label("使用所选游戏作为预览上下文")))
        {
            m_context = NovelResourceSelection.Get<NovelProjectAsset>(m_interactions) ?? throw new InvalidOperationException(m_fields.Localize("在 File Browser 中选择游戏定义 .ivnproject。"));
            Stop();
        }
        if (m_context is null)
        {
            EditorUiSession.current.widgets.Hint(m_fields.Localize("先选择游戏定义。预览会读取它的资源与界面，并编译当前未保存的章节；不读取玩家存档或执行外部副作用。")); return;
        }
        ImGui.Text(m_fields.Localize("上下文 · ") + m_context.title);
        bool nativeCase = m_toolbar.Draw<NovelPreviewCaseAsset>("预览用例", ".ivntest", static () => new(), out var stored);
        NovelPreviewCaseAsset test = nativeCase ? stored! : m_temporary;
        bool changed = Settings(draft, selected, test);
        changed |= m_debugView.Settings(test, draft.chapterId, selected, draft.GetVariables());
        if (changed)
        {
            if (nativeCase) m_resources.Edit<NovelPreviewCaseAsset>(m_toolbar.documentId, m_fields.Localize("编辑预览用例"), target => CopySettings(test, target), m_fields.mergeKey);
            else m_temporary = test;
        }
        Guid caseDocument = nativeCase ? m_toolbar.documentId : Guid.Empty;
        if (m_preparation is { } pending && (caseDocument != pending.caseDocument || changed))
            CancelPreparation("预览设置已变化，请重新运行。");
        if (m_preview is not null && (caseDocument != m_previewCaseDocument || changed && !nativeCase))
            m_freshness?.Invalidate("当前预览用例或临时设置已变化。");
        if (ImGui.Button(m_fields.Label("运行此预览")))
            StartPreview(draft, test, caseDocument);
        if (DrawPreparation()) return;
        if (m_message.Length > 0) EditorUiSession.current.widgets.Hint(m_message);
        if (m_preview is null) return;
        ImGui.Text(m_fields.Format("预览输入 · {0} 项资源，其中 {1} 项来自当前未保存文档。", m_inputSourceCount, m_unsavedInputCount));
        ImGui.SameLine(); if (ImGui.Button(m_fields.Label("停止预览"))) { Stop(); return; }
        ImGui.SameLine(); if (ImGui.Button(m_fields.Caption(m_paused ? "继续时间" : "暂停时间", m_paused ? m_fields.Localize("继续时间") : m_fields.Localize("暂停时间")))) m_paused = !m_paused;
        m_debugView.Draw(m_debugger!, m_preview, test, source => LocateSource(source, locate));
        TickPreview();
        if (m_paused && ImGui.Button(m_fields.Label("单步时间 1/60 秒")))
        {
            m_preview.session.audio?.SetSuspended(false); m_preview.StepTime(1.0 / 60); m_preview.session.audio?.SetSuspended(true);
        }
        if (m_preview.routeRunning && ImGui.Button(m_fields.Label("暂停路线重放"))) m_preview.StopRoute();
        if (m_preview.holdingRoute && ImGui.Button(m_fields.Label("从当前状态继续游玩"))) m_preview.ContinuePlaying();
        m_preview.session.audio?.SetSuspended(m_paused || m_preview.holdingRoute || m_debugger!.paused);
        if (m_preview.error.Length > 0) EditorUiSession.current.widgets.Hint(m_preview.failure is { } cause ? m_fields.Error(cause) : m_preview.error);
        if (m_preview.targetReached) ImGui.Text(m_fields.Localize("已到达路线目标。可以检查当前状态或继续手动游玩。"));
        var story = m_preview.session.interactiveStory!;
        ImGui.Text(story.status + " · " + story.sourceChapterId + "/" + story.sourceNodeId);
        if (story.status == StoryStatus.Faulted) EditorUiSession.current.widgets.Hint(story.error);
        if (ImGui.Button(m_fields.Label("定位当前节点"))) LocateSource(story.debugLocation, locate);
        RuntimeInput(story);
        DrawCanvas();
        if (ImGui.CollapsingHeader(m_fields.Label("会话变量（只影响当前预览）")))
        {
            StoryMachine observed = m_debugger?.stoppedTask ?? story;
            foreach (var variable in observed.InspectVariables())
            {
                NovelValue value = variable.value;
                if (m_values.Draw("preview-observe/" + variable.id, ref value, false))
                { observed.SetVariable(variable.id, value); m_preview.InvalidateRouteRecording("会话变量经过调试修改；请保存局部初值，或重新从入口录制路线。"); }
            }
        }
        if (m_freshness?.stale != true && (story.status is StoryStatus.Dialogue or StoryStatus.Choice or StoryStatus.Input or StoryStatus.AdvanceWait)
            && ImGui.Button(m_fields.Label("将实际试玩路线写入此用例")) && ConfirmWriteBack())
        {
            var recorded = m_preview.CaptureRoute(); m_fields.Finish();
            if (nativeCase) m_resources.Edit<NovelPreviewCaseAsset>(m_toolbar.documentId, m_fields.Localize("录制试玩路线"), target => CopySettings(recorded, target)); else m_temporary = recorded;
        }
        if (m_preview.recordingError.Length > 0) EditorUiSession.current.widgets.Hint(m_preview.recordingError);
        if (m_freshness?.stale != true && story.canSave && ImGui.Button(m_fields.Label("将当前变量与舞台写入此用例")) && ConfirmWriteBack())
        {
            var current = story.Capture(); test.initialValues = current.gameVariables.Concat(current.localVariables).Concat(m_preview.session.CaptureProgress().variables).ToArray();
            test.stage = m_preview.session.stage.Capture(); test.useStageSnapshot = true; test.mode = PreviewRunMode.Local; test.nodeId = story.sourceNodeId;
            if (story.sourceChapterId != draft.chapterId) throw new InvalidOperationException(m_fields.Localize("打开当前节点所属章节后再保存局部用例。"));
            m_fields.Finish();
            if (nativeCase) m_resources.Edit<NovelPreviewCaseAsset>(m_toolbar.documentId, m_fields.Localize("保存预览初始状态"), target => CopySettings(test, target)); else m_temporary = test;
        }
    }

    private bool Settings(
        StoryDraftState draft,
        string selected,
        NovelPreviewCaseAsset test
    ) {
        bool changed = false; PreviewRunMode mode = test.mode;
        changed |= m_fields.SelectEnum("preview/mode", "预览方式", ref mode); test.mode = mode;
        string chapter = test.chapterId, entry = test.entryId, node = test.nodeId;
        changed |= m_fields.Select("preview/chapter", m_fields.Localize("起始章节"), ref chapter, new[] { (string.Empty, m_fields.Localize("游戏默认章节")) }
            .Concat(m_context!.chapters.Select(static item => (item.chapterId, item.title)))); test.chapterId = chapter;
        var source = m_context.chapters.SingleOrDefault(item => item.chapterId == (chapter.Length == 0 ? m_context.startChapter : chapter));
        changed |= m_fields.Select("preview/entry", m_fields.Localize("公开入口"), ref entry, new[] { (string.Empty, m_fields.Localize("游戏默认入口")) }
            .Concat(source?.program?.entries.Select(static item => (item.id, item.id)) ?? [])); test.entryId = entry;
        if (mode != PreviewRunMode.Complete)
        {
            if (m_fields.Text("preview/node", mode == PreviewRunMode.Local ? m_fields.Localize("局部起点") : m_fields.Localize("交互目标"), ref node)) { test.nodeId = node; test.targetInteraction = 0; changed = true; }
            if (selected.Length > 0 && ImGui.Button(m_fields.Label("使用所选节点作为起点/目标"))) { m_fields.Finish(); test.nodeId = selected; test.targetInteraction = 0; changed = true; }
            EditorUiSession.current.widgets.Hint(mode == PreviewRunMode.Local ? m_fields.Localize("局部预览使用这里明确指定的变量和舞台；未覆盖的变量采用声明默认值，不重建过去。") : m_fields.Localize("从公开入口按选项记录重放到当前章节中的对白、选项或输入节点。"));
        }
        if (mode == PreviewRunMode.Route)
        {
            ImGui.Text(m_fields.Localize("目标交互序号 · ") + test.targetInteraction + m_fields.Localize("（0 为首次匹配）"));
            if (test.targetInteraction > 0 && ImGui.Button(m_fields.Label("改为首次匹配目标"))) { m_fields.Finish(); test.targetInteraction = 0; changed = true; }
            string choices = string.Join('\n', test.choices);
            if (m_fields.MultilineText("preview/choices", m_fields.Localize("路线选项 ID（每行一个）"), ref choices)) { test.choices = choices.Split('\n').Where(static item => item.Length > 0).ToArray(); changed = true; }
            double maximum = test.maximumSeconds;
            changed |= m_fields.Number("preview/maximum", m_fields.Localize("路线最长逻辑秒数"), ref maximum, 0.1, 3600); test.maximumSeconds = maximum;
            int removeInput = -1;
            for (int index = 0; index < test.inputs.Length; index++)
            {
                PreviewInput input = test.inputs[index]; string prefix = "preview/input/" + index;
                string chapterId = input.chapterId, nodeId = input.nodeId, text = input.text;
                changed |= m_fields.Text(prefix + "/chapter", m_fields.Localize("输入章节 ID"), ref chapterId); input.chapterId = chapterId;
                changed |= m_fields.Text(prefix + "/node", m_fields.Localize("输入节点 ID"), ref nodeId); input.nodeId = nodeId;
                bool cancel = input.cancel; changed |= m_fields.Toggle(prefix + "/cancel", "取消此输入", ref cancel); input.cancel = cancel;
                if (!cancel) { changed |= m_fields.Text(prefix + "/text", m_fields.Localize("测试输入文字"), ref text); input.text = text; }
                if (ImGui.Button(m_fields.Caption("删除此输入步骤##" + prefix, m_fields.Localize("删除此输入步骤##") + prefix))) removeInput = index;
            }
            if (removeInput >= 0) { m_fields.Finish(); test.inputs = test.inputs.Where((_, index) => index != removeInput).ToArray(); changed = true; }
            if (selected.Length > 0 && draft.graph.document.FindNode(new(selected))?.definitionId == Model.Story.StoryNodeIds.input && ImGui.Button(m_fields.Label("为所选输入节点添加测试文字")))
            { m_fields.Finish(); test.inputs = [.. test.inputs, new() { chapterId = draft.chapterId, nodeId = selected }]; changed = true; }
        }
        if (ImGui.CollapsingHeader(m_fields.Label("预览变量初值")))
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
        changed |= m_fields.Toggle("preview/stage-snapshot", "使用已保存的舞台快照", ref snapshot); test.useStageSnapshot = snapshot;
        return changed;
    }

    private void RuntimeInput(StoryMachine story)
    {
        if (story.status != StoryStatus.Input) return;
        string text = story.inputText;
        if (ImGui.InputText(m_fields.Label("测试输入文字##preview-live-input"), ref text, 32768)) story.SetInputText(text);
        if (story.isInputValid && ImGui.Button(m_fields.Label("提交测试输入"))) { m_preview!.session.SubmitInput(); m_preview.ObserveInteraction(); }
    }
    private void LocateSource(
        StoryDebugLocation source,
        Action<string> select
    ) {
        Guid previous = m_stories.activeDocumentId;
        try
        {
            var context = m_context ?? throw new AuthoringValidationException($"先选择明确的游戏上下文，再定位跨章节调试来源。");
            if (m_freshness is not null && !m_freshness.CheckAll()) throw new AuthoringValidationException($"预览来源已变化，请重新运行预览后定位。");
            var snapshot = PreviewSourceNavigator.Resolve(context, m_stories, source.chapter, source.node, m_previewSource);
            m_fields.Finish(); m_stories.Open(snapshot.path);
            if (m_stories.Inspect(snapshot.path).fingerprint != snapshot.fingerprint)
                throw new AuthoringValidationException($"调试来源在定位期间发生变化，请重新定位。");
            select(source.node); m_message = string.Empty;
        }
        catch (Exception failure) when (failure is not OutOfMemoryException)
        { if (previous != Guid.Empty) m_stories.activeDocumentId = previous; m_message = m_fields.Error(failure); }
    }
    internal void DrawCompact(
        StoryDraftState draft,
        Action<string> locate,
        Action settings
    ) {
        SetVisible(true);
        if (ImGui.SmallButton(m_fields.Label("预览设置与调试"))) { m_fields.Finish(); settings(); }
        if (m_preparation is not null && DrawPreparation()) return;
        if (m_message.Length > 0) EditorUiSession.current.widgets.Hint(m_message);
        if (m_preview is null)
        {
            if (ImGui.Button(m_fields.Label("使用所选游戏")))
                m_context = NovelResourceSelection.Get<NovelProjectAsset>(m_interactions) ?? throw new InvalidOperationException(m_fields.Localize("选择游戏定义 .ivnproject 作为明确预览上下文。"));
            if (m_context is null) { EditorUiSession.current.widgets.Hint(m_fields.Localize("选择游戏定义后，从真实游戏入口开始预览；局部初值与路线用例在预览设置中配置。")); return; }
            if (ImGui.Button(m_fields.Label("从游戏入口运行"))) StartPreview(draft, new() { mode = PreviewRunMode.Complete });
            if (m_preparation is not null) return;
            if (m_preview is null) return;
        }
        ImGui.SameLine(); if (ImGui.SmallButton(m_fields.Caption(m_paused ? "继续" : "暂停", m_paused ? m_fields.Localize("继续") : m_fields.Localize("暂停")))) m_paused = !m_paused;
        ImGui.SameLine(); if (ImGui.SmallButton(m_fields.Label("停止"))) { Stop(); return; }
        TickPreview();
        var story = m_preview.session.interactiveStory!;
        if (m_preview.holdingRoute && ImGui.SmallButton(m_fields.Label("继续游玩"))) m_preview.ContinuePlaying();
        if (m_preview.routeRunning && ImGui.SmallButton(m_fields.Label("暂停路线"))) m_preview.StopRoute();
        if (ImGui.SmallButton(m_fields.Label("定位当前指令"))) LocateSource(story.debugLocation, locate);
        if (m_debugger?.paused == true) EditorUiSession.current.widgets.Hint(m_fields.Localize("已在断点暂停，可打开预览设置继续或单步。"));
        if (m_preview.error.Length > 0) EditorUiSession.current.widgets.Hint(m_preview.failure is { } cause ? m_fields.Error(cause) : m_preview.error);
        if (story.status == StoryStatus.Faulted) EditorUiSession.current.widgets.Hint(story.error);
        RuntimeInput(story); DrawCanvas(true);
    }
    private void StartPreview(
        StoryDraftState draft,
        NovelPreviewCaseAsset test,
        Guid caseDocument = default
    ) {
        m_fields.Finish();
        var context = m_context ?? throw new InvalidOperationException(m_fields.Localize("先选择预览的游戏定义。"));
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
        { CancelPreparation("预览章节或用例在准备期间已修改、保存或关闭，请重新运行。"); return false; }
        var progress = pending.operation.progressMessage;
        ImGui.Text(m_fields.Format(progress.Format, progress.GetArguments()) + m_fields.Localize(" · 已捕获 ") + pending.operation.capturedSources + m_fields.Localize(" 项资源"));
        if (ImGui.Button(m_fields.Label("取消预览准备"))) { CancelPreparation("已取消准备，保留先前预览。"); return false; }
        if (!pending.operation.Advance()) return true;
        m_preparation = null;
        using var completed = pending.operation;
        if (completed.result is not { } prepared) { m_message = completed.failure is { } cause ? m_fields.Error(cause) : completed.error; return false; }
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
        { candidate?.Dispose(); m_message = m_fields.Error(failure); }
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
        string message = m_freshness.reason + " 重新运行预览后再录制路线或写回用例；当前会话仍使用先前准备的剧情与界面。";
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
            bool changed = m_fields.Text("preview/live-input", m_fields.Localize("预览中的文字输入"), ref current);
            m_textBridgeActive = ImGui.IsItemActive();
            if (changed)
                owner.HandleInput(new() { selectAll = true, delete = current.Length == 0, text = current.Length == 0 ? [] : [current] });
            if (owner.session.page == "reading" && owner.session.interactiveStory is { status: StoryStatus.Input } playerInput)
            {
                ImGui.BeginDisabled(!playerInput.isInputValid);
                if (ImGui.Button(m_fields.Label("提交预览输入"))) { m_fields.Finish(); owner.HandleInput(new() { textConfirm = true }); }
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
