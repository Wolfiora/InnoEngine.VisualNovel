using System;
using System.Collections.Generic;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Compiler;

namespace Inno.VisualNovel.Authoring.Templates;

/// <summary>Provides the five executable tutorial graph factories without retaining imported assets.</summary>
public static class TutorialSampleCatalog
{
    /// <summary>Gets stable distribution directory names, titles and required setup descriptions.</summary>
    public static IReadOnlyList<(string id, string title, string description)> entries { get; } = Array.AsReadOnly(new (string, string, string)[]
    {
        ("FirstStory", "首个故事", "六节点对白与选项，理解同一张图的两种编辑视图。"),
        ("BranchingStory", "分支与循环", "玩家姓名、类型变量、汇合、两次循环和片段调用。"),
        ("PerformanceShowcase", "演出展示", "双角色、时间线、音乐、粒子、暗角与背景交叉溶解。"),
        ("InterfaceShowcase", "界面展示", "真实设置页、命名检查点及五种阅读布局。"),
        ("ScriptExtension", "脚本扩展", "配套 DelayScoreCommand 正常 C# 类，需显式注册；类型化结果、取消与存档恢复。")
    });
    /// <summary>Creates fresh graph, chapter and text identities for one declared sample.</summary>
    /// <param name="id">One stable sample directory identity.</param>
    /// <param name="codec">The native graph settings codec.</param>
    /// <returns>A detached chapter ready to import or instantiate into ordinary Game assets.</returns>
    public static StoryDraftState Create(
        string id,
        StoryNodeDataCodec codec
    ) => id switch
    {
        "FirstStory" => FirstStoryTemplate.Create(codec), "BranchingStory" => TutorialStoryTemplates.Branching(codec),
        "PerformanceShowcase" => PerformanceStoryTemplate.Create(codec), "InterfaceShowcase" => TutorialStoryTemplates.Interface(codec),
        "ScriptExtension" => ExtensionStoryTemplate.Create(codec), _ => throw new ArgumentException("Unknown tutorial sample: " + id, nameof(id))
    };
}
