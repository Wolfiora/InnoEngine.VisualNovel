using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Extensions;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Extensions;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Authoring.Templates;

/// <summary>Builds the matching graph and schema for the distributed restorable C# command lesson.</summary>
public static class ExtensionStoryTemplate
{
    /// <summary>Creates a fresh schema matching the shipped DelayScoreCommand source exactly.</summary>
    /// <returns>The explicit generation-neutral authoring contract.</returns>
    public static NovelCommandDefinition Definition() => new()
    {
        id = "sample.delay-score", title = "延时返回分数", help = "等待逻辑时间后返回类型化 Integer 结果，可保存与回滚。",
        capabilities = CommandCapabilities.Preview | CommandCapabilities.Save | CommandCapabilities.Rollback | CommandCapabilities.Cancel,
        hasResult = true, resultDefault = NovelValue.Integer(0), parameters = [
            new() { id = "seconds", label = "等待秒数", defaultValue = NovelValue.Number(1) },
            new() { id = "score", label = "返回分数", defaultValue = NovelValue.Integer(10) }]
    };
    /// <summary>Creates a real typed command invocation with result and cancellation paths.</summary>
    /// <param name="codec">The native node settings codec.</param>
    /// <returns>A chapter requiring explicit registration of the accompanying C# sample.</returns>
    public static StoryDraftState Create(StoryNodeDataCodec codec)
    {
        var b = new StoryTemplateBuilder(codec, "C# 自定义指令与中立恢复状态"); b.Add("entry", StoryNodeIds.entry);
        b.Add("hello", StoryNodeIds.dialogue, TutorialStoryTemplates.Line("下一步调用 sample.delay-score。等待时可取消、保存或恢复；成功后会把 Integer 结果写入分数变量。"));
        b.Add("command", StoryNodeIds.command, new CommandNode { definition = Definition(), resultVariable = "score" });
        b.Add("result", StoryNodeIds.dialogue, TutorialStoryTemplates.Line("自定义指令返回了 {score} 分。"));
        b.Add("cancelled", StoryNodeIds.dialogue, TutorialStoryTemplates.Line("已取消等待，分数保持原值 {score}。")); b.Add("end", StoryNodeIds.end);
        b.Link("entry", "hello"); b.Link("hello", "command"); b.Link("command", "result"); b.Link("command", "cancelled", "cancel"); b.Link("result", "end"); b.Link("cancelled", "end");
        return b.Build([new() { id = "score", name = "指令返回分数", initialValue = NovelValue.Integer(0) }]);
    }
}
