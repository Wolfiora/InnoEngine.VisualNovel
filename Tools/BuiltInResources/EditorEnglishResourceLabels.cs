using System.Collections.Generic;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal static class EditorEnglishResourceLabels
{
    internal static IReadOnlyList<EditorEnglishEntry> entries { get; } = new EditorEnglishEntry[]
    {
        new("format", "批量整理需要 1～10000 个不同且仍存在的资源记录。", "A batch requires 1–10000 distinct resource records that still exist."),
        new("format", "资源整理记录已经移除，请刷新列表。", "The resource annotation was removed. Refresh the list."),
        new("format", "分类和说明需要有效文字；清空时请使用空字符串。", "Categories and descriptions require valid text. Use an empty string to clear them."),
        new("format", "每批标签输入最多 128 项，标签不能为 null。", "Tag input is limited to 128 entries per batch; tags cannot be null."),
        new("format", "每条资源最多 32 个标签；每个标签最多 64 个字符且不能包含控制字符。", "Each resource supports up to 32 tags. Each tag is limited to 64 characters without control characters."),
        new("format", "资源整理需要 1～256 字符的标题，每份目录最多 10000 条记录。", "Resource annotations require a 1–256 character title and at most 10000 records per document."),
        new("format", "整理记录需要唯一的原生资源身份；不能记录整理目录自身。", "Annotations require unique native resource identities and cannot reference annotation catalogs."),
        new("format", "分类最多 128 字符、说明最多 4096 字符；源标签需要去重并去除首尾空白。", "Categories are limited to 128 characters and descriptions to 4096. Source tags must be unique and trimmed."),
        new("literal", "批量整理所选资源", "Organize selected resources"),
        new("literal", "先移除标签，再添加标签；未启用的分类和说明保留各资源原值。一次应用是一项历史操作。", "Tags are removed before additions. Categories and descriptions retain each resource's value unless explicitly replaced. Apply creates one History operation."),
        new("literal", "替换分类（空值表示未分类）", "Replace category (empty means ungrouped)"),
        new("literal", "新分类", "New category"),
        new("literal", "添加标签（每行一个）", "Add tags (one per line)"),
        new("literal", "移除标签（每行一个）", "Remove tags (one per line)"),
        new("literal", "替换全部所选说明", "Replace descriptions for all selected resources"),
        new("literal", "新说明", "New description"),
        new("literal", "应用批量整理", "Apply batch annotations"),
        new("literal", "批量整理资源", "Organize resources"),
        new("literal", "VN 资源整理 · Resource Labels", "VN Resource Labels"),
        new("literal", "资源整理", "Resource annotations"),
        new("literal", "选择 File Browser 中的原生资源后收录。分类、标签和说明属于创作记录，资源加载与身份由引擎维护。标签每行一个；分发游戏不包含本目录。", "Select a native resource in File Browser, then add it. Categories, tags and descriptions are authoring notes; the engine owns resource loading and identity. Enter one tag per line. This catalog is excluded from game content."),
        new("literal", "目录标题", "Catalog title"),
        new("literal", "编辑资源整理标题", "Edit resource annotation title"),
        new("literal", "收录所选资源", "Add selected resource"),
        new("literal", "收录资源整理记录", "Add resource annotation"),
        new("literal", "查找路径 / 分类 / 标签 / 说明", "Search path / category / tags / description"),
        new("literal", "全部分类", "All categories"),
        new("literal", "未分类", "Ungrouped"),
        new("literal", "按分类筛选", "Filter by category"),
        new("literal", "刷新整理列表", "Refresh annotations"),
        new("literal", "选择全部筛选结果", "Select all filtered resources"),
        new("literal", "清空资源选择", "Clear resource selection"),
        new("format", "{0} 条记录 · 已选择 {1}", "{0} records · {1} selected"),
        new("literal", "上一页##labels", "Previous##labels"),
        new("literal", "下一页##labels", "Next##labels"),
        new("literal", "没有匹配的整理记录。清除筛选，或收录所选资源。", "No matching annotations. Clear the filter or add the selected resource."),
        new("literal", "选择##label-resource", "Select##label-resource"),
        new("literal", "选中资源供设计器使用", "Select resource for designers"),
        new("literal", "标签（每行一个）", "Tags (one per line)"),
        new("literal", "资源说明", "Resource description"),
        new("literal", "编辑资源整理记录", "Edit resource annotation"),
        new("literal", "移除整理记录（保留资源）", "Remove annotation (keep resource)"),
        new("literal", "移除资源整理记录", "Remove resource annotation"),
    };
}
