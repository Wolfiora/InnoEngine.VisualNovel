using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Inno.VisualNovel.Model.Programs;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Narrative.Reading;
using Inno.VisualNovel.Narrative.Values;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using Inno.VisualNovel.Model.Story.Nodes.Audio;
using Inno.VisualNovel.Model.Story.Nodes.Interface;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Localization;
using Inno.VisualNovel.Model.Extensions;
using Inno.VisualNovel.Narrative.Extensions;
using Inno.VisualNovel.Model.Text;
using Inno.VisualNovel.Narrative.Debugging;

namespace Inno.VisualNovel.Narrative.Execution;

/// <summary>Executes one compiled program using bounded, explicit and restorable state.</summary>
public sealed class StoryMachine : IDisposable
{
    private readonly StoryProgram m_program;
    private readonly Dictionary<string, StoryInstruction> m_instructions;
    private readonly Dictionary<string, string> m_entries;
    private readonly VariableDefinition[] m_localDefinitions;
    private VariableStore m_game;
    private readonly VariableStore m_globals;
    private readonly StoryRandomState m_random;
    private readonly List<StoryCallFrame> m_calls = [];
    private VariableStore m_locals;
    private string m_nodeId = string.Empty;
    private ReadingController m_reading;
    private double m_waitElapsed;
    private string m_inputText = string.Empty;
    private long m_interaction;
    private StoryTextSource? m_textSource;
    private readonly StorySpeakerSource? m_speakers;
    private readonly NovelCommandRegistry m_commands;
    private readonly bool m_preview;
    private NovelCommandExecution? m_command;
    private long m_rollbackEpoch;
    private string m_barrierReason = string.Empty;
    private readonly StoryTaskBudget m_taskBudget;
    private StoryParallelExecution? m_parallel;
    private StoryMachine? m_parent;
    private string m_taskJoin = string.Empty;
    private int m_taskDepth;
    private bool m_taskLease;
    private StoryParallelScope? m_taskScope;
    private readonly List<StoryOwnedResource> m_owned = [];
    private readonly List<StoryOwnedResource> m_cleanup = [];
    private readonly HashSet<(string node, string option)> m_selectedChoices = [];
    private long m_resultSequence;
    private StoryDebugger? m_debugger;
    private string m_debugPath = "main";

    /// <summary>Creates an isolated interpreter over a compiled immutable program.</summary>
    /// <param name="program">The validated compiled program.</param>
    /// <param name="seed">The nonzero deterministic seed.</param>
    /// <param name="globals">An optional shared global scope with matching declarations.</param>
    /// <param name="textSource">The optional frozen game-language view.</param>
    /// <param name="commands">Explicit generation-local command factories.</param>
    /// <param name="preview">Whether external effects and commands without preview permission are prohibited.</param>
    /// <param name="speakers">Optional frozen character-name resolution owned by the session.</param>
    public StoryMachine(
        StoryProgram program,
        ulong seed = 1,
        VariableStore? globals = null,
        StoryTextSource? textSource = null,
        NovelCommandRegistry? commands = null,
        bool preview = false,
        StorySpeakerSource? speakers = null
    ) : this(program, seed, globals, textSource, commands, preview, false, new StoryTaskBudget(), speakers) { }

    private StoryMachine(
        StoryProgram program,
        ulong seed,
        VariableStore? globals,
        StoryTextSource? textSource,
        NovelCommandRegistry? commands,
        bool preview,
        bool sharedProgram,
        StoryTaskBudget taskBudget,
        StorySpeakerSource? speakers,
        VariableStore? game = null,
        VariableStore? locals = null
    ) {
        ArgumentNullException.ThrowIfNull(program);
        m_program = program = sharedProgram ? program : StoryProgramCopy.Copy(program);
        if (string.IsNullOrWhiteSpace(program.fingerprint))
            throw new ArgumentException("A compiled fingerprint is required.", nameof(program));
        m_instructions = program.instructions.ToDictionary(static item => item.nodeId, StringComparer.Ordinal);
        m_speakers = speakers; m_reading = new(m_instructions, speakers);
        m_entries = program.entries.ToDictionary(static item => item.id, static item => item.nodeId, StringComparer.Ordinal);
        m_localDefinitions = program.variables.Where(static item => item.scope == VariableScope.Local).ToArray();
        m_locals = locals ?? new(m_localDefinitions);
        m_game = game ?? new(program.variables.Where(static item => item.scope == VariableScope.Game));
        m_globals = globals ?? new(program.variables.Where(static item => item.scope == VariableScope.Global));
        m_random = new(seed);
        m_textSource = textSource;
        m_commands = (commands ?? new()).Snapshot(); m_preview = preview;
        m_taskBudget = taskBudget;
    }

