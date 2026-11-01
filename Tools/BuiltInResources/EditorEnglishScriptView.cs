using System.Collections.Generic;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal static class EditorEnglishScriptView
{
    internal static IReadOnlyList<EditorEnglishEntry> entries { get; } = new EditorEnglishEntry[]
    {
        new("literal", "剧本视图 · Cards 卡片 / Compact 紧凑 / Proofread 阅读稿", "Script view · Cards / Compact / Proofread"),
        new("literal", "定位所选节点", "Locate selected node"),
        new("literal", "创建 / 编辑指令", "Create / edit instruction"),
        new("literal", " 个指令 · 按实际连线分段 · 只绘制可见区域；分支、汇合、循环均保留明确目标。", " instructions · Segmented by actual connections · Only visible content is drawn; branches, merges and loops retain explicit targets."),
        new("literal", "未归属片段", "No fragment"),
        new("literal", " · 执行段", " · Execution segment"),
        new("literal", "收起完整参数", "Collapse full parameters"),
        new("literal", "编辑此段", "Edit this segment"),
        new("literal", "完整参数与连接", "Full parameters and connections"),
        new("literal", "汇合 · ", "Merge · "),
        new("literal", " 个来源", " incoming paths"),
        new("literal", "角色 · ", "Character · "),
        new("literal", "旁白", "Narration"),
        new("literal", "\n格式诊断：", "\nFormatting diagnostics: "),
        new("literal", "关联所选角色 · ", "Link selected character · "),
        new("literal", "姓名覆盖 / 旁白", "Name override / narration"),
        new("literal", "语音提示", "Voice cue"),
        new("literal", "正文", "Body"),
        new("literal", "继续写对白", "Continue writing dialogue"),
        new("literal", "编辑剧本对白", "Edit script dialogue"),
        new("literal", "继续写下一句（同角色 / 同布局，Ctrl+Enter）", "Write the next line (same character / layout, Ctrl+Enter)"),
        new("literal", "文档已经修改，请重新预览移动。", "The document changed. Preview the move again."),
        new("literal", "上移一句", "Move line up"),
        new("literal", "下移一句", "Move line down"),
        new("literal", "移动到指定连线…", "Move to connection…"),
        new("literal", "插入到连接", "Insert at connection"),
        new("literal", "选择目标连接", "Select target connection"),
        new("literal", "预览连接变化", "Preview connection changes"),
        new("literal", "此操作跨越控制流或片段边界。确认以下三条连接变化后作为一次历史操作应用。", "This operation crosses a control-flow or fragment boundary. Review the following three connection changes before applying them as one History operation."),
        new("literal", "同一线性执行段内换序；节点身份、正文、资源和图中位置保留。确认后应用。", "Reorder within one linear segment, preserving node identity, text, resources and graph position. Review before applying."),
        new("literal", "接受连接变化并移动", "Accept connections and move"),
        new("literal", "应用换序", "Apply reorder"),
        new("literal", "移动剧情指令", "Move story instruction"),
        new("literal", "取消移动", "Cancel move"),
        new("literal", "松开后按实际连线移动；跨分支或片段时先审阅连接变化。", "Release to move along the actual connections. Review changes before crossing branches or fragments."),
        new("literal", "将此指令插入这条执行连线；复杂控制流先审阅变化。", "Insert this instruction on this execution connection. Review changes for complex control flow."),
    };
}
