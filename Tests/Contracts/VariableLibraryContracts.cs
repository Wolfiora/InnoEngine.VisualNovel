using System;
using System.Linq;
using Inno.Core.Serialization;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Authoring.Variables;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Narrative.Execution;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class VariableLibraryContracts
{
    internal static void CompilationAndOwnership(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var draft = FirstStoryTemplate.Create(codec); var source = draft.ToAsset();
        string original = compiler.Compile(source).program!.fingerprint;
        var table = new VariableTableAsset { variables = source.variables.Select(VariableValueRules.Copy).ToArray() };
        source.variables = []; source.variableTable = table;
        var compiled = compiler.Compile(source);
        Check(compiled.success && compiled.program!.fingerprint == original && source.variables.Length == 0,
            "Changing declaration authority altered narrative semantics or duplicated declarations in the source.");
        table.variables[0].initialValue = NovelValue.String("Changed source");
        using var machine = new StoryMachine(compiled.program!); machine.Start();
        Check(machine.GetVariable("player").text == "旅人" && machine.dialogueText.Contains("旅人", StringComparison.Ordinal), "Compiled declarations retained mutable library defaults.");
        var old = table.variables[0].initialValue;
        Reject(() => VariableTableEditService.Set(table, new() { id = "local", scope = VariableScope.Local, initialValue = NovelValue.Boolean(false) }));
        Check(table.variables.Length == 1 && table.variables[0].initialValue.Equals(old), "A rejected shared lifetime partially changed the library.");
        source.variables = [VariableValueRules.Copy(table.variables[0])];
        var conflict = compiler.Compile(source);
        Check(!conflict.success && conflict.diagnostics.Any(static item => item.code == "VN_VARIABLE_TABLE"), "Duplicate chapter/library identities were implicitly overridden.");
        source.variables = []; table.variables[0].scope = VariableScope.Local;
        Check(!compiler.Compile(source).success, "A call-local declaration was allowed to become a cross-chapter authority.");
        table.variables[0].scope = VariableScope.Global; StoryVariableDeclarations.Validate(table);
        var copied = table.Copy(); copied.variables[0].initialValue = NovelValue.String("Copy only");
        Check(table.variables[0].initialValue.text == "Changed source", "Copying a library retained mutable typed values.");
        var definition = native.Deserialize<VariableDefinition>(native.Serialize(table.variables[0]));
        Check(definition.scope == VariableScope.Global && StoryVariableDeclarations.Equivalent(table.variables[0], definition), "Native shared metadata lost lifetime or defaults.");
    }
    private static void Reject(Action operation) { try { operation(); } catch (InvalidOperationException) { return; } throw new InvalidOperationException("An invalid shared declaration edit was accepted."); }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}
