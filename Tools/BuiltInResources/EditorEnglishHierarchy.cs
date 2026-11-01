using System.Collections.Generic;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal static class EditorEnglishHierarchy
{
    internal static IReadOnlyList<EditorEnglishEntry> entries { get; } = new EditorEnglishEntry[]
    {
        new("literal", "根字段##", "Root fields##"),
        new("literal", "图片字段 ID（逗号分隔）", "Image field IDs (comma separated)"),
        new("literal", "添加列表##", "Add list##"),
        new("literal", "列表", "List"),
        new("literal", "文字", "Text"),
        new("literal", "列表 · ", "List · "),
        new("literal", "列表 ID", "List ID"),
        new("literal", "删除列表##", "Delete list##"),
        new("literal", "编辑 UI 数据契约", "Edit UI data contract"),
        new("literal", "删除数据源##", "Delete data source##"),
        new("literal", "删除 UI 数据源", "Delete UI data source"),
        new("literal", "层级与顺序", "Hierarchy and order"),
        new("literal", "上移一层##ui-order", "Move up##ui-order"),
        new("literal", "下移一层##ui-order", "Move down##ui-order"),
        new("literal", "拖放已选择新父元素。确认下方布局、裁切和 item.* 上下文后应用；尚未修改页面。", "The drop selected a new parent. Review layout, clipping and item.* context below before applying; the screen has not changed."),
        new("literal", "更换父元素 / 指定位置", "Change parent / position"),
        new("literal", "目标父元素", "Target parent"),
        new("literal", "目标位置（从 1 开始）", "Target position (starting at 1)"),
        new("literal", "]\n保留控件 ID、本地位置、动作与绑定；新父元素可能改变堆叠布局、裁切和 item.* 行上下文。不会自动猜测或改写这些绑定。", "]\nControl IDs, local positions, actions and bindings are preserved. The new parent may change stacking, clipping and item.* row context. These bindings will not be guessed or rewritten."),
        new("literal", "应用层级变更", "Apply hierarchy change"),
        new("literal", " · 松开后审阅新父元素的布局与裁切", " · Release to review the new parent's layout and clipping"),
        new("literal", " · 松开调整同级顺序", " · Release to reorder siblings"),
        new("literal", " · item.* 数据上下文将改变", " · item.* data context will change"),
    };
}
