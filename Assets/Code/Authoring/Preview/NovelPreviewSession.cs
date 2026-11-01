using System;
using System.Linq;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Narrative.Extensions;
using Inno.VisualNovel.Narrative.Values;
using Inno.VisualNovel.Player.Ports;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Player.Shell;
using Inno.VisualNovel.UI.Text;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Narrative.Debugging;
using Inno.VisualNovel.Player.Persistence;

namespace Inno.VisualNovel.Authoring.Preview;

/// <summary>Owns one throwaway preview using the production session, UI and presentation chain.</summary>
public sealed class NovelPreviewSession : IDisposable
{
    private readonly NovelPreviewCaseAsset m_test;
    private readonly string m_targetChapter;
    private int m_choice;
    private int m_input;
    private int m_steps;
    private double m_routeTime;
    private bool m_manual;
    private readonly PreviewRouteRecorder m_recorder;
    private readonly PreviewStorage? m_storage;
    /// <summary>Prepares explicit initial values before starting the production interpreter.</summary>
    /// <param name="context">The imported game context.</param>
    /// <param name="draft">The current unsaved chapter.</param>
    /// <param name="compiler">The production story compiler.</param>
    /// <param name="test">The native preview case.</param>
    /// <param name="measureText">The active text adapter.</param>
    /// <param name="audio">An optional independently owned audio output.</param>
    /// <param name="measureRichText">The width-aware production text adapter.</param>
    /// <param name="commands">Optional explicit preview-safe command factories.</param>
    /// <param name="debugger">An optional source debugger attached before the first instruction.</param>
    /// <param name="stateCodec">Optional editor-native serialization for independent volatile preview saves.</param>
    /// <param name="variableTable">Optional prepared shared declarations for the active draft's native library identity.</param>
    /// <param name="performanceTargets">Explicit pure extension channels for this isolated preview, without invoking a scene bridge.</param>
    public NovelPreviewSession(
        NovelProjectAsset context,
        StoryDraftState draft,
        StoryCompiler compiler,
        NovelPreviewCaseAsset test,
        Func<UiElementDefinition, string, (float width, float height)> measureText,
        INovelAudioOutput? audio = null,
        UiTextMeasurement? measureRichText = null,
        NovelCommandRegistry? commands = null,
        StoryDebugger? debugger = null,
        INovelStateCodec? stateCodec = null,
        Model.Values.VariableTableAsset? variableTable = null,
        Player.Performance.NovelPerformanceTargets? performanceTargets = null
    ) {
        var built = PreviewContentBuilder.Build(context, draft, compiler, test, variableTable);
        m_test = Copy(test); m_targetChapter = draft.chapterId; project = built.project;
        session = new(project, audio, commands: commands, preview: true, debugger: debugger, options: new() { checkpointCodec = stateCodec, performanceTargets = performanceTargets });
        try
        {
            var declarations = project.chapters.SelectMany(static chapter => chapter.program!.variables).DistinctBy(static variable => variable.id).ToDictionary(static variable => variable.id, StringComparer.Ordinal);
            foreach (var initial in m_test.initialValues)
                if (!declarations.TryGetValue(initial.id, out var definition)) throw new InvalidOperationException("Preview initial variable is undeclared: " + initial.id);
                else Model.Values.VariableValueRules.Validate(definition, initial.value);
            var progress = session.CaptureProgress();
            foreach (var value in progress.variables)
                if (m_test.initialValues.SingleOrDefault(initial => initial.id == value.id) is { } initial) value.value = initial.value.Copy();
            session.RestoreProgress(progress);
            if (m_test.useStageSnapshot) m_test.stage = session.stage.Fork(m_test.stage).Capture();
            session.StartAt(built.chapter, built.entry, m_test.initialValues.Where(initial => declarations[initial.id].scope != Model.Values.VariableScope.Global).ToArray(),
                m_test.useStageSnapshot ? m_test.stage : null);
            m_storage = stateCodec is null ? null : new PreviewStorage();
            var saves = stateCodec is null ? null : new SaveService(m_storage!, stateCodec, session.gameId, session.buildFingerprint);
            view = new(project, session, measureText, saves, measureRichText: measureRichText);
            m_recorder = new(m_test, session.stateRevision);
            routeRunning = m_test.mode == PreviewRunMode.Route; CheckTarget();
        }
        catch { m_storage?.Dispose(); session.Dispose(); throw; }
    }
    /// <summary>Gets the isolated in-memory project consumed by the same production frame composer.</summary>
    public NovelProjectAsset project { get; }
    /// <summary>Gets the production session; its progress and variables are never copied to the authoring source.</summary>
    public NovelSession session { get; }
    /// <summary>Gets the actual game UI owner, optionally backed by isolated native-format preview memory.</summary>
    public NovelGameView view { get; }
    /// <summary>Gets whether bounded route replay is advancing automatically.</summary>
    public bool routeRunning { get; private set; }
    /// <summary>Gets whether the recorded route reached its declared interactive target.</summary>
    public bool targetReached { get; private set; }
    /// <summary>Gets an actionable route diagnostic, empty when healthy.</summary>
    public string error { get; private set; } = string.Empty;
    /// <summary>Gets the transient route failure before presentation; it is not included in narrative snapshots.</summary>
    public Exception? failure { get; private set; }
    /// <summary>Gets whether route playback is holding its exact interactive target for inspection.</summary>
    public bool waitingAtTarget => targetReached && !m_manual;
    /// <summary>Gets whether route replay is holding an inspectable target, cancellation or failure.</summary>
    public bool holdingRoute => m_test.mode == PreviewRunMode.Route && !routeRunning && !m_manual;
    /// <summary>Gets why a modified or incomplete interaction stream cannot be saved as a reproducible route.</summary>
    public string recordingError => m_recorder.error;
    /// <summary>Routes one real game UI input and immediately records its accepted narrative result.</summary>
    /// <param name="input">The same logical input used by the production game view.</param>
    public void HandleInput(UI.Input.UiInputSnapshot input) { if (session.story?.debugPaused == true) return; view.HandleInput(input); m_recorder.Observe(session); }
    /// <summary>Observes an explicitly driven session interaction before another step can replace its result.</summary>
    public void ObserveInteraction() => m_recorder.Observe(session);
    /// <summary>Marks debugger edits as unsuitable for route recording without explicit starting preconditions.</summary>
    /// <param name="reason">The author-visible explanation.</param>
    public void InvalidateRouteRecording(string reason) => m_recorder.Invalidate(reason);
    /// <summary>Captures actual decisions and committed input from the original entry to the current interactive boundary.</summary>
    /// <returns>A detached native route case ready for the author's explicit save.</returns>
    public NovelPreviewCaseAsset CaptureRoute() => m_recorder.Capture(session, m_targetChapter);
    /// <summary>Leaves automatic route traversal and continues ordinary manual game time.</summary>
    public void ContinuePlaying() { routeRunning = false; m_manual = true; }
    /// <summary>Advances only game time without automatically choosing or skipping an interaction.</summary>
    /// <param name="seconds">A finite nonnegative logical interval.</param>
    public void StepTime(double seconds) { view.Tick(seconds); m_recorder.Observe(session); }
    /// <summary>Advances ordinary preview time or a bounded number of fixed route steps.</summary>
    /// <param name="seconds">Nonnegative elapsed editor time.</param>
    public void Tick(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (session.story?.debugPaused == true) return;
        m_recorder.Observe(session);
        if (m_test.mode != PreviewRunMode.Route || m_manual) { view.Tick(seconds); m_recorder.Observe(session); return; }
        if (!routeRunning) return;
        try
        {
            for (int budget = 0; budget < 8 && routeRunning; budget++)
            {
                if (session.story?.debugPaused == true) return;
                if (++m_steps > 100000 || m_routeTime >= m_test.maximumSeconds) throw new InvalidOperationException("Route replay exceeded its logical time or update budget. Check waits and loops.");
                if (CheckTarget()) return;
                StoryMachine story = session.interactiveStory!;
                switch (story.status)
                {
                    case StoryStatus.Dialogue:
                    case StoryStatus.AdvanceWait: session.Advance(); break;
                    case StoryStatus.Choice:
                        if (m_choice >= m_test.choices.Length) throw new InvalidOperationException("Record the next decision option at " + story.sourceChapterId + "/" + story.sourceNodeId);
                        if (!session.Choose(m_test.choices[m_choice])) throw new InvalidOperationException("Recorded option is hidden, disabled or missing: " + m_test.choices[m_choice]);
                        m_choice++; break;
                    case StoryStatus.Input:
                        var input = m_input < m_test.inputs.Length ? m_test.inputs[m_input] : null;
                        if (input is null || input.chapterId != story.sourceChapterId || input.nodeId != story.sourceNodeId)
                            throw new InvalidOperationException("Declare the next ordered player input for " + story.sourceChapterId + "/" + story.sourceNodeId);
                        bool accepted = input.cancel ? session.CancelInput() : story.SetInputText(input.text) && session.SubmitInput();
                        if (!accepted) throw new InvalidOperationException("Recorded player input violates its authored bounds or cancellation policy.");
                        m_input++;
                        break;
                    case StoryStatus.Faulted: throw new InvalidOperationException(story.error);
                    case StoryStatus.Completed: throw new InvalidOperationException("The recorded route ended before its target.");
                    default: view.Tick(m_test.stepSeconds); m_routeTime += m_test.stepSeconds; break;
                }
                m_recorder.Observe(session); CheckTarget();
            }
        }
        catch (Exception cause) when (cause is not OutOfMemoryException) { routeRunning = false; failure = cause; error = cause.Message; }
    }
    /// <summary>Stops automatic route traversal while retaining the inspectable production session.</summary>
    public void StopRoute() => routeRunning = false;
    /// <summary>Releases UI, extension, audio and session owners together.</summary>
    public void Dispose() { view.Dispose(); session.Dispose(); m_storage?.Dispose(); }

