using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using InnoEngine.Graphs;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Extensions;
using Inno.VisualNovel.Model.Story.Nodes.Flow;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Model.Story.Nodes.Variables;
using Inno.VisualNovel.Model.Text;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Authoring.Diagnostics;

namespace Inno.VisualNovel.Authoring.StoryEditing;

internal sealed class StoryClipboardVariables(StoryNodeDataCodec codec)
{
    internal void Remap(
        StoryClipboardState source,
        IReadOnlyDictionary<string, string> requested,
        IReadOnlyDictionary<string, VariableDefinition> destination
    ) {
        var map = requested.ToDictionary(static item => item.Key, static item => item.Value, StringComparer.Ordinal);
        var declarations = source.variables.ToDictionary(static item => item.id, StringComparer.Ordinal);
        foreach (var item in map)
            if (!declarations.ContainsKey(item.Key) || string.IsNullOrWhiteSpace(item.Value) || item.Value.Length > 256 || item.Value.IndexOfAny(['{', '}', '[', ']']) >= 0)
                throw new AuthoringValidationException($"复制变量映射的来源或目标身份无效：{item.Key}");
        string[] finalIds = source.variables.Select(variable => Map(variable.id)).ToArray();
        if (finalIds.Distinct(StringComparer.Ordinal).Count() != finalIds.Length) throw new AuthoringValidationException($"多个源变量不能隐式合并为同一目标；请分别选择目标或导入为新变量。");
        var candidates = source.variables.Select(variable =>
        {
            if (!map.TryGetValue(variable.id, out string? id)) return VariableValueRules.Copy(variable);
            if (destination.TryGetValue(id, out var target))
            {
                VariableValueRules.Validate(target, variable.initialValue);
                bool enumeration = variable.initialValue.kind == NovelValueKind.Enum || variable.initialValue.kind == NovelValueKind.List && variable.initialValue.elementKind == NovelValueKind.Enum;
                if (enumeration && variable.members.Any(member => !target.members.Contains(member, StringComparer.Ordinal))) throw new AuthoringValidationException($"目标枚举没有包含复制变量的全部成员：{id}");
                return VariableValueRules.Copy(target);
            }
            var created = VariableValueRules.Copy(variable); created.id = id; return created;
        }).ToArray();
        var catalog = new StoryNodeCatalog(codec);
        foreach (var node in source.graph.document.nodes)
        {
            if (!catalog.TryResolve(node.definitionId, out _)) throw new AuthoringValidationException($"缺失扩展的变量引用无法安全改写；请先恢复该扩展：{node.id.value}");
            switch (node.definitionId)
            {
                case StoryNodeIds.log:
                    var log = codec.Read<Model.Story.Nodes.Debugging.LogNode>(node); log.message = Text(log.message); codec.Write(node, log); break;
                case StoryNodeIds.dialogue:
                    var line = codec.Read<DialogueNode>(node); line.text = Text(line.text); line.speaker = Text(line.speaker); codec.Write(node, line); break;
                case StoryNodeIds.input:
                    var input = codec.Read<InputNode>(node); input.variableId = Map(input.variableId); input.prompt = Text(input.prompt); input.defaultText = Text(input.defaultText); codec.Write(node, input); break;
                case StoryNodeIds.choice:
                    var choice = codec.Read<ChoiceNode>(node); choice.prompt = Text(choice.prompt);
                    foreach (var option in choice.options) { option.text = Text(option.text); option.disabledReason = Text(option.disabledReason); Expression(option.visible); Expression(option.enabled); }
                    codec.Write(node, choice); break;
                case StoryNodeIds.assign:
                    var assignment = codec.Read<AssignmentNode>(node); assignment.variableId = Map(assignment.variableId); Expression(assignment.value); codec.Write(node, assignment); break;
                case StoryNodeIds.condition:
                case StoryNodeIds.assertion:
                    var condition = codec.Read<ConditionNode>(node); Expression(condition.condition); codec.Write(node, condition); break;
                case StoryNodeIds.command:
                    var command = codec.Read<CommandNode>(node); command.resultVariable = Map(command.resultVariable);
                    foreach (var argument in command.arguments) Expression(argument.value);
                    codec.Write(node, command); break;
            }
        }
        source.variables = candidates;

        string Map(string id) => map.GetValueOrDefault(id) ?? id;
        string Text(string text)
        {
            var result = new StringBuilder();
            foreach (var token in NovelTextTemplate.Parse(text))
            {
                if (token.variable && map.TryGetValue(token.value, out string? replacement))
                {
                    if (result.Length > 1_000_000 - replacement.Length - 2) throw new AuthoringValidationException($"映射后的文字超过字符预算。");
                    result.Append('{').Append(replacement).Append('}');
                }
                else
                {
                    if (result.Length > 1_000_000 - token.length) throw new AuthoringValidationException($"映射后的文字超过字符预算。");
                    result.Append(text, token.start, token.length);
                }
            }
            return result.ToString();
        }
        void Expression(
            ExpressionDocument expression,
            int depth = 0
        ) {
            if (depth >= 64) throw new AuthoringValidationException($"映射表达式超过深度预算。");
            if (expression.operation == ExpressionOperation.Variable) expression.variableId = Map(expression.variableId);
            foreach (var operand in expression.operands) Expression(operand, depth + 1);
        }
    }
}
