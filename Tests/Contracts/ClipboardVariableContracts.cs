using System;
using System.Collections.Generic;
using System.Linq;
using Inno.Core.Graphs;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Extensions;
using Inno.VisualNovel.Model.Story.Nodes.Flow;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Model.Story.Nodes.Variables;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Narrative.Extensions;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Samples.ScriptExtension;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class ClipboardVariableContracts
{
    internal static void TypedRemappingAndActualCommandExecution(
        IStorySerialization native,
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var source = Source(codec); var target = Target(codec); var clipboard = new StoryClipboardService(native, codec);
        byte[] original = native.Serialize(source), bytes = Copy(clipboard, source);
        var review = clipboard.Review(bytes); var definitions = review.CopyVariables(); definitions[0].id = "changed-outside";
        Check(review.CopyVariables().All(static definition => definition.id != "changed-outside"), "Variable conflict review exposed its captured declaration storage.");
        string[] pasted = clipboard.Paste(target, "opening", bytes, new(0, 0), variableTargets: new Dictionary<string, string>
            { ["player"] = "hero", ["other"] = "guest", ["score"] = "points" });
        Check(target.variables.Single(static item => item.id == "hero").scope == VariableScope.Global
            && target.variables.Single(static item => item.id == "hero").initialValue.text == "目标默认"
            && target.variables.Single(static item => item.id == "points").initialValue.integer == 30
            && target.variables.Single(static item => item.id == "guest").initialValue.text == "Other", "Explicit mapping overwrote target defaults/scope or lost imported declarations.");
        var nodes = pasted.Select(id => target.graph.document.FindNode(new(id))!).ToArray();
        var condition = nodes.Single(static node => node.definitionId == StoryNodeIds.condition);
        new StoryEditService(codec).Connect(target, new(new("entry"), new("next")), condition.id);
        var copiedChoice = codec.Read<ChoiceNode>(nodes.Single(static node => node.definitionId == StoryNodeIds.choice));
        Check(copiedChoice.prompt.Contains("{hero}", StringComparison.Ordinal) && copiedChoice.options.All(static option => option.disabledReason == "{hero} disabled"),
            "Choice prompts or disabled explanations retained source variable identities.");
        var command = codec.Read<CommandNode>(nodes.Single(static node => node.definitionId == StoryNodeIds.command));
        Check(command.resultVariable == "points" && command.arguments.Single(static argument => argument.id == "score").value.operands[0].variableId == "points",
            "Command arguments/results were not remapped, or stable command parameter identities were renamed.");
        var compiled = compiler.Compile(target.ToAsset()); Check(compiled.success, string.Join("; ", compiled.diagnostics.Select(static issue => issue.message)));
        var chapter = target.ToAsset(); chapter.program = compiled.program; var registry = new NovelCommandRegistry(); registry.Register(static () => new DelayScoreCommand());
        using var session = new NovelSession(new NovelProjectAsset { chapters = [chapter], startChapter = chapter.chapterId }, commands: registry);
        session.StartNew();
        Check(session.story!.status == StoryStatus.Input && session.story.inputText == "Other!", "Condition, assignment or input default references did not reach mapped variables.");
        session.story.SetInputText("玩家新名"); session.SubmitInput(); session.Tick(0);
        Check(session.story.status == StoryStatus.Dialogue && session.story.speaker == "玩家新名" && session.story.dialogueText == "玩家新名|Other!|{player}"
            && session.story.GetVariable("points").integer == 32 && session.story.GetVariable("player").text == "原有玩家",
            "Mapped runtime flow lost interpolation escapes, assertion, typed command results or unrelated target variables.");
        session.Advance(); session.Advance(); Check(session.story.status == StoryStatus.Choice && session.story.choices.Count == 2 && session.story.choices.All(static choice => choice.enabled),
            "Mapped visible/enabled expressions did not use the actual new variable state.");
        Check(native.Serialize(source).SequenceEqual(original) && clipboard.Review(bytes).CopyVariables().Any(static variable => variable.id == "player"),
            "Target remapping rewrote source content or the native clipboard buffer.");
    }

    internal static void SimultaneousMappingsAndAtomicConflicts(
        IStorySerialization native,
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var source = Source(codec); var target = Target(codec); var clipboard = new StoryClipboardService(native, codec); byte[] bytes = Copy(clipboard, source);
        target.variables = [.. target.variables, new() { id = "flag", name = "Wrong type", initialValue = NovelValue.Boolean(false) }];
        byte[] before = native.Serialize(target);
        Reject(() => clipboard.Paste(target, "opening", bytes, new(0, 0), variableTargets: new Dictionary<string, string> { ["player"] = "flag" }));
        Reject(() => clipboard.Paste(target, "opening", bytes, new(0, 0), variableTargets: new Dictionary<string, string> { ["player"] = "other" }));
        Reject(() => clipboard.Paste(target, "opening", bytes, new(0, 0), variableTargets: new Dictionary<string, string> { ["missing"] = "hero" }));
        Check(native.Serialize(target).SequenceEqual(before), "Rejected type, merging or unknown mapping partially changed target nodes or declarations.");
        string[] pasted = clipboard.Paste(target, "opening", bytes, new(0, 0), variableTargets: new Dictionary<string, string>
            { ["player"] = "other", ["other"] = "player", ["score"] = "points" });
        var copied = pasted.Select(id => target.graph.document.FindNode(new(id))!).Single(node => node.definitionId == StoryNodeIds.dialogue && codec.Read<DialogueNode>(node).text.Contains('|'));
        Check(codec.Read<DialogueNode>(copied).text == "{other}|{player}|{{player}" && compiler.Compile(target.ToAsset()).success,
            "Simultaneous variable swaps cascaded through already renamed identifiers or altered escaped literals.");

        source = FirstStoryTemplate.Create(codec); source.variables = [new() { id = "tone", name = "Tone", initialValue = NovelValue.Enum("happy"), members = ["happy", "sad"] }];
        var edits = new StoryEditService(codec); var assignment = edits.Add(source, "opening", StoryNodeIds.assign,
            new AssignmentNode { variableId = "tone", value = new() { literal = NovelValue.Enum("happy") } }, new(0, 0));
        bytes = clipboard.Capture(source, [assignment.value]); target = Target(codec); target.variables = [.. target.variables,
            new() { id = "tone-target", name = "Restricted tone", initialValue = NovelValue.Enum("happy"), members = ["happy"] }];
        before = native.Serialize(target);
        Reject(() => clipboard.Paste(target, "opening", bytes, new(0, 0), variableTargets: new Dictionary<string, string> { ["tone"] = "tone-target" }));
        Check(native.Serialize(target).SequenceEqual(before), "An incompatible enum target accepted undeclared source members.");

        source = Source(codec); var missing = new GraphNodeRecord(new("missing-extension"), "custom.missing"); source.graph.document.AddNode(missing);
        source.fragments[0].nodes = [.. source.fragments[0].nodes, missing.id.value]; bytes = Copy(clipboard, source); before = native.Serialize(target);
        Reject(() => clipboard.Paste(target, "opening", bytes, new(0, 0), variableTargets: new Dictionary<string, string> { ["player"] = "hero" }));
        Check(native.Serialize(target).SequenceEqual(before), "Missing extension bytes were guessed during variable rewriting or partially committed.");
    }
    private static StoryDraftState Source(StoryNodeDataCodec codec)
    {
        var draft = FirstStoryTemplate.Create(codec); var edits = new StoryEditService(codec);
        draft.variables = [.. draft.variables, new() { id = "other", name = "Other", initialValue = NovelValue.String("Other") }, new() { id = "score", name = "Score", initialValue = NovelValue.Integer(5) }];
        var line = codec.Read<DialogueNode>(draft.graph.document.FindNode(new("hello"))!); line.speaker = "{player}"; line.text = "{player}|{other}|{{player}"; edits.SetSettings(draft, new("hello"), line);
        var choice = codec.Read<ChoiceNode>(draft.graph.document.FindNode(new("choice"))!); choice.prompt = "{player}?";
        foreach (var option in choice.options) { option.text += " {other}"; option.disabledReason = "{player} disabled"; option.visible = Equal("player", "玩家新名"); option.enabled = Equal("other", "Other!"); }
        edits.SetSettings(draft, new("choice"), choice);
        var condition = edits.Add(draft, "opening", StoryNodeIds.condition, new ConditionNode { condition = Equal("other", "Other") }, new(0, 0));
        var assignment = edits.Add(draft, "opening", StoryNodeIds.assign, new AssignmentNode { variableId = "other", value = Add("other", NovelValue.String("!")) }, new(0, 100));
        var input = edits.Add(draft, "opening", StoryNodeIds.input, new InputNode { variableId = "player", prompt = "{player}", defaultText = "{other}" }, new(0, 200));
        var assertion = edits.Add(draft, "opening", StoryNodeIds.assertion, new ConditionNode { condition = Equal("player", "玩家新名") }, new(0, 300));
        using var sample = new DelayScoreCommand();
        var command = edits.Add(draft, "opening", StoryNodeIds.command, new CommandNode { definition = sample.definition, resultVariable = "score", arguments = [
            new() { id = "seconds", value = new() { literal = NovelValue.Number(0) } }, new() { id = "score", value = Add("score", NovelValue.Integer(2)) }] }, new(0, 400));
        edits.Connect(draft, new(new("entry"), new("next")), condition); edits.Connect(draft, new(condition, new("true")), assignment); edits.Connect(draft, new(condition, new("false")), new("end"));
        edits.Connect(draft, new(assignment, new("next")), input); edits.Connect(draft, new(input, new("next")), assertion);
        edits.Connect(draft, new(assertion, new("next")), command); edits.Connect(draft, new(command, new("next")), new("hello")); edits.Connect(draft, new(command, new("cancel")), new("end"));
        return draft;
    }
    private static StoryDraftState Target(StoryNodeDataCodec codec)
    {
        var target = FirstStoryTemplate.Create(codec); target.variables[0].initialValue = NovelValue.String("原有玩家");
        target.variables = [.. target.variables, new() { id = "hero", name = "Hero", scope = VariableScope.Global, initialValue = NovelValue.String("目标默认") },
            new() { id = "points", name = "Points", initialValue = NovelValue.Integer(30) }]; return target;
    }
    private static byte[] Copy(
        StoryClipboardService clipboard,
        StoryDraftState source
    ) => clipboard.Capture(source, source.graph.document.nodes.Where(static node => node.id.value != "entry").Select(static node => node.id.value).ToArray());
    private static ExpressionDocument Equal(
        string variable,
        string value
    ) => new() { operation = ExpressionOperation.Equal, operands = [new() { operation = ExpressionOperation.Variable, variableId = variable }, new() { literal = NovelValue.String(value) }] };
    private static ExpressionDocument Add(
        string variable,
        NovelValue value
    ) => new() { operation = ExpressionOperation.Add, operands = [new() { operation = ExpressionOperation.Variable, variableId = variable }, new() { literal = value }] };
    private static void Reject(Action action)
    { try { action(); } catch (Exception error) when (error is ArgumentException or InvalidOperationException or FormatException) { return; } throw new InvalidOperationException("An incompatible copied variable map was accepted."); }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}
