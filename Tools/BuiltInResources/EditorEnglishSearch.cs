using System.Collections.Generic;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal static class EditorEnglishSearch
{
    internal static IReadOnlyList<EditorEnglishEntry> entries { get; } = new EditorEnglishEntry[]
    {
        new("literal", "搜索所选游戏全部章节", "Search all chapters in selected game"),
        new("literal", "刷新当前章节搜索", "Refresh current chapter search"),
        new("literal", "搜索结果是当前章节快照。修改内容、撤销或重做后，点击刷新更新；定位使用共享节点身份。", "Results are a chapter snapshot. Refresh after edits, undo or redo; navigation uses shared node identity."),
        new("literal", " 个结果 · ", " results · "),
        new("literal", "该节点已删除；刷新搜索以更新结果。", "This node was deleted; refresh search to update results."),
        new("literal", "搜索正文、姓名、ID、资源、标签与备注", "Search text, names, IDs, resources, tags and notes"),
        new("literal", "只看书签", "Bookmarks only"),
        new("literal", "使用所选游戏开始搜索", "Search selected game"),
        new("literal", "请在 File Browser 选择 .ivnproject 游戏定义。", "Select an .ivnproject game definition in File Browser."),
        new("literal", "重新搜索此游戏", "Search this game again"),
        new("literal", "选择游戏定义，再开始搜索。搜索不会批量打开或保存章节；已打开文档使用当前未保存草稿。", "Select a game definition to search. Search does not open or save chapters in bulk; open documents use their current unsaved drafts."),
        new("literal", "取消搜索", "Cancel search"),
        new("literal", " 章 · ", " chapters · "),
        new("literal", " 个节点", " nodes"),
        new("literal", " · 已取消（保留部分结果）", " · Cancelled (partial results retained)"),
        new("literal", " · 搜索结束", " · Search finished"),
        new("literal", " · 搜索中", " · Searching"),
        new("literal", "查询已修改；点击重新搜索更新全游戏结果。", "The query changed; search again to update game-wide results."),
        new("literal", "结果达到 100000 项上限；请使用更精确的查询。", "The 100000-result limit was reached; use a more specific query."),
        new("literal", "上一页##game-search", "Previous##game-search"),
        new("literal", "下一页##game-search", "Next##game-search"),
        new("literal", "此节点已删除；请重新搜索。", "This node was deleted; search again."),
        new("literal", "已定位原生节点。", "Located native node."),
        new("literal", "内容已变化，已按稳定节点身份定位；请重新搜索更新摘要。", "Content changed; located by stable node identity. Search again to update summaries."),
        new("literal", "所选游戏定义缺失。", "The selected game definition is missing."),
    };
}