    private bool CheckTarget()
    {
        if (m_test.mode == PreviewRunMode.Route && m_test.targetInteraction > 0 && session.story!.interactionSequence > m_test.targetInteraction)
            throw new InvalidOperationException("The recorded target interaction changed. Replay and record this route again.");
        if (m_test.mode != PreviewRunMode.Route || session.interactiveStory is not { } story || story.status is not (StoryStatus.Dialogue or StoryStatus.Choice or StoryStatus.Input or StoryStatus.AdvanceWait)
            || story.sourceChapterId != m_targetChapter || story.sourceNodeId != m_test.nodeId
            || m_test.targetInteraction > 0 && session.story!.interactionSequence != m_test.targetInteraction) return false;
        targetReached = true; routeRunning = false; return true;
    }
    private static NovelPreviewCaseAsset Copy(NovelPreviewCaseAsset source) => new()
    {
        title = source.title, mode = source.mode, chapterId = source.chapterId, entryId = source.entryId, nodeId = source.nodeId, useStageSnapshot = source.useStageSnapshot,
        targetInteraction = source.targetInteraction,
        stage = source.stage, initialValues = source.initialValues.Select(static item => new VariableState { id = item.id, value = item.value.Copy() }).ToArray(),
        choices = source.choices.ToArray(), inputs = source.inputs.Select(static item => new PreviewInput { chapterId = item.chapterId, nodeId = item.nodeId, text = item.text, cancel = item.cancel }).ToArray(),
        breakpoints = source.breakpoints.Select(static item => item.Copy()).ToArray(),
        maximumSeconds = source.maximumSeconds, stepSeconds = source.stepSeconds
    };
}
