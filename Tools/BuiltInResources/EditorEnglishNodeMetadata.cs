using System.Collections.Generic;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal static class EditorEnglishNodeMetadata
{
    internal static IReadOnlyList<EditorEnglishEntry> entries { get; } = new EditorEnglishEntry[]
    {
        new("literal", "入口", "Entry"),
        new("literal", "流程", "Flow"),
        new("literal", "结束", "End"),
        new("literal", "返回", "Return"),
        new("literal", "对白", "Dialogue"),
        new("literal", "阅读", "Reading"),
        new("literal", "设置变量", "Set variable"),
        new("literal", "变量", "Variables"),
        new("literal", "等待", "Wait"),
        new("literal", "调用或跳转", "Call or jump"),
        new("literal", "断言", "Assert"),
        new("literal", "调试", "Debugging"),
        new("literal", "输出日志", "Log message"),
        new("literal", "舞台与角色", "Stage and characters"),
        new("literal", "演出", "Presentation"),
        new("literal", "画面转场", "Image transition"),
        new("literal", "音频", "Audio"),
        new("literal", "声音", "Sound"),
        new("literal", "解锁鉴赏内容", "Unlock collection content"),
        new("literal", "游戏系统", "Game system"),
        new("literal", "页面与主题", "Screens and themes"),
        new("literal", "并行演出", "Parallel presentation"),
        new("literal", "并行汇合", "Parallel join"),
        new("literal", "自定义 C# 指令", "Custom C# command"),
        new("literal", "扩展", "Extensions"),
        new("literal", "演出片段", "Performance clip"),
        new("literal", "玩家文字输入", "Player text input"),
        new("literal", "条件", "Condition"),
        new("literal", "选项", "Choices"),
        new("literal", "随机分支", "Random branch")
    };
}
