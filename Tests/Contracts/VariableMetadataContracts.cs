using System;
using System.Linq;
using Inno.Core.Serialization;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Authoring.UiEditing;
using Inno.VisualNovel.Authoring.Variables;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Programs;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Narrative.Execution;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class VariableMetadataContracts
{
    internal static void NativeDeclarationsCompilationAndClipboard(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        IStorySerialization native,
        SerializationRegistry serialization
    ) {
        var draft = FirstStoryTemplate.Create(codec); var variable = VariableValueRules.Copy(draft.variables.Single());
        variable.group = "角色 / 玩家"; variable.description = "玩家输入的姓名，用于对白插值；随游戏保存。";
        new VariableEditService(codec).Update(draft, variable);
        var restored = serialization.Deserialize<VariableDefinition>(serialization.Serialize(variable));
        Check(restored.group == variable.group && restored.description == variable.description, "Native variable serialization lost author metadata.");
        var program = compiler.Compile(draft.ToAsset()).program ?? throw new InvalidOperationException("Annotated declarations failed ordinary compilation.");
        var copied = StoryProgramCopy.Copy(program);
        Check(copied.variables.Single().group == variable.group && copied.variables.Single().description == variable.description, "Program ownership copies dropped variable metadata.");
        copied.variables[0].description = "changed"; Check(program.variables[0].description == variable.description, "A copied declaration changed the original program.");
        using var machine = new StoryMachine(program); machine.Start(); Check(machine.dialogueText.Contains("旅人", StringComparison.Ordinal), "Annotation changes altered variable identity, value or story execution.");
        var clipboard = new StoryClipboardService(native, codec); var target = FirstStoryTemplate.Create(codec); target.variables = [];
        clipboard.Paste(target, "opening", clipboard.Capture(draft, ["hello"]), new(0, 0), variableTargets: new System.Collections.Generic.Dictionary<string, string> { ["player"] = "new-player" });
        Check(target.variables.Single().id == "new-player" && target.variables.Single().description == variable.description && target.variables.Single().group == variable.group,
            "A reviewed new variable mapping lost author documentation.");
        var semantic = VariableValueRules.Copy(variable); semantic.group = "不同的整理分组"; semantic.description = "其他说明";
        Check(StoryVariableDeclarations.Equivalent(variable, semantic), "Author annotations incorrectly changed the semantic handoff contract.");
    }
    internal static void ReusableUiFieldsAndAtomicBounds()
    {
        var field = new VariableDefinition { id = "expanded", name = "展开", group = "面板", description = "本次页面展开状态", scope = VariableScope.Local };
        var screen = new ScreenAsset { stateVariables = [field], root = new() { id = "root", children = [new() { id = "toggle", kind = UiElementKind.Toggle, binding = "page.expanded" }] } };
        var template = UiWidgetEditService.Extract(screen, "toggle", "Annotated toggle");
        var destination = new ScreenAsset { root = new() { id = "root" } }; UiWidgetEditService.Insert(destination, "root", template);
        Check(destination.stateVariables.Single().id != field.id && destination.stateVariables.Single().description == field.description
            && destination.stateVariables.Single().group == field.group, "UI component field identity rebuilding lost its documentation.");
        var table = new VariableTableAsset(); var definition = VariableValueRules.Copy(field); definition.scope = VariableScope.Game; VariableTableEditService.Set(table, definition);
        var invalid = VariableValueRules.Copy(definition); invalid.group = new('g', 129); Reject(() => VariableTableEditService.Set(table, invalid));
        invalid = VariableValueRules.Copy(definition); invalid.description = new('d', 4097); Reject(() => VariableTableEditService.Set(table, invalid));
        Check(table.variables.Single().description == definition.description && table.variables.Single().group == definition.group,
            "An invalid metadata edit partially changed the authoritative table.");
    }
    private static void Reject(Action operation) { try { operation(); } catch (InvalidOperationException) { return; } throw new InvalidOperationException("Unbounded variable annotations were accepted."); }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}
