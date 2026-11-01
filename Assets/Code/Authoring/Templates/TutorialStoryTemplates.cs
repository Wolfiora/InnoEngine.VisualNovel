using System;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Flow;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Model.Story.Nodes.Variables;
using Inno.VisualNovel.Model.Story.Nodes.Interface;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Authoring.Templates;

/// <summary>Builds editable tutorial graphs using the same node contracts as both authoring views.</summary>
public static class TutorialStoryTemplates
{
    /// <summary>Creates a minimal runnable story without adding characters or presentation instructions.</summary>
    /// <param name="codec">The native graph settings codec.</param>
    /// <returns>A fresh entry, line and ending.</returns>
    public static StoryDraftState Blank(StoryNodeDataCodec codec)
    {
        var builder = new StoryTemplateBuilder(codec, "空白故事"); builder.Add("entry", StoryNodeIds.entry);
        builder.Add("hello", StoryNodeIds.dialogue, Line("从这里写下你的第一句对白。")); builder.Add("end", StoryNodeIds.end);
        builder.Link("entry", "hello"); builder.Link("hello", "end"); return builder.Build();
    }
    /// <summary>Creates a bounded branching loop with player input, typed assignments and a reusable called fragment.</summary>
    /// <param name="codec">The native graph settings codec.</param>
    /// <returns>A complete two-visit route lesson with a genuine call and return.</returns>
    public static StoryDraftState Branching(StoryNodeDataCodec codec)
    {
        var b = new StoryTemplateBuilder(codec, "分支、循环与片段调用"); b.Add("entry", StoryNodeIds.entry);
        b.Add("name", StoryNodeIds.input, new InputNode { variableId = "player", prompt = "如何称呼你？", defaultText = "旅人", maximumLength = 16 });
        b.Add("hello", StoryNodeIds.dialogue, Line("你好，{player}。每次选择都会增加访问次数；两次之后会走向结尾。"));
        b.Add("choice", StoryNodeIds.choice, new ChoiceNode { prompt = "下一站去哪里？", options = [new() { id = "sea", text = "去看海" }, new() { id = "forest", text = "走进森林" }] });
        b.Add("sea", StoryNodeIds.assign, new AssignmentNode { variableId = "route", value = Literal(NovelValue.String("海边")) });
        b.Add("forest", StoryNodeIds.assign, new AssignmentNode { variableId = "route", value = Literal(NovelValue.String("森林")) });
        b.Add("count", StoryNodeIds.assign, new AssignmentNode { variableId = "visits", value = new() { operation = ExpressionOperation.Add,
            operands = [new() { operation = ExpressionOperation.Variable, variableId = "visits" }, Literal(NovelValue.Integer(1))] } });
        b.Add("call", StoryNodeIds.call, new CallNode { entryId = "describe" });
        b.Add("again", StoryNodeIds.condition, new ConditionNode { condition = new() { operation = ExpressionOperation.Less,
            operands = [new() { operation = ExpressionOperation.Variable, variableId = "visits" }, Literal(NovelValue.Integer(2))] } });
        b.Add("finish", StoryNodeIds.dialogue, Line("你完成了 {visits} 次选择。图中的回路、汇合与片段调用，全部可以在剧本视图中修改。"));
        b.Add("end", StoryNodeIds.end); b.Add("description", StoryNodeIds.dialogue, Line("第 {visits} 次出发：{route}。返回后继续原来的调用位置。"));
        b.Add("return", StoryNodeIds.returnFromCall);
        b.Link("entry", "name"); b.Link("name", "hello"); b.Link("hello", "choice"); b.Link("choice", "sea", "sea"); b.Link("choice", "forest", "forest");
        b.Link("sea", "count"); b.Link("forest", "count"); b.Link("count", "call"); b.Link("call", "again"); b.Link("description", "return");
        b.Link("again", "choice", "true"); b.Link("again", "finish", "false"); b.Link("finish", "end");
        return b.Build([new() { id = "player", name = "玩家姓名", initialValue = NovelValue.String("旅人") },
            new() { id = "route", name = "当前路线", initialValue = NovelValue.String("") }, new() { id = "visits", name = "访问次数", initialValue = NovelValue.Integer(0) }],
            [new() { id = "start", nodeId = "entry" }, new() { id = "describe", nodeId = "description" }]);
    }
    /// <summary>Creates five reading layouts with a real modal settings pause and named rollback boundary.</summary>
    /// <param name="codec">The native graph settings codec.</param>
    /// <returns>A runnable UI lesson without script or side effects hidden in animation tracks.</returns>
    public static StoryDraftState Interface(StoryNodeDataCodec codec)
    {
        var b = new StoryTemplateBuilder(codec, "阅读布局与游戏界面"); b.Add("entry", StoryNodeIds.entry);
        b.Add("hello", StoryNodeIds.dialogue, Line("每个页面都可以在界面设计器中修改。下一步会打开真正的设置页，关闭后剧情才继续。"));
        b.Add("settings", StoryNodeIds.system, new SystemNode { operation = SystemOperation.OpenPage, argument = "settings" });
        b.Add("checkpoint", StoryNodeIds.system, new SystemNode { operation = SystemOperation.Checkpoint, argument = "reading-layouts" });
        string previous = "checkpoint";
        foreach (var mode in Enum.GetValues<DialoguePresentation>())
        {
            string id = "layout-" + mode.ToString().ToLowerInvariant(); var line = Line(mode + "：同一剧情数据使用不同阅读布局。可测试文字缩放、隐藏、历史与回滚。");
            line.presentation = mode; line.clearPage = true; line.anchorX = .15; line.anchorY = .2; line.relativeWidth = .65;
            b.Add(id, StoryNodeIds.dialogue, line); b.Link(previous, id); previous = id;
        }
        b.Add("end", StoryNodeIds.end); b.Link("entry", "hello"); b.Link("hello", "settings"); b.Link("settings", "checkpoint"); b.Link(previous, "end"); return b.Build();
    }
    internal static DialogueNode Line(string text) => new() { textId = Guid.NewGuid().ToString("N"), text = text };
    private static ExpressionDocument Literal(NovelValue value) => new() { literal = value };
}
