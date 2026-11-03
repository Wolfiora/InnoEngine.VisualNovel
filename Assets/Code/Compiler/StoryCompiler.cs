using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using InnoEngine.Diagnostics;
using InnoEngine.Graphs;
using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Programs;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Flow;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Model.Story.Nodes.Variables;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Compiler.Validation;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using Inno.VisualNovel.Model.Story.Nodes.Audio;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Model.Extensions;
using Inno.VisualNovel.Model.Story.Nodes.Extensions;
using Inno.VisualNovel.Model.Story.Nodes.Interface;

namespace Inno.VisualNovel.Compiler;

/// <summary>Validates a free control-flow graph and lowers it to a source-addressable runtime program.</summary>
public sealed class StoryCompiler
{
    private readonly IStorySerialization m_serialization;
    private readonly StoryNodeDataCodec m_codec;
    private readonly StoryNodeCatalog m_catalog;
    private readonly Extensions.StoryCompilerRegistry? m_extensions;

    /// <summary>Creates a compiler bound to the current serialization generation.</summary>
    /// <param name="serialization">The editor's native serialization owner.</param>
    /// <param name="extensions">Optional generation-owned custom node factories; automatic host discovery requires a public engine contract.</param>
    public StoryCompiler(
        IStorySerialization serialization,
        Extensions.StoryCompilerRegistry? extensions = null
    ) {
        m_serialization = serialization ?? throw new ArgumentNullException(nameof(serialization));
        m_extensions = extensions;
        m_codec = new(serialization);
        m_catalog = new(m_codec);
    }

