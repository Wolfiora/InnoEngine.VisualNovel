using System.Collections.Generic;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Flow;
using Inno.VisualNovel.Model.Story.Nodes.Reading;

namespace Inno.VisualNovel.Authoring.Templates;

/// <summary>Creates an ordinary editable chapter menu linking the included no-code lessons.</summary>
public static class ShowcaseStoryTemplate
{
    /// <summary>Builds an explicit multi-chapter menu; each lesson remains independently editable.</summary>
    /// <param name="codec">The native graph settings codec.</param>
    /// <param name="lessons">Stable menu keys mapped to authored chapters and display titles.</param>
    /// <returns>A menu that jumps to a selected public start entry or ends normally.</returns>
    public static StoryDraftState Create(
        StoryNodeDataCodec codec,
        IReadOnlyList<(string id, string chapter, string title)> lessons
    ) {
        var b = new StoryTemplateBuilder(codec, "功能展示目录"); b.Add("entry", StoryNodeIds.entry);
        b.Add("hello", StoryNodeIds.dialogue, TutorialStoryTemplates.Line("选择一个教学章节。每个章节均由普通可编辑节点制作；结束后可返回标题重新选择，也可在剧本中继续添加内容。"));
        var options = new List<ChoiceOption>();
        foreach (var lesson in lessons)
        {
            options.Add(new() { id = lesson.id, text = lesson.title });
            b.Add("jump-" + lesson.id, StoryNodeIds.call, new CallNode { chapterId = lesson.chapter, entryId = "start", jump = true });
        }
        options.Add(new() { id = "finish", text = "结束展示" }); b.Add("menu", StoryNodeIds.choice, new ChoiceNode { prompt = "今天想体验哪个部分？", options = options.ToArray() });
        b.Add("end", StoryNodeIds.end); b.Link("entry", "hello"); b.Link("hello", "menu"); b.Link("menu", "end", "finish");
        foreach (var lesson in lessons) b.Link("menu", "jump-" + lesson.id, lesson.id); return b.Build();
    }
}
