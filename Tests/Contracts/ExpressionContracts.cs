using System;
using System.Linq;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Model.Story.Nodes.Variables;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Narrative.Values;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class ExpressionContracts
{
    internal static void EnumListDeclarations(
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var declaration = new VariableDefinition { id = "flags", members = ["sun", "rain"], initialValue = NovelValue.List(NovelValueKind.Enum, [NovelValue.Enum("sun")]) };
        var store = new VariableStore([declaration]);
        declaration.members[0] = "changed"; declaration.initialValue = NovelValue.String("changed");
        store.Set("flags", NovelValue.List(NovelValueKind.Enum, [NovelValue.Enum("rain")]));
        Throws<InvalidOperationException>(() => store.Set("flags", NovelValue.List(NovelValueKind.Enum, [NovelValue.Enum("missing")])));
        Check(store.Get("flags").items.Single().text == "rain", "An invalid enum-list assignment mutated the previous value.");
        var state = store.Capture(); state[0].value = NovelValue.List(NovelValueKind.Enum, [NovelValue.Enum("missing")]);
        Throws<InvalidOperationException>(() => store.Restore(state));
        Check(store.Get("flags").items.Single().text == "rain", "An invalid enum-list restore mutated the previous value.");
        var draft = FirstStoryTemplate.Create(codec);
        draft.variables = [new() { id = "flags", members = ["sun"], initialValue = NovelValue.List(NovelValueKind.Enum, [NovelValue.Enum("rain")]) }];
        Check(!compiler.Compile(draft.ToAsset()).success, "Compilation accepted an undeclared enum-list member.");
    }

    internal static void TypedListsAndRanges(
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var evaluator = new ExpressionEvaluator(_ => throw new InvalidOperationException("Unexpected variable lookup."), () => Math.BitDecrement(1d));
        var list = Literal(NovelValue.List(NovelValueKind.String, [NovelValue.String("a"), NovelValue.String("b")]));
        NovelValue inserted = evaluator.Evaluate(Operation(ExpressionOperation.ListInsert, list, Literal(NovelValue.Integer(1)), Literal(NovelValue.String("x"))));
        Check(inserted.items[1].text == "x" && list.literal.items.Length == 2, "List insertion mutated its source or lost item ordering.");
        NovelValue set = evaluator.Evaluate(Operation(ExpressionOperation.ListSet, Literal(inserted), Literal(NovelValue.Integer(0)), Literal(NovelValue.String("z"))));
        Check(set.items[0].text == "z" && inserted.items[0].text == "a", "List replacement retained shared storage.");
        Check(evaluator.Evaluate(Operation(ExpressionOperation.ListCount, Literal(set))).integer == 3, "List count has the wrong type or value.");
        Check(evaluator.Evaluate(Operation(ExpressionOperation.ListContains, Literal(set), Literal(NovelValue.String("x")))).boolean, "Typed list lookup failed.");
        Throws<InvalidOperationException>(() => evaluator.Evaluate(Operation(ExpressionOperation.ListGet, list, Literal(NovelValue.Integer(2)))));
        Throws<ArgumentException>(() => evaluator.Evaluate(Operation(ExpressionOperation.ListAppend, list, Literal(NovelValue.Integer(2)))));
        ExpressionDocument range = Operation(ExpressionOperation.RandomInteger, Literal(NovelValue.Integer(long.MinValue)), Literal(NovelValue.Integer(long.MaxValue)));
        long sampled = evaluator.Evaluate(range).integer;
        Check(sampled >= long.MinValue && sampled < long.MaxValue, "Random integer overflowed or reached its exclusive upper bound.");
        range.operands = [Literal(NovelValue.Integer(1)), Literal(NovelValue.Integer(2))];
        Check(evaluator.Evaluate(range).integer == 1, "Near-one random samples escaped a unit-width range.");
        double minimum = 1, maximum = Math.BitIncrement(1d);
        double number = evaluator.Evaluate(Operation(ExpressionOperation.RandomNumber, Literal(NovelValue.Number(minimum)), Literal(NovelValue.Number(maximum)))).number;
        Check(number == minimum && number < maximum, "Floating point rounding reached an exclusive random upper bound.");
        Throws<InvalidOperationException>(() => new ExpressionEvaluator(_ => NovelValue.Boolean(false)).Evaluate(range));
        Check(evaluator.Evaluate(Operation(ExpressionOperation.Greater, Literal(NovelValue.Integer(long.MaxValue)), Literal(NovelValue.Integer(long.MaxValue - 1)))).boolean, "Integer comparison lost precision through a double conversion.");
        var draft = FirstStoryTemplate.Create(codec);
        ChoiceNode choice = codec.Read<ChoiceNode>(draft.graph.document.FindNode(new("choice"))!);
        choice.options[0].visible = Operation(ExpressionOperation.Greater, range, Literal(NovelValue.Integer(0)));
        codec.Write(draft.graph.document.FindNode(new("choice"))!, choice);
        Check(!compiler.Compile(draft.ToAsset()).success, "Repeated choice queries were allowed to consume random state.");
        draft = FirstStoryTemplate.Create(codec);
        draft.variables = [.. draft.variables, new() { id = "items", initialValue = NovelValue.List(NovelValueKind.Integer, []) }];
        var edits = new Authoring.StoryEditing.StoryEditService(codec);
        var assign = edits.Add(draft, "opening", Model.Story.StoryNodeIds.assign, new AssignmentNode { variableId = "items", value = list }, new(0, 400));
        edits.Connect(draft, new(assign, new("next")), new("end"));
        Check(!compiler.Compile(draft.ToAsset()).success, "Compile-time list assignment lost its element type.");
    }
    private static ExpressionDocument Literal(NovelValue value) => new() { literal = value };
    private static ExpressionDocument Operation(ExpressionOperation operation, params ExpressionDocument[] operands) => new() { operation = operation, operands = operands };
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }
}
