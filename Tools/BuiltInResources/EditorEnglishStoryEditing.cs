using System.Collections.Generic;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal static class EditorEnglishStoryEditing
{
    internal static IReadOnlyList<EditorEnglishEntry> entries { get; } = new EditorEnglishEntry[]
    {
        new("literal", "新建指令", "New instruction"),
        new("literal", "添加指令 / Add instruction", "Add instruction"),
        new("literal", "添加剧情指令", "Add story instruction"),
        new("literal", "选择节点或剧本行以编辑参数，也可从上方菜单添加指令。中键平移节点图，滚轮缩放。", "Select a node or script row to edit its parameters, or add an instruction from the menu above. Pan the graph with the middle mouse button and zoom with the wheel."),
        new("literal", "复制所选节点", "Copy selected nodes"),
        new("literal", "插入循环的次数", "Iterations for inserted loop"),
        new("literal", "在 next 连接插入次数循环", "Insert counted loop on the next connection"),
        new("literal", "插入可编辑次数循环", "Insert editable counted loop"),
        new("literal", "作者备注、标签与书签##", "Author notes, tags and bookmarks##"),
        new("literal", "作者备注", "Author notes"),
        new("literal", "标签（每段一个）", "Tags (one per paragraph)"),
        new("literal", "书签", "Bookmark"),
        new("literal", "编辑作者备注与书签", "Edit author notes and bookmark"),
        new("literal", "。原始数据保留，恢复扩展后重新打开。", ". Original data is retained. Restore the extension and reopen this document."),
        new("literal", "目标 ", "Target "),
        new("literal", "未连接", "Disconnected"),
        new("literal", "修改执行目标", "Change execution target"),
        new("literal", "删除汇合/分支节点…", "Delete join / branch node…"),
        new("literal", "删除线性节点 / Delete linear", "Delete linear node"),
        new("literal", "删除剧情节点", "Delete story node"),
        new("literal", "将断开：", "Connections to disconnect: "),
        new("literal", "确认断开以上连接并删除", "Disconnect these connections and delete"),
        new("literal", "删除复杂节点并显式断开连接", "Delete complex node and explicitly disconnect its edges")
    };
}