    /// <summary>Gets the current lifecycle.</summary>
    public StoryStatus status { get; private set; }
    /// <summary>Gets the single validated dialogue, choice or input owner, including nested parallel tasks.</summary>
    public StoryMachine? interactiveOwner => status is StoryStatus.Dialogue or StoryStatus.Choice or StoryStatus.Input or StoryStatus.AdvanceWait ? this : m_parallel?.interactive;
    internal bool hasTaskDialogue => m_taskScope?.hasDialogue ?? false;
    /// <summary>Gets the stable current source node.</summary>
    public string nodeId => m_nodeId;
    /// <summary>Gets the authored chapter identity of the active linked instruction.</summary>
    public string sourceChapterId => Current.sourceChapterId.Length > 0 ? Current.sourceChapterId : m_program.chapterId;
    /// <summary>Gets the authored node identity of the active linked instruction.</summary>
    public string sourceNodeId => Current.sourceNodeId.Length > 0 ? Current.sourceNodeId : m_nodeId;
    /// <summary>Gets the last explicit runtime failure, empty when healthy.</summary>
    public string error { get; private set; } = string.Empty;
    /// <summary>Gets the active speaker, empty outside dialogue.</summary>
    public string speaker => status == StoryStatus.Dialogue ? m_reading.speaker : string.Empty;
    /// <summary>Gets the active dialogue's stable character identity, empty for narration or unlinked speakers.</summary>
    public string characterId => status == StoryStatus.Dialogue ? Current.dialogue!.characterId : string.Empty;
    /// <summary>Gets a session-local sequence that changes whenever an interactive instruction is entered, including loops.</summary>
    public long interactionSequence => m_interaction;
    /// <summary>Gets the latest committed player result for synchronous owner observation, independent of persisted story state.</summary>
    public StoryInteractionResult? lastInteractionResult { get; private set; }
    /// <summary>Gets whether all active continuations can be captured as neutral state.</summary>
    public bool canSave => status != StoryStatus.Running && (m_command?.canSave ?? true) && (m_parallel?.canSave ?? true);
    /// <summary>Gets whether this entire interpreter tree is held by its optional debugger.</summary>
    public bool debugPaused => m_debugger?.paused ?? false;
    /// <summary>Gets the linked source and stable parallel task path without exposing mutable instructions.</summary>
    public StoryDebugLocation debugLocation => new(m_debugPath, sourceChapterId, sourceNodeId, m_nodeId, Current.definitionId);
    /// <summary>Attaches one optional owner-local debugger to this root and its existing task tree.</summary>
    /// <param name="debugger">The debug owner, or null to release all observation.</param>
    public void AttachDebugger(StoryDebugger? debugger)
    {
        if (m_taskDepth != 0) throw new InvalidOperationException("Attach a debugger through the root interpreter.");
        if (ReferenceEquals(m_debugger, debugger)) return;
        debugger?.Bind(this);
        m_debugger?.Detach(this);
        UseDebugger(debugger, "main");
    }
    /// <summary>Reads active game, invocation and global values without requiring a savable command state.</summary>
    /// <returns>Detached values for explicit debugging observation.</returns>
    public IReadOnlyList<VariableState> InspectVariables() => Array.AsReadOnly(m_game.Capture().Concat(m_locals.Capture()).Concat(m_globals.Capture()).ToArray());
    /// <summary>Reads the invocation return stack without capturing an active extension continuation.</summary>
    /// <returns>Independent caller-local values, oldest frame first.</returns>
    public IReadOnlyList<StoryCallFrame> InspectCalls() => Array.AsReadOnly(m_calls.Select(static item => new StoryCallFrame
        { returnNode = item.returnNode, locals = item.locals.Select(static value => new VariableState { id = value.id, value = value.value.Copy() }).ToArray() }).ToArray());
    /// <summary>Reads active tasks in deterministic branch order, including their nested descendants.</summary>
    /// <returns>Live owner-local interpreters for explicit debugger inspection.</returns>
    public IReadOnlyList<StoryMachine> InspectTasks() => Array.AsReadOnly(new[] { this }.Concat(m_parallel?.InspectTasks() ?? []).ToArray());
    internal bool UsesDebugger(StoryDebugger debugger) => ReferenceEquals(m_debugger, debugger);
    internal void UseDebugger(
        StoryDebugger? debugger,
        string path
    ) { m_debugger = debugger; m_debugPath = path; m_parallel?.UseDebugger(debugger, path); }
    /// <summary>Gets whether the current command exposes an explicit cancel output.</summary>
    public bool canCancelCommand => status == StoryStatus.Command && m_command is { canCancel: true };
    /// <summary>Gets the current rollback barrier epoch.</summary>
    public long rollbackEpoch => m_rollbackEpoch;
    /// <summary>Gets the author-facing explanation of the active irreversible boundary.</summary>
    public string rollbackBarrierReason => m_barrierReason;
    /// <summary>Creates a monotonic rollback barrier without changing variables or executing another instruction.</summary>
    /// <param name="reason">A nonempty bounded explanation shown to authors and players.</param>
    public void BlockRollback(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 256) throw new ArgumentException("Rollback barrier requires a bounded explanation.", nameof(reason));
        m_rollbackEpoch = checked(m_rollbackEpoch + 1); m_barrierReason = reason;
    }
    /// <summary>Gets the last extension cleanup diagnostic, empty when healthy.</summary>
    public string cleanupError { get; private set; } = string.Empty;
    /// <summary>Changes language observation without recreating an active command or its external work.</summary>
    /// <param name="source">The candidate immutable language view.</param>
    public void SetTextSource(StoryTextSource source)
    {
        m_textSource = source; m_parallel?.SetTextSource(source);
        m_waitElapsed = m_reading.Reflow(status == StoryStatus.Dialogue ? Current : null, m_textSource, m_waitElapsed);
    }
    /// <summary>Gets the language used by the currently captured dialogue.</summary>
    public string dialogueLocale => m_reading.locale;
    /// <summary>Gets the declared voice cue of the current line.</summary>
    public string voiceCueId => status == StoryStatus.Dialogue ? m_textSource?.ResolveVoice(StoryTextKeys.Dialogue(Current.dialogue!.textId), Current.dialogue.text, Current.dialogue.voiceCueId) ?? Current.dialogue!.voiceCueId : string.Empty;
    /// <summary>Gets the pending player text draft.</summary>
    public string inputText => m_inputText;
    /// <summary>Gets the pending input's declared maximum grapheme count.</summary>
    public int inputMaximumLength => status == StoryStatus.Input ? Current.input!.maximumLength : 0;
    /// <summary>Gets the interpolated player input prompt.</summary>
    public string inputPrompt => status == StoryStatus.Input ? Interpolate(Localized(NodeText("input.prompt"), Current.input!.prompt)) : string.Empty;
    /// <summary>Gets the localized prompt of the active decision.</summary>
    public string choicePrompt => status == StoryStatus.Choice ? Interpolate(Localized(NodeText("choice.prompt"), Current.choice!.prompt)) : string.Empty;
    /// <summary>Gets whether the active decision has an authored positive timeout.</summary>
    public bool isTimedChoice => status == StoryStatus.Choice && Current.choice!.timeout > 0;
    /// <summary>Gets the nonnegative seconds remaining on the active decision's existing logical clock.</summary>
    public double choiceTimeRemaining => isTimedChoice ? Math.Max(0, Current.choice!.timeout - m_waitElapsed) : 0;
    /// <summary>Gets the remaining fraction of the active timed decision, zero outside that decision.</summary>
    public double choiceTimeRemainingRatio => isTimedChoice ? Math.Clamp(choiceTimeRemaining / Current.choice!.timeout, 0, 1) : 0;
    /// <summary>Gets whether the pending input permits cancellation.</summary>
    public bool canCancelInput => status == StoryStatus.Input && Current.input!.allowCancel;
    /// <summary>Gets whether the current Unicode input draft satisfies its declared length bounds.</summary>
    public bool isInputValid => status == StoryStatus.Input && StringInfo.ParseCombiningCharacters(m_inputText).Length >= Current.input!.minimumLength;

    /// <summary>Edits a bounded input draft without writing its destination variable.</summary>
    /// <param name="value">Committed Unicode text, excluding IME composition fragments.</param>
    /// <returns>Whether the waiting input accepted this bounded draft.</returns>
    public bool SetInputText(string value)
    {
        if (interactiveOwner is { } owner && owner != this) return owner.SetInputText(value);
        if (debugPaused || status != StoryStatus.Input || StringInfo.ParseCombiningCharacters(value).Length > Current.input!.maximumLength) return false;
        m_inputText = value; return true;
    }
    /// <summary>Commits valid player input and consumes the action before entering the following instruction.</summary>
    /// <returns>Whether the input was committed.</returns>
    public bool SubmitInput()
    {
        if (interactiveOwner is { } owner && owner != this) { bool accepted = owner.SubmitInput(); if (accepted) SynchronizeTasks(); return accepted; }
        if (debugPaused || !isInputValid) return false;
        Guard(() => { SetVariable(Current.input!.variableId, NovelValue.String(m_inputText)); RecordResult(StoryInteractionKind.InputSubmitted, m_inputText); Continue("next"); }); return true;
    }
    /// <summary>Takes the explicit cancellation output without changing the destination variable.</summary>
    /// <returns>Whether cancellation was available and consumed.</returns>
    public bool CancelInput()
    {
        if (interactiveOwner is { } owner && owner != this) { bool accepted = owner.CancelInput(); if (accepted) SynchronizeTasks(); return accepted; }
        if (debugPaused || !canCancelInput) return false;
        Guard(() => { RecordResult(StoryInteractionKind.InputCancelled, string.Empty); Continue("cancel"); }); return true;
    }
    /// <summary>Gets the full current interpolated text.</summary>
    public string dialogueText => m_reading.document.text;
    /// <summary>Gets the immutable resolved rich document without exposing source-variable aliases.</summary>
    public NovelTextDocument dialogueDocument => m_reading.document;
    /// <summary>Gets the authored stable dialogue identity, excluding the current control-flow position.</summary>
    public string dialogueTextId => status == StoryStatus.Dialogue ? Current.dialogue!.textId : string.Empty;
    /// <summary>Gets the selected language's authored template before frozen variable substitution, for content-aware reading progress.</summary>
    public string dialogueSourceText => status == StoryStatus.Dialogue
        ? m_textSource?.Resolve(StoryTextKeys.Dialogue(Current.dialogue!.textId), Current.dialogue.text) ?? Current.dialogue!.text : string.Empty;
    /// <summary>Gets the visible number of complete Unicode text elements.</summary>
    public int visibleGraphemes => m_reading.visibleCount;
    /// <summary>Gets the active author-selected reading layout.</summary>
    public DialoguePresentation presentation => status == StoryStatus.Dialogue ? Current.dialogue!.presentation : DialoguePresentation.Adv;
    /// <summary>Gets the positioned reading rectangle as normalized left, top and width values.</summary>
    public (double x, double y, double width) dialoguePlacement => status == StoryStatus.Dialogue
        ? (Current.dialogue!.anchorX, Current.dialogue.anchorY, Current.dialogue.relativeWidth) : (0, 0, 1);
    /// <summary>Gets the full immutable NVL page including unrevealed active content.</summary>
    public NovelTextDocument readingPage => m_reading.page;
    /// <summary>Gets the grapheme-safe visible boundary within the full NVL page.</summary>
    public int visiblePageGraphemes => m_reading.visiblePageCount;
    /// <summary>Gets the grapheme-safe visible dialogue prefix.</summary>
    public string visibleText => m_reading.visibleText;
    /// <summary>Gets whether every current text element is revealed.</summary>
    public bool isTextComplete => m_reading.IsComplete(m_waitElapsed);
    /// <summary>Gets visible options evaluated against the current state.</summary>
    public IReadOnlyList<ChoiceState> choices => status != StoryStatus.Choice ? [] : Current.choice!.options
        .Where(option => Evaluate(option.visible).boolean)
        .Select(option => new ChoiceState(option.id, Interpolate(Localized(NodeText("option/" + option.id), option.text)), Evaluate(option.enabled).boolean,
            Interpolate(Localized(NodeText("reason/" + option.id), option.disabledReason)), IsSelected(m_nodeId, option.id))).ToArray();

    /// <summary>Reflows the active line in the supplied language view while preserving its reveal fraction.</summary>
    public void RefreshLocalizedDialogue()
    {
        m_waitElapsed = m_reading.Reflow(status == StoryStatus.Dialogue ? Current : null, m_textSource, m_waitElapsed);
        m_parallel?.RefreshLocalizedDialogue();
    }

    /// <summary>Gets an independent copy of the pending stage request.</summary>
    public StageNode? pendingStage => status != StoryStatus.Presentation || Current.stage is not { } source ? null : new()
    {
        operation = source.operation, resourceId = source.resourceId, target = source.target, pose = source.pose, expression = source.expression, order = source.order, seed = source.seed,
        transform = source.transform.Copy()
    };

    /// <summary>Completes one applied presentation request and resumes bounded narrative execution.</summary>
    /// <param name="suspend">Whether an applied page or title action suspends before its next instruction.</param>
    /// <returns>Whether a presentation request was waiting.</returns>
    public bool CompletePresentation(bool suspend = false)
    {
        if (debugPaused || status != StoryStatus.Presentation) return false;
        if (suspend && (Current.system is not { } action || !SystemNodeRules.Suspends(action.operation)))
            throw new InvalidOperationException("Only an applied session boundary action can suspend its narrative continuation.");
        if (m_taskJoin.Length > 0)
        {
            if (Current.audio is { operation: AudioOperation.Play } audio) Own("audio", audio.slot);
            if (Current.performance is { operation: PerformanceOperation.Play } performance) Own("performance", performance.instanceId);
            if (Current.stage is { operation: StageOperation.StartParticles } particles) Own("particles", particles.target);
            if (Current.stage is { operation: StageOperation.BeginSpeaking } speaking) Own("speaking", speaking.target);
            if (Current.stage is { operation: StageOperation.EndSpeaking } silent) m_owned.RemoveAll(resource => resource.kind == "speaking" && resource.id == silent.target);
            if (Current.stage is { operation: StageOperation.FollowCamera } following) Own("camera-follow", following.target);
            if (Current.stage is { operation: StageOperation.SetCamera or StageOperation.StopCameraFollow }) m_owned.RemoveAll(static resource => resource.kind == "camera-follow");
            if (Current.screenEffect is { operation: ScreenEffectOperation.Set or ScreenEffectOperation.Fade } effect) Own("effect", effect.instanceId);
            if (Current.transition is { operation: TransitionOperation.Begin }) Own("transition", "stage");
        }
        Guard(() =>
        {
            if (!suspend) { Continue("next"); return; }
            Move("next"); m_waitElapsed = 0; status = StoryStatus.Suspended; EnteredInteraction();
        });
        return true;
    }

    /// <summary>Records a source-addressed coordinator failure without unwinding the host update.</summary>
    /// <param name="message">The domain failure explanation.</param>
    public void Fail(string message)
    {
        ReleaseCommand();
        if (m_parallel is not null) m_cleanup.AddRange(m_parallel.CollectResources(true).Concat(m_parallel.CollectResources(false)));
        m_parallel?.Dispose(); m_parallel = null;
        status = StoryStatus.Faulted;
        error = m_nodeId + ": " + message;
    }

    private StoryInstruction Current => m_instructions.TryGetValue(m_nodeId, out StoryInstruction? value)
        ? value : throw new InvalidOperationException($"Instruction '{m_nodeId}' is unavailable.");

    /// <summary>Gets an independent pending audio request without device handles.</summary>
    public AudioNode? pendingAudio => status != StoryStatus.Presentation || Current.audio is not { } source ? null
        : new() { operation = source.operation, slot = source.slot, cueId = source.cueId };
    /// <summary>Gets the requested monotonic collection unlock, or null outside its presentation boundary.</summary>
    public string? pendingUnlock => status == StoryStatus.Presentation ? Current.unlock?.entryId : null;
    /// <summary>Gets a detached page or theme request for the owning session.</summary>
    public SystemNode? pendingSystem => status != StoryStatus.Presentation || Current.system is not { } source ? null : new() { operation = source.operation, argument = source.argument };
    /// <summary>Gets a detached pending performance request with explicit stable target bindings.</summary>
    public PerformanceNode? pendingPerformance => status != StoryStatus.Presentation || Current.performance is not { } source ? null
        : new() { operation = source.operation, clipId = source.clipId, instanceId = source.instanceId, time = source.time, markerId = source.markerId,
            bindings = source.bindings.Select(static item => new PerformanceBinding { slot = item.slot, target = item.target }).ToArray() };
    /// <summary>Gets a detached screen-image lifecycle request without graphics dependencies.</summary>
    public ScreenEffectNode? pendingScreenEffect => status != StoryStatus.Presentation || Current.screenEffect is not { } source ? null
        : new() { operation = source.operation, resourceId = source.resourceId, instanceId = source.instanceId, strength = source.strength,
            duration = source.duration, includeUi = source.includeUi, order = source.order, waitForCompletion = source.waitForCompletion };
    /// <summary>Gets a detached image-transition request and its explicit image scope.</summary>
    public TransitionNode? pendingTransition => status != StoryStatus.Presentation || Current.transition is not { } source ? null
        : new() { operation = source.operation, resourceId = source.resourceId, nextStage = source.nextStage, scope = source.scope,
            group = source.group, target = source.target, waitForCompletion = source.waitForCompletion };

    /// <summary>Starts an entry once and runs until an observable wait.</summary>
    /// <param name="entryId">The public entry identity.</param>
    public void Start(string entryId = "start")
    {
        if (status != StoryStatus.Stopped)
            throw new InvalidOperationException("Create a fresh machine to start another game.");
        m_nodeId = m_entries.TryGetValue(entryId, out string? node) ? node : throw new ArgumentException("Unknown story entry.", nameof(entryId));
        status = StoryStatus.Running;
        Pump();
    }

    /// <summary>Advances logical time without consuming a player action.</summary>
    /// <param name="seconds">A finite nonnegative time interval.</param>
    public void Tick(double seconds) => Tick(seconds, 1);

    /// <summary>Advances task clocks while scaling only dialogue reveal time, leaving sibling timers unchanged.</summary>
    /// <param name="seconds">A finite nonnegative logical interval.</param>
    /// <param name="readingSpeed">The finite positive player reading-rate multiplier.</param>
    public void Tick(
        double seconds,
        double readingSpeed
    )
    {
        if (!double.IsFinite(seconds) || seconds < 0 || !double.IsFinite(readingSpeed) || readingSpeed <= 0 || !double.IsFinite(seconds * readingSpeed))
            throw new ArgumentOutOfRangeException(nameof(seconds));
        if (debugPaused) return;
        Guard(() =>
        {
            if (status == StoryStatus.Suspended) status = StoryStatus.Running;
            if (status == StoryStatus.Running) { Pump(); return; }
            m_waitElapsed += status == StoryStatus.Dialogue ? seconds * readingSpeed : seconds;
            if (status == StoryStatus.Parallel)
            { m_parallel!.Tick(seconds, readingSpeed); SynchronizeTasks(); }
            else if (status == StoryStatus.Command)
            { StepCommand(seconds); if (status == StoryStatus.Running) Pump(); }
            else if (status == StoryStatus.Dialogue)
                m_reading.Evaluate(m_waitElapsed);
            else if (status == StoryStatus.Timer && m_waitElapsed >= Current.wait!.seconds)
                Continue("next");
            else if (status == StoryStatus.Choice && Current.choice!.timeout > 0 && m_waitElapsed >= Current.choice.timeout)
            {
                if (!Choose(Current.choice.defaultOption))
                    throw new InvalidOperationException("The timed choice's default option is not currently available.");
            }
        });
    }

    /// <summary>Completes text first, advances complete dialogue, or releases one explicit input wait without forwarding the action.</summary>
    /// <returns>True when this action was consumed.</returns>
    public bool Advance()
    {
        if (interactiveOwner is { } owner && owner != this) { bool accepted = owner.Advance(); if (accepted) SynchronizeTasks(); return accepted; }
        if (debugPaused) return false;
        if (status == StoryStatus.AdvanceWait) { Guard(() => Continue("next")); return true; }
        if (status != StoryStatus.Dialogue)
            return false;
        if (!isTextComplete)
        { m_reading.Complete(); m_waitElapsed = Math.Max(m_waitElapsed, m_reading.duration); }
        else
            Guard(() => Continue("next"));
        return true;
    }

    /// <summary>Chooses one currently visible enabled option without forwarding the same input.</summary>
    /// <param name="optionId">The stable option identity.</param>
    /// <returns>True when a valid decision was accepted.</returns>
    public bool Choose(string optionId)
    {
        if (interactiveOwner is { } owner && owner != this) { bool accepted = owner.Choose(optionId); if (accepted) SynchronizeTasks(); return accepted; }
        if (debugPaused || status != StoryStatus.Choice || !choices.Any(option => option.id == optionId && option.enabled))
            return false;
        Guard(() =>
        {
            MarkSelected(m_nodeId, optionId); RecordResult(StoryInteractionKind.Choice, optionId); Continue(optionId);
        });
        return true;
    }

    /// <summary>Reads one declared variable using current invocation scope.</summary>
    /// <param name="id">The stable variable identity.</param>
    /// <returns>A defensive typed value.</returns>
    public NovelValue GetVariable(string id) => Store(id).Get(id);

    /// <summary>Applies a checked variable write to this session.</summary>
    /// <param name="id">The stable destination identity.</param>
    /// <param name="value">A compatible detached value.</param>
    public void SetVariable(
        string id,
        NovelValue value
    ) => Store(id).Set(id, value);

    /// <summary>Cancels the active command and follows its explicit cancel edge.</summary>
    /// <returns>Whether cancellation was accepted.</returns>
    public bool CancelCommand()
    {
        if (debugPaused || !canCancelCommand) return false;
        Guard(() => { var command = m_command!; m_command = null; command.Dispose(); Continue("cancel"); }); return true;
    }

    /// <summary>Captures the interpreter while keeping global progress outside rollback state.</summary>
    /// <returns>A detached neutral snapshot.</returns>
    public StoryExecutionState Capture() => m_reading.Capture(new()
    {
        fingerprint = m_program.fingerprint, nodeId = m_nodeId, status = status,
        waitElapsed = m_waitElapsed, inputText = m_inputText,
        gameVariables = m_game.Capture(), localVariables = m_locals.Capture(),
        selectedChoices = m_selectedChoices.OrderBy(static item => item.node, StringComparer.Ordinal).ThenBy(static item => item.option, StringComparer.Ordinal)
            .Select(static item => new ChoiceSelection { nodeId = item.node, optionId = item.option }).ToArray(),
        randomState = m_random.state,
        commands = m_command is null ? [] : [m_command.Capture()], rollbackEpoch = m_rollbackEpoch, rollbackBarrierReason = m_barrierReason,
        tasks = m_parallel?.Capture() ?? [], taskJoin = m_taskJoin, ownedResources = m_owned.Select(CopyResource).ToArray(), cleanup = m_cleanup.Select(CopyResource).ToArray(),
        calls = m_calls.Select(static frame => new StoryCallFrame
        {
            returnNode = frame.returnNode, locals = frame.locals.Select(static item => new VariableState { id = item.id, value = item.value.Copy() }).ToArray()
        }).ToArray()
    });

    /// <summary>Validates a complete candidate before replacing any active state.</summary>
    /// <param name="snapshot">An exact-build interpreter snapshot.</param>
    /// <exception cref="InvalidOperationException">The snapshot is incompatible or malformed.</exception>
    public void Restore(StoryExecutionState snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.fingerprint != m_program.fingerprint || !Enum.IsDefined(snapshot.status)
            || snapshot.status is StoryStatus.Running or StoryStatus.Faulted
            || snapshot.status != StoryStatus.Stopped && !m_instructions.ContainsKey(snapshot.nodeId)
            || !double.IsFinite(snapshot.waitElapsed) || snapshot.waitElapsed < 0
            || !double.IsFinite(snapshot.revealProgress) || snapshot.revealProgress < 0
            || snapshot.calls.Length > 256 || snapshot.randomState == 0 || snapshot.rollbackEpoch < 0 || snapshot.rollbackBarrierReason.Length > 256
            || snapshot.commands.Length != (snapshot.status == StoryStatus.Command ? 1 : 0) || snapshot.taskJoin != m_taskJoin
            || snapshot.status != StoryStatus.Parallel && snapshot.tasks.Length > 0 || snapshot.ownedResources.Length > 256 || snapshot.cleanup.Length > 4096
            || m_taskScope is not null && !m_taskScope.Contains(snapshot.nodeId) && snapshot.nodeId != m_taskJoin
            || m_taskJoin.Length > 0 && snapshot.status == StoryStatus.Completed && snapshot.nodeId != m_taskJoin)
            throw new InvalidOperationException("Snapshot does not match this executable state.");
        StoryInstruction? instruction = m_instructions.GetValueOrDefault(snapshot.nodeId);
        if (snapshot.status == StoryStatus.Dialogue && instruction?.dialogue is null
            || snapshot.status == StoryStatus.Choice && instruction?.choice is null
            || snapshot.status == StoryStatus.Timer && instruction?.wait is not { waitForInput: false }
            || snapshot.status == StoryStatus.AdvanceWait && instruction?.wait is not { waitForInput: true }
            || snapshot.status == StoryStatus.Input && instruction?.input is null
            || snapshot.status == StoryStatus.Command && instruction?.command is null
            || snapshot.status == StoryStatus.Parallel && instruction?.parallel is null
            || snapshot.status == StoryStatus.Suspended && !m_instructions.Values.Any(source => source.system is { } action && SystemNodeRules.Suspends(action.operation)
                && source.targets.Any(target => target.nodeId == snapshot.nodeId))
            || snapshot.status == StoryStatus.Presentation && instruction?.stage is null && instruction?.audio is null && instruction?.performance is null && instruction?.unlock is null
                && instruction?.screenEffect is null && instruction?.transition is null && instruction?.system is null)
            throw new InvalidOperationException("Snapshot wait does not match its instruction.");
        var reading = new ReadingController(m_instructions, m_speakers); reading.Restore(snapshot);
        var selected = snapshot.selectedChoices.Select(static item => (node: item.nodeId, option: item.optionId)).ToHashSet();
        if (selected.Count != snapshot.selectedChoices.Length || selected.Count > 1_000_000
            || m_taskScope is not null && selected.Any(item => !m_taskScope.Contains(item.node))
            || selected.Any(item => !m_instructions.TryGetValue(item.node, out var source) || source.choice is null || !source.choice.options.Any(option => option.id == item.option)))
            throw new InvalidOperationException("Saved choice marks refer to unavailable options.");
        if (snapshot.status == StoryStatus.Input && StringInfo.ParseCombiningCharacters(snapshot.inputText).Length > instruction!.input!.maximumLength)
            throw new InvalidOperationException("Snapshot player input exceeds its declared grapheme bound.");
        var game = new VariableStore(m_program.variables.Where(static item => item.scope == VariableScope.Game));
        game.Restore(snapshot.gameVariables);
        var locals = new VariableStore(m_localDefinitions);
        locals.Restore(snapshot.localVariables);
        if (m_taskDepth > 0)
        {
            MatchSharedScope(game, m_game); MatchSharedScope(locals, m_locals);
            game = m_game; locals = m_locals;
        }
        var calls = new List<StoryCallFrame>();
        foreach (StoryCallFrame frame in snapshot.calls)
        {
            if (!m_instructions.ContainsKey(frame.returnNode))
                throw new InvalidOperationException("Snapshot return target is unavailable.");
            var caller = new VariableStore(m_localDefinitions);
            caller.Restore(frame.locals);
            calls.Add(new() { returnNode = frame.returnNode, locals = caller.Capture() });
        }
        var resources = snapshot.ownedResources.Select(ValidateResource).ToArray(); var cleanup = snapshot.cleanup.Select(ValidateResource).ToArray();
        NovelCommandExecution? commandCandidate = snapshot.status == StoryStatus.Command
            ? new(m_commands, instruction!.command!, snapshot.nodeId,
                id => locals.Contains(id) ? locals.Get(id) : game.Contains(id) ? game.Get(id) : m_globals.Get(id), m_preview, snapshot.commands[0]) : null;
        StoryParallelExecution? parallelCandidate = null;
        try
        {
            if (snapshot.status == StoryStatus.Parallel)
            {
                StoryParallelScope.ValidateFork(instruction!, m_instructions);
                parallelCandidate = new(instruction!.parallel!, (branch, state) => CreateTask(instruction, branch, state, game, locals), snapshot.tasks);
                if (parallelCandidate.InspectTasks().SelectMany(static task => task.m_selectedChoices).Any(mark => !selected.Contains(mark)))
                    throw new InvalidOperationException("Parallel choice marks are absent from their owning parent snapshot.");
            }
        }
        catch { commandCandidate?.Dispose(); parallelCandidate?.Dispose(); throw; }
        m_parallel?.Dispose(); m_parallel = parallelCandidate;
        ReleaseCommand();
        m_command = commandCandidate; m_rollbackEpoch = snapshot.rollbackEpoch; m_barrierReason = snapshot.rollbackBarrierReason;
        m_owned.Clear(); m_owned.AddRange(resources); m_cleanup.Clear(); m_cleanup.AddRange(cleanup);
        m_game = game;
        m_locals = locals;
        m_calls.Clear();
        m_calls.AddRange(calls);
        m_nodeId = snapshot.nodeId;
        status = snapshot.status;
        m_waitElapsed = snapshot.waitElapsed;
        m_reading = reading; m_inputText = snapshot.inputText;
        m_selectedChoices.Clear(); m_selectedChoices.UnionWith(selected); lastInteractionResult = null;
        m_random.Restore(snapshot.randomState);
        error = string.Empty;
    }

    private void Pump() => Guard(() =>
    {
        for (int remaining = 4096; status == StoryStatus.Running; remaining--)
        {
            if (remaining == 0)
                throw new InvalidOperationException("Immediate instruction budget exceeded; check for a loop without a wait.");
            StoryInstruction instruction = Current;
            if (m_debugger?.Before(this) == false) return;
            if (m_taskJoin.Length > 0) (m_taskScope ?? throw new InvalidOperationException("The task scope is unavailable.")).Validate(instruction);
            switch (instruction.definitionId)
            {
                case StoryNodeIds.log:
                    if (instruction.log is not { } log) throw new InvalidOperationException("The log instruction has no declaration.");
                    if (!log.previewOnly || m_preview)
                    {
                        string message = "[VN " + instruction.sourceChapterId + "/" + instruction.sourceNodeId + "] " + Interpolate(log.message);
                        switch (log.level)
                        {
                            case Model.Story.Nodes.Debugging.NovelLogLevel.Information: InnoEngine.Logging.Log.Info(typeof(StoryMachine), message); break;
                            case Model.Story.Nodes.Debugging.NovelLogLevel.Warning: InnoEngine.Logging.Log.Warn(typeof(StoryMachine), message); break;
                            case Model.Story.Nodes.Debugging.NovelLogLevel.Error: InnoEngine.Logging.Log.Error(typeof(StoryMachine), message); break;
                            default: throw new InvalidOperationException("The log instruction severity is unavailable.");
                        }
                    }
                    Move("next"); break;
                case StoryNodeIds.parallel:
                    StoryParallelScope.ValidateFork(instruction, m_instructions);
                    m_parallel = new(instruction.parallel!, (branch, _) => CreateTask(instruction, branch, null, m_game, m_locals));
                    status = StoryStatus.Parallel; FinishParallel();
                    break;
                case StoryNodeIds.join:
                    if (m_nodeId != m_taskJoin) throw new InvalidOperationException("A parallel join cannot be entered outside its owning task.");
                    status = StoryStatus.Completed;
                    break;
                case StoryNodeIds.command:
                    m_command = new(m_commands, instruction.command!, instruction.nodeId, GetVariable, m_preview);
                    if (!instruction.command!.definition.capabilities.HasFlag(CommandCapabilities.Rollback)) BlockRollback(instruction.command.definition.title.Length > 0 ? instruction.command.definition.title[..Math.Min(256, instruction.command.definition.title.Length)] : instruction.command.definition.id);
                    status = StoryStatus.Command; StepCommand(0);
                    break;
                case StoryNodeIds.stage:
                case StoryNodeIds.system:
                case StoryNodeIds.screenEffect:
                case StoryNodeIds.transition:
                case StoryNodeIds.unlock:
                case StoryNodeIds.performance:
                case StoryNodeIds.audio: status = StoryStatus.Presentation; break;
                case StoryNodeIds.entry: Move("next"); break;
                case StoryNodeIds.end: status = StoryStatus.Completed; break;
                case StoryNodeIds.dialogue:
                    EnteredInteraction();
                    m_reading.Enter(instruction, m_textSource, GetVariable);
                    status = StoryStatus.Dialogue;
                    break;
                case StoryNodeIds.input:
                    EnteredInteraction();
                    m_inputText = Interpolate(instruction.input!.defaultText);
                    int[] inputElements = StringInfo.ParseCombiningCharacters(m_inputText);
                    if (inputElements.Length > instruction.input.maximumLength) m_inputText = m_inputText[..inputElements[instruction.input.maximumLength]];
                    status = StoryStatus.Input;
                    break;
                case StoryNodeIds.choice:
                    EnteredInteraction();
                    status = StoryStatus.Choice;
                    if (!choices.Any(static option => option.enabled))
                        throw new InvalidOperationException("The current decision has no visible enabled option.");
                    break;
                case StoryNodeIds.assign:
                    SetVariable(instruction.assignment!.variableId, new ExpressionEvaluator(GetVariable, m_random.Next).Evaluate(instruction.assignment.value));
                    Move("next");
                    break;
                case StoryNodeIds.condition: Move(Evaluate(instruction.condition!.condition).boolean ? "true" : "false"); break;
                case StoryNodeIds.assertion:
                    if (!Evaluate(instruction.condition!.condition).boolean)
                        throw new InvalidOperationException("Story assertion failed.");
                    Move("next");
                    break;
                case StoryNodeIds.wait:
                    if (instruction.wait!.waitForInput) { EnteredInteraction(); status = StoryStatus.AdvanceWait; }
                    else if (instruction.wait.seconds == 0) Move("next");
                    else status = StoryStatus.Timer;
                    break;
                case StoryNodeIds.call:
                    if (!instruction.call!.jump)
                    {
                        if (m_calls.Count >= 256) throw new InvalidOperationException("Story invocation depth exceeds 256.");
                        m_calls.Add(new() { returnNode = Target("next"), locals = m_locals.Capture() });
                        m_locals = new(m_localDefinitions);
                    }
                    string callee = m_entries[instruction.call!.entryId]; m_debugger?.Edge(this, instruction.call.jump ? "jump" : "call", callee); m_nodeId = callee;
                    break;
                case StoryNodeIds.returnFromCall:
                    if (m_calls.Count == 0)
                        throw new InvalidOperationException("Return requires an active invocation.");
                    StoryCallFrame frame = m_calls[^1];
                    m_locals.Restore(frame.locals);
                    m_debugger?.Edge(this, "return", frame.returnNode); m_nodeId = frame.returnNode;
                    m_calls.RemoveAt(m_calls.Count - 1);
                    break;
                case StoryNodeIds.random:
                    double total = instruction.random!.branches.Sum(static branch => branch.weight);
                    double point = m_random.Next() * total;
                    string selected = instruction.random.branches[^1].id;
                    foreach (var branch in instruction.random.branches)
                    {
                        point -= branch.weight;
                        if (point < 0) { selected = branch.id; break; }
                    }
                    Move(selected);
                    break;
                default: throw new InvalidOperationException($"Instruction implementation is unavailable: {instruction.definitionId}.");
            }
            m_waitElapsed = 0;
            m_debugger?.After(this);
            if (debugPaused) return;
        }
    });

    private void Continue(string port)
    {
        Move(port);
        status = StoryStatus.Running;
        m_waitElapsed = 0;
        Pump();
    }

    private void StepCommand(double seconds)
    {
        if (m_command is null || !m_command.Tick(seconds)) return;
        var command = m_command;
        if (Current.command!.resultVariable.Length > 0) SetVariable(Current.command.resultVariable, command.result ?? throw new InvalidOperationException("Command has no result for its destination variable."));
        m_command = null; command.Complete(); Move("next"); m_waitElapsed = 0; status = StoryStatus.Running;
    }
    private void ReleaseCommand()
    {
        var command = m_command; m_command = null;
        try { command?.Dispose(); }
        catch (Exception failure) when (failure is not OutOfMemoryException) { cleanupError = failure.Message; }
    }
    /// <summary>Cancels active extension work and releases generation-owned handlers.</summary>
    public void Dispose()
    {
        if (m_taskDepth == 0) m_debugger?.Detach(this);
        m_debugger = null;
        ReleaseCommand(); m_parallel?.Dispose(); m_parallel = null; status = StoryStatus.Stopped;
        m_parent = null;
        if (m_taskLease) { m_taskLease = false; m_taskBudget.Release(); }
    }

    /// <summary>Gets presentation actors in stable branch order without creating worker threads.</summary>
    /// <returns>Active leaf interpreters awaiting presentation requests.</returns>
    public IReadOnlyList<StoryMachine> PresentationTasks() => status == StoryStatus.Presentation ? [this] : m_parallel?.presentations ?? [];
    /// <summary>Commits bounded parallel completion after the host applies presentation requests.</summary>
    public void SynchronizeTasks()
    {
        if (debugPaused) return;
        Guard(() => { m_parallel?.Synchronize(); if (FinishParallel() && status == StoryStatus.Running) Pump(); });
    }
    /// <summary>Drains neutral cleanup requests produced by cancelled parallel tasks.</summary>
    /// <returns>Detached logical resource identities.</returns>
    public IReadOnlyList<StoryOwnedResource> DrainTaskCleanup()
    {
        if (m_parallel is not null) m_cleanup.AddRange(m_parallel.DrainCleanup());
        var values = m_cleanup.Select(CopyResource).ToArray(); m_cleanup.Clear(); return values;
    }
    internal IEnumerable<StoryOwnedResource> AllOwnedResources() => m_owned.Concat(m_parallel is null ? [] : m_parallel.CollectResources(false).Concat(m_parallel.CollectResources(true))).Select(CopyResource);
    /// <summary>Associates a successfully started dialogue voice with its bounded task so cancellation releases it.</summary>
    public void TrackReadingVoice()
    {
        if (status != StoryStatus.Dialogue) throw new InvalidOperationException("A reading voice requires an active dialogue owner.");
        if (m_taskJoin.Length > 0) Own("audio", "voice");
    }
    private void Own(
        string kind,
        string id
    ) {
        if (!m_owned.Any(resource => resource.kind == kind && resource.id == id)) m_owned.Add(new() { kind = kind, id = id });
        if (m_owned.Count > 256) throw new InvalidOperationException("Parallel task presentation resource budget exceeded.");
    }
    private bool FinishParallel()
    {
        if (m_parallel is null || !m_parallel.complete) return false;
        m_cleanup.AddRange(m_parallel.DrainCleanup());
        m_cleanup.AddRange(m_parallel.CollectResources(true));
        if (m_taskJoin.Length > 0) foreach (var resource in m_parallel.CollectResources(false)) Own(resource.kind, resource.id);
        string join = Current.parallel!.joinId;
        if (m_parallel.completedReading is { } completed) m_reading.InheritPage(completed.m_reading);
        m_parallel.Dispose(); m_parallel = null;
        m_nodeId = m_instructions[join].targets.Single(target => target.port == "next").nodeId;
        m_waitElapsed = 0; status = StoryStatus.Running; return true;
    }
    private StoryMachine CreateTask(
        StoryInstruction fork,
        string branch,
        StoryExecutionState? restored,
        VariableStore game,
        VariableStore locals
    ) {
        if (m_taskDepth >= 8) throw new InvalidOperationException("Parallel nesting budget of 8 was exceeded.");
        string target = fork.targets.Single(item => item.port == branch).nodeId;
        m_taskBudget.Acquire(); StoryMachine? candidate = null;
        try
        {
            if (restored is null) m_random.Next();
            candidate = new(m_program, m_random.state, m_globals, m_textSource, m_commands, m_preview, true, m_taskBudget, m_speakers, game, locals)
            { m_taskJoin = fork.parallel!.joinId, m_taskDepth = m_taskDepth + 1, m_taskLease = true,
                m_debugger = m_debugger, m_debugPath = m_debugPath + "/" + fork.nodeId + ":" + branch, m_parent = this };
            candidate.m_taskScope = new(m_instructions, target, fork.parallel!.joinId);
            if (restored is not null) candidate.Restore(restored);
            else
            {
                if (candidate.hasTaskDialogue) candidate.m_reading.InheritPage(m_reading);
                candidate.m_nodeId = target; candidate.status = StoryStatus.Running; candidate.Pump();
            }
            return candidate;
        }
        catch { if (candidate is not null) candidate.Dispose(); else m_taskBudget.Release(); throw; }
    }
    private static void MatchSharedScope(
        VariableStore candidate,
        VariableStore shared
    ) {
        var values = candidate.Capture(); var expected = shared.Capture();
        if (values.Length != expected.Length || values.Where((value, index) => value.id != expected[index].id || !value.value.Equals(expected[index].value)).Any())
            throw new InvalidOperationException("Parallel task variables do not match their shared parent scope.");
    }
    private static StoryOwnedResource CopyResource(StoryOwnedResource value) => new() { kind = value.kind, id = value.id };
    private static StoryOwnedResource ValidateResource(StoryOwnedResource value) => value.kind is "audio" or "performance" or "particles" or "effect" or "transition" or "speaking" or "camera-follow" && !string.IsNullOrWhiteSpace(value.id)
        && (value.kind != "speaking" || !value.id.Contains('/') && !value.id.Contains('\\'))
        && (value.kind != "camera-follow" || Model.Stages.StageCameraTargetRules.IsValid(value.id))
        ? CopyResource(value) : throw new InvalidOperationException("Parallel resource snapshot contains an unknown identity.");

    private void Move(string port)
    {
        string target = Target(port); m_debugger?.Edge(this, port, target); m_nodeId = target;
    }
    private string Target(string port) => Current.targets.SingleOrDefault(target => target.port == port)?.nodeId
        ?? throw new InvalidOperationException($"Output '{port}' has no target.");
    private VariableStore Store(string id) => m_locals.Contains(id) ? m_locals : m_game.Contains(id) ? m_game : m_globals.Contains(id) ? m_globals
        : throw new InvalidOperationException($"Unknown variable '{id}'.");
    private NovelValue Evaluate(ExpressionDocument expression) => new ExpressionEvaluator(GetVariable).Evaluate(expression);
    private string NodeText(string field) => StoryTextKeys.Node(sourceChapterId, sourceNodeId, field);
    private string Localized(
        string id,
        string source
    ) => m_textSource?.Resolve(id, source) ?? source;

    private string Interpolate(
        string source,
        bool escapeMarkup = false
    )
    {
        var text = new StringBuilder();
        foreach (NovelTextToken token in NovelTextTemplate.Parse(source))
        {
            string value = token.variable ? GetVariable(token.value).ToString() : token.value;
            text.Append(escapeMarkup && token.variable ? NovelTextMarkup.Escape(value) : value);
        }
        return text.ToString();
    }

    private void RecordResult(
        StoryInteractionKind kind,
        string value
    ) => RecordResultAt(kind, sourceChapterId, sourceNodeId, value);

    private void RecordResultAt(
        StoryInteractionKind kind,
        string chapter,
        string node,
        string value
    ) {
        lastInteractionResult = new(checked(++m_resultSequence), kind, chapter, node, value);
        m_parent?.RecordResultAt(kind, chapter, node, value);
    }
    private void EnteredInteraction()
    {
        for (StoryMachine? owner = this; owner is not null; owner = owner.m_parent) owner.m_interaction = checked(owner.m_interaction + 1);
    }
    private bool IsSelected(
        string node,
        string option
    ) => m_selectedChoices.Contains((node, option)) || (m_parent?.IsSelected(node, option) ?? false);
    private void MarkSelected(
        string node,
        string option
    ) {
        for (StoryMachine? owner = this; owner is not null; owner = owner.m_parent)
            if (owner.m_selectedChoices.Count >= 1_000_000 && !owner.m_selectedChoices.Contains((node, option)))
                throw new InvalidOperationException("Selected choice marks exceed the game-state budget.");
        for (StoryMachine? owner = this; owner is not null; owner = owner.m_parent) owner.m_selectedChoices.Add((node, option));
    }

    private void Guard(Action action)
    {
        try { action(); }
        catch (Exception failure) when (failure is not OutOfMemoryException)
        {
            ReleaseCommand();
            if (m_parallel is not null) m_cleanup.AddRange(m_parallel.CollectResources(true).Concat(m_parallel.CollectResources(false)));
            m_parallel?.Dispose(); m_parallel = null;
            status = StoryStatus.Faulted;
            error = $"{m_nodeId}: {failure.Message}";
        }
    }
}