    /// <summary>Compiles without mutating the source asset or activating a partial program.</summary>
    /// <param name="story">The complete authored chapter.</param>
    /// <returns>A program when valid; otherwise diagnostics preserving source identities.</returns>
    public StoryCompilationResult Compile(StoryAsset story)
    {
        ArgumentNullException.ThrowIfNull(story);
        if (m_extensions is not null) return Extensions.StoryExtensionLowering.Compile(story, m_serialization, m_extensions);
        var diagnostics = new List<GraphDiagnostic>();
        VariableDefinition[] declarations = story.variables;
        if (story.variableTable is not null)
        {
            try { declarations = StoryVariableDeclarations.Merge(story.variables, story.variableTable); }
            catch (Exception failure) when (failure is ArgumentException or InvalidOperationException)
            { return new(null, [new("VN_VARIABLE_TABLE", Explain(failure), DiagnosticSeverity.Error)]); }
        }
        GraphDocument graph = story.graph.document;
        foreach (GraphDiagnostic item in GraphValidator.Validate(graph, m_catalog, allowCycles: true).diagnostics)
            diagnostics.Add(item);
        foreach (GraphNodeRecord node in graph.nodes)
            if (!m_catalog.TryResolve(node.definitionId, out _))
                Error("VN_NODE_MISSING", $"Node implementation is unavailable: {node.definitionId}. Original data is preserved.", node.id.value);
        ValidateStructure();
        if (HasErrors())
            return new(null, diagnostics);

        var expressionTypes = new ExpressionTypeAnalyzer(declarations);
        var variableIds = declarations.Select(static variable => variable.id).ToHashSet(StringComparer.Ordinal);

        var instructions = new List<StoryInstruction>();
        var edgesByOutput = graph.edges.GroupBy(static edge => edge.output.nodeId).ToDictionary(static group => group.Key, static group => group.ToArray());
        foreach (GraphNodeRecord node in graph.nodes)
        {
            try
            {
                var instruction = new StoryInstruction
                {
                    nodeId = node.id.value,
                    sourceChapterId = story.chapterId,
                    sourceNodeId = node.id.value,
                    definitionId = node.definitionId,
                    targets = (edgesByOutput.GetValueOrDefault(node.id) ?? []).Select(edge => new InstructionTarget
                    {
                        port = edge.output.portId.value, nodeId = edge.input.nodeId.value
                    }).OrderBy(static item => item.port, StringComparer.Ordinal).ToArray()
                };
                Lower(node, instruction);
                if (instruction.dialogue is { } line) { ValidateTemplate(line.speaker, node.id.value); ValidateTemplate(line.text, node.id.value); }
                if (instruction.choice is { } choice)
                {
                    ValidateTemplate(choice.prompt, node.id.value);
                    foreach (var option in choice.options) { ValidateTemplate(option.text, node.id.value); ValidateTemplate(option.disabledReason, node.id.value); }
                }
                if (instruction.input is { } input) { ValidateTemplate(input.prompt, node.id.value); ValidateTemplate(input.defaultText, node.id.value); }
                instructions.Add(instruction);
            }
            catch (Exception failure) when (failure is not OutOfMemoryException)
            {
                RawError("VN_NODE_INVALID", failure.Message, node.id.value);
            }
        }
        if (HasErrors())
            return new(null, diagnostics);
        foreach (var issue in new Analysis.ParallelScopeAnalyzer(m_codec).Analyze(story)) Error("VN_PARALLEL_SCOPE", issue.message, issue.nodeId);
        if (HasErrors()) return new(null, diagnostics);
        var program = new StoryProgram
        {
            chapterId = story.chapterId,
            instructions = Lowering.PresentationWaitLowering.Expand(instructions).OrderBy(static item => item.nodeId, StringComparer.Ordinal).ToArray(),
            entries = story.entries.Select(static item => new StoryEntry { id = item.id, nodeId = item.nodeId }).OrderBy(static item => item.id, StringComparer.Ordinal).ToArray(),
            variables = m_serialization.Deserialize<VariableDeclarationSnapshot>(m_serialization.Serialize(new VariableDeclarationSnapshot { items = declarations })).items
        };
        program.fingerprint = Convert.ToHexString(SHA256.HashData(m_serialization.Serialize(program)));
        return new(program, diagnostics);

        bool HasErrors() => diagnostics.Any(static item => item.severity == DiagnosticSeverity.Error);
        void Error(
            string code,
            FormattableString message,
            string nodeId = ""
        ) => Add(code, message, DiagnosticSeverity.Error, nodeId);
        void Warning(
            string code,
            FormattableString message,
            string nodeId
        ) => Add(code, message, DiagnosticSeverity.Warning, nodeId);
        void Add(
            string code,
            FormattableString message,
            DiagnosticSeverity severity,
            string nodeId
        ) {
            var diagnostic = new GraphDiagnostic(code, FormattableString.Invariant(message), severity, nodeId.Length > 0 ? new GraphNodeId(nodeId) : null);
            diagnostics.Add(diagnostic);
        }
        void RawError(
            string code,
            string message,
            string nodeId = ""
        ) => diagnostics.Add(new(code, string.IsNullOrWhiteSpace(message) ? "Validation failed without an explanation." : message,
            DiagnosticSeverity.Error, nodeId.Length > 0 ? new GraphNodeId(nodeId) : null));

        void ValidateStructure()
        {
            if (string.IsNullOrWhiteSpace(story.chapterId))
                Error("VN_CHAPTER_ID", $"The chapter needs a stable ID.");
            if (story.entries.Length == 0)
                Error("VN_ENTRY_REQUIRED", $"The chapter needs at least one entry.");
            var nodeIds = graph.nodes.Select(static item => item.id.value).ToHashSet(StringComparer.Ordinal);
            var entryIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (StoryEntry entry in story.entries)
                if (string.IsNullOrWhiteSpace(entry.id) || !entryIds.Add(entry.id) || !nodeIds.Contains(entry.nodeId))
                    Error("VN_ENTRY_INVALID", $"The entry is invalid or duplicated: {entry.id}.", entry.nodeId);
            var members = new HashSet<string>(StringComparer.Ordinal);
            var fragments = new HashSet<string>(StringComparer.Ordinal);
            foreach (StoryFragment fragment in story.fragments)
            {
                if (string.IsNullOrWhiteSpace(fragment.id) || !fragments.Add(fragment.id))
                    Error("VN_FRAGMENT_ID", $"A fragment ID is empty or duplicated.");
                foreach (string id in fragment.nodes)
                    if (!nodeIds.Contains(id) || !members.Add(id))
                        Error("VN_FRAGMENT_MEMBER", $"A node must exist and belong to exactly one fragment.", id);
            }
            foreach (string id in nodeIds.Where(id => !members.Contains(id)))
                Error("VN_FRAGMENT_REQUIRED", $"The node has not been assigned to a fragment.", id);
            var variables = new HashSet<string>(StringComparer.Ordinal);
            foreach (VariableDefinition variable in declarations)
            {
                if (string.IsNullOrWhiteSpace(variable.id) || !variables.Add(variable.id) || variable.initialValue is null)
                    Error("VN_VARIABLE_INVALID", $"A variable ID is invalid, duplicated or has no default value.");
                else
                    try { VariableValueRules.Validate(variable, variable.initialValue); }
                    catch (InvalidOperationException failure) { RawError("VN_VARIABLE_INVALID", failure.Message); }
            }
            var reachable = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<string>(story.entries.Select(static entry => entry.nodeId));
            var outgoing = graph.edges.GroupBy(static edge => edge.output.nodeId.value).ToDictionary(static group => group.Key, static group => group.Select(edge => edge.input.nodeId.value).ToArray(), StringComparer.Ordinal);
            while (pending.TryPop(out string? id))
                if (reachable.Add(id) && outgoing.TryGetValue(id, out string[]? targets))
                    foreach (string target in targets)
                        pending.Push(target);
            foreach (string id in nodeIds.Where(id => !reachable.Contains(id)))
                Warning("VN_UNREACHABLE", $"This node cannot be reached from any entry.", id);
        }

        void Lower(
            GraphNodeRecord node,
            StoryInstruction instruction
        ) {
            switch (node.definitionId)
            {
                case StoryNodeIds.log:
                    instruction.log = m_codec.Read<Model.Story.Nodes.Debugging.LogNode>(node);
                    if (!Enum.IsDefined(instruction.log.level) || instruction.log.message.Length > 8192)
                        Error("VN_LOG_INVALID", $"A log needs a valid level and at most 8192 source characters.", node.id.value);
                    ValidateTemplate(instruction.log.message, node.id.value);
                    break;
                case StoryNodeIds.system:
                    instruction.system = m_codec.Read<SystemNode>(node); SystemNodeRules.Validate(instruction.system); break;
                case StoryNodeIds.parallel: instruction.parallel = m_codec.Read<ParallelNode>(node); break;
                case StoryNodeIds.join: instruction.join = m_codec.Read<JoinNode>(node); break;
                case StoryNodeIds.command:
                    instruction.command = m_codec.Read<CommandNode>(node);
                    CommandNode command = instruction.command; CommandContract.Validate(command.definition);
                    if (command.arguments.Select(static argument => argument.id).Distinct(StringComparer.Ordinal).Count() != command.arguments.Length
                        || command.arguments.Any(argument => !command.definition.parameters.Any(parameter => parameter.id == argument.id)))
                        Error("VN_COMMAND_ARGUMENT", $"A command argument is empty, duplicated or undeclared.", node.id.value);
                    foreach (var argument in command.arguments)
                    {
                        var parameter = command.definition.parameters.SingleOrDefault(parameter => parameter.id == argument.id); if (parameter is null) continue;
                        ValidateExpression(argument.value, node.id.value, parameter.defaultValue.kind);
                        if (parameter.defaultValue.kind == NovelValueKind.List && expressionTypes.Describe(argument.value).element != parameter.defaultValue.elementKind)
                            Error("VN_COMMAND_LIST", $"A command list argument has the wrong element type.", node.id.value);
                        if (argument.value.operation == ExpressionOperation.Literal) parameter.Validate(argument.value.literal);
                    }
                    if (command.resultVariable.Length > 0)
                    {
                        var commandDestination = declarations.SingleOrDefault(variable => variable.id == command.resultVariable);
                        if (!command.definition.hasResult || commandDestination is null) Error("VN_COMMAND_RESULT", $"A result target requires a command that returns a value and a declared variable.", node.id.value);
                        else VariableValueRules.Validate(commandDestination, command.definition.resultDefault);
                    }
                    break;
                case StoryNodeIds.unlock:
                    instruction.unlock = m_codec.Read<UnlockNode>(node);
                    if (string.IsNullOrWhiteSpace(instruction.unlock.entryId)) Error("VN_UNLOCK_INVALID", $"Specify a collection entry ID.", node.id.value);
                    break;
                case StoryNodeIds.input:
                    instruction.input = m_codec.Read<InputNode>(node);
                    InputNode input = instruction.input;
                    if (!declarations.Any(variable => variable.id == input.variableId && variable.initialValue.kind == NovelValueKind.String)
                        || input.minimumLength < 0 || input.maximumLength < 1 || input.maximumLength > 1024 || input.minimumLength > input.maximumLength)
                        Error("VN_INPUT_INVALID", $"Player input requires a declared String variable and valid grapheme limits, up to 1024.", node.id.value);
                    break;
                case StoryNodeIds.performance:
                    instruction.performance = m_codec.Read<PerformanceNode>(node);
                    PerformanceNode performance = instruction.performance;
                    if (!Enum.IsDefined(performance.operation) || string.IsNullOrWhiteSpace(performance.instanceId) || !double.IsFinite(performance.time) || performance.time < 0
                        || performance.operation == PerformanceOperation.Play && string.IsNullOrWhiteSpace(performance.clipId)
                        || performance.operation == PerformanceOperation.WaitMarker && (string.IsNullOrWhiteSpace(performance.markerId) || performance.markerId.Length > 128)
                        || performance.bindings.Any(static item => string.IsNullOrWhiteSpace(item.slot) || string.IsNullOrWhiteSpace(item.target))
                        || performance.bindings.Select(static item => item.slot).Distinct(StringComparer.Ordinal).Count() != performance.bindings.Length)
                        Error("VN_PERFORMANCE_INVALID", $"A performance needs a valid operation, resource, instance, target bindings and time.", node.id.value);
                    break;
                case StoryNodeIds.audio:
                    instruction.audio = m_codec.Read<AudioNode>(node);
                    if (!Enum.IsDefined(instruction.audio.operation) || string.IsNullOrWhiteSpace(instruction.audio.slot)
                        || instruction.audio.operation == AudioOperation.Play && string.IsNullOrWhiteSpace(instruction.audio.cueId))
                        Error("VN_AUDIO_INVALID", $"An audio instruction needs a valid operation, slot and playback resource.", node.id.value);
                    break;
                case StoryNodeIds.stage:
                    instruction.stage = m_codec.Read<StageNode>(node);
                    StageNode stage = instruction.stage;
                    if (!Enum.IsDefined(stage.operation)
                        || stage.operation is StageOperation.SetStage or StageOperation.ShowCharacter or StageOperation.StartParticles && string.IsNullOrWhiteSpace(stage.resourceId)
                        || stage.operation is StageOperation.ShowCharacter or StageOperation.HideCharacter or StageOperation.BeginSpeaking or StageOperation.EndSpeaking or StageOperation.StartParticles or StageOperation.StopParticles or StageOperation.RemoveParticles or StageOperation.WaitParticles && string.IsNullOrWhiteSpace(stage.target)
                        || stage.operation is StageOperation.ShowCharacter or StageOperation.HideCharacter or StageOperation.BeginSpeaking or StageOperation.EndSpeaking && (stage.target.Contains('/') || stage.target.Contains('\\'))
                        || stage.operation == StageOperation.StartParticles && stage.seed == 0
                        || new[] { stage.transform.x, stage.transform.y, stage.transform.pivotX, stage.transform.pivotY, stage.transform.scaleX, stage.transform.scaleY, stage.transform.rotation, stage.transform.opacity }
                            .Any(static value => !double.IsFinite(value) || Math.Abs(value) > 1e6)
                        || stage.transform.opacity is < 0 or > 1 || stage.operation == StageOperation.SetCamera && stage.transform.scaleX <= 0)
                        Error("VN_STAGE_INVALID", $"A stage instruction needs a valid resource, target and finite transform.", node.id.value);
                    if (stage.operation == StageOperation.FollowCamera && (!StageCameraTargetRules.IsValid(stage.target) || stage.transform.scaleX <= 0
                        || stage.transform.pivotX is < 0 or > 1 || stage.transform.pivotY is < 0 or > 1))
                        Error("VN_CAMERA_FOLLOW", $"Camera follow requires a character/instance or stage/layer target, positive zoom and screen anchors from 0 to 1.", node.id.value);
                    break;
                case StoryNodeIds.screenEffect:
                    instruction.screenEffect = m_codec.Read<ScreenEffectNode>(node);
                    var effect = instruction.screenEffect;
                    if (!Enum.IsDefined(effect.operation) || string.IsNullOrWhiteSpace(effect.instanceId) || !double.IsFinite(effect.strength) || effect.strength is < 0 or > 1
                        || !double.IsFinite(effect.duration) || effect.duration is < 0 or > 3600
                        || effect.operation is ScreenEffectOperation.Set or ScreenEffectOperation.Fade && string.IsNullOrWhiteSpace(effect.resourceId))
                        Error("VN_SCREEN_EFFECT", $"A screen effect needs a valid operation, instance, resource and finite strength/duration.", node.id.value);
                    break;
                case StoryNodeIds.transition:
                    instruction.transition = m_codec.Read<TransitionNode>(node);
                    var transition = instruction.transition;
                    if (!Enum.IsDefined(transition.operation) || !Enum.IsDefined(transition.scope) || !Enum.IsDefined(transition.group) || transition.target.Length > 1024
                        || transition.operation == TransitionOperation.Begin && string.IsNullOrWhiteSpace(transition.resourceId)
                        || transition.operation != TransitionOperation.Begin && transition.nextStage.Length > 0)
                        Error("VN_TRANSITION", $"A transition needs a valid operation, scope and resource. Only starting a transition can replace the stage.", node.id.value);
                    break;
                case StoryNodeIds.dialogue:
                    instruction.dialogue = m_codec.Read<DialogueNode>(node);
                    if (string.IsNullOrWhiteSpace(instruction.dialogue.textId) || instruction.dialogue.characterId.Length > 128 || instruction.dialogue.characterId.Length > 0 && string.IsNullOrWhiteSpace(instruction.dialogue.characterId)
                        || !double.IsFinite(instruction.dialogue.charactersPerSecond) || instruction.dialogue.charactersPerSecond <= 0
                        || !double.IsFinite(instruction.dialogue.punctuationPause) || instruction.dialogue.punctuationPause is < 0 or > 10)
                        Error("VN_DIALOGUE_INVALID", $"Dialogue requires a text ID and positive text speed.", node.id.value);
                    if (!Enum.IsDefined(instruction.dialogue.presentation) || !double.IsFinite(instruction.dialogue.anchorX) || instruction.dialogue.anchorX is < 0 or > 1
                        || !double.IsFinite(instruction.dialogue.anchorY) || instruction.dialogue.anchorY is < 0 or > 1
                        || !double.IsFinite(instruction.dialogue.relativeWidth) || instruction.dialogue.relativeWidth is < 0.1 or > 1)
                        Error("VN_READING_LAYOUT", $"A reading layout needs a valid kind, position from 0 to 1 and width from 0.1 to 1.", node.id.value);
                    try { Inno.VisualNovel.Model.Text.NovelTextMarkup.Parse(instruction.dialogue.text); }
                    catch (FormatException failure) { RawError("VN_TEXT_MARKUP", failure.Message, node.id.value); }
                    if (Model.Text.NovelTextTemplate.Parse(instruction.dialogue.text).Concat(Model.Text.NovelTextTemplate.Parse(instruction.dialogue.speaker))
                        .Where(static token => token.variable).Select(static token => token.value).Distinct(StringComparer.Ordinal).Count() > 256)
                        Error("VN_TEXT_PARAMETERS", $"A dialogue line can reference at most 256 distinct text variables.", node.id.value);
                    break;
                case StoryNodeIds.choice:
                    instruction.choice = m_codec.Read<ChoiceNode>(node);
                    ChoiceNode choice = instruction.choice;
                    if (choice.options.Length == 0 || choice.options.Any(static option => string.IsNullOrWhiteSpace(option.id))
                        || choice.options.Select(static option => option.id).Distinct(StringComparer.Ordinal).Count() != choice.options.Length)
                        Error("VN_CHOICE_INVALID", $"Choices require nonempty, unique IDs.", node.id.value);
                    if (!double.IsFinite(choice.timeout) || choice.timeout < 0 || choice.timeout > 0 && !choice.options.Any(option => option.id == choice.defaultOption))
                        Error("VN_CHOICE_TIMEOUT", $"A timeout requires a valid default choice and a finite, nonnegative duration.", node.id.value);
                    foreach (ChoiceOption option in choice.options)
                    {
                        ValidateExpression(option.visible, node.id.value, NovelValueKind.Boolean);
                        ValidateExpression(option.enabled, node.id.value, NovelValueKind.Boolean);
                    }
                    break;
                case StoryNodeIds.assign:
                    instruction.assignment = m_codec.Read<AssignmentNode>(node);
                    if (!declarations.Any(variable => variable.id == instruction.assignment.variableId))
                        Error("VN_VARIABLE_MISSING", $"The variable does not exist: {instruction.assignment.variableId}.", node.id.value);
                    ValidateExpression(instruction.assignment.value, node.id.value,
                        declarations.FirstOrDefault(variable => variable.id == instruction.assignment.variableId)?.initialValue.kind, allowRandom: true);
                    VariableDefinition? destination = declarations.FirstOrDefault(variable => variable.id == instruction.assignment.variableId);
                    if (destination?.initialValue.kind == NovelValueKind.List && expressionTypes.Describe(instruction.assignment.value).element != destination.initialValue.elementKind)
                        Error("VN_LIST_TYPE", $"List assignment must preserve the declared element type.", node.id.value);
                    break;
                case StoryNodeIds.condition:
                case StoryNodeIds.assertion:
                    instruction.condition = m_codec.Read<ConditionNode>(node);
                    ValidateExpression(instruction.condition.condition, node.id.value, NovelValueKind.Boolean);
                    break;
                case StoryNodeIds.wait:
                    instruction.wait = m_codec.Read<WaitNode>(node);
                    if (!double.IsFinite(instruction.wait.seconds) || instruction.wait.seconds < 0)
                        Error("VN_WAIT_INVALID", $"Wait duration must be finite and nonnegative.", node.id.value);
                    break;
                case StoryNodeIds.call:
                    instruction.call = m_codec.Read<CallNode>(node);
                    if (string.IsNullOrWhiteSpace(instruction.call.entryId) || (instruction.call.chapterId.Length == 0 || instruction.call.chapterId == story.chapterId)
                        && !story.entries.Any(entry => entry.id == instruction.call.entryId))
                        Error("VN_CALL_MISSING", $"The called entry does not exist: {instruction.call.entryId}.", node.id.value);
                    break;
                case StoryNodeIds.random:
                    instruction.random = m_codec.Read<RandomNode>(node);
                    if (instruction.random.branches.Length == 0 || instruction.random.branches.Any(static branch => !double.IsFinite(branch.weight) || branch.weight <= 0)
                        || !double.IsFinite(instruction.random.branches.Sum(static branch => branch.weight)))
                        Error("VN_RANDOM_INVALID", $"Random branches require finite, positive weights.", node.id.value);
                    break;
            }
            if (node.definitionId is not (StoryNodeIds.end or StoryNodeIds.returnFromCall)
                && m_catalog.TryResolve(node.definitionId, out GraphNodeDefinition? definition) && definition is not null)
                foreach (GraphPortDefinition port in definition.GetPorts(node).Where(static port => port.direction == GraphPortDirection.Output))
                    if (!instruction.targets.Any(target => target.port == port.id.value))
                        Error("VN_TARGET_REQUIRED", $"Output {port.displayName} is not connected.", node.id.value);
        }

        void ValidateTemplate(string text, string nodeId)
        {
            foreach (var token in Model.Text.NovelTextTemplate.Parse(text).Where(static token => token.variable))
                if (!variableIds.Contains(token.value)) Error("VN_TEXT_VARIABLE_MISSING", $"Text references an undeclared variable: {token.value}", nodeId);
        }

        void ValidateExpression(
            ExpressionDocument expression,
            string nodeId,
            NovelValueKind? expected = null,
            bool allowRandom = false
        ) {
            try
            {
                NovelValueKind actual = expressionTypes.Infer(expression);
                if (!allowRandom && ExpressionOperations.UsesRandom(expression)) Error("VN_RANDOM_CONTEXT", $"Random ranges may only be evaluated in explicit variable assignments. Conditions and UI queries cannot consume random state.", nodeId);
                if (expected is { } required && required != actual)
                    Error("VN_EXPRESSION_TYPE", $"Expected {required}; the expression returns {actual}.", nodeId);
            }
            catch (InvalidOperationException failure)
            {
                RawError("VN_EXPRESSION_INVALID", failure.Message, nodeId);
            }
        }
    }

    internal static string Explain(Exception failure) => string.IsNullOrWhiteSpace(failure.Message) ? "Validation failed without an explanation." : failure.Message;
}
