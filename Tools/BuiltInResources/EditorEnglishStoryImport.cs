using System.Collections.Generic;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal static class EditorEnglishStoryImport
{
    internal static IReadOnlyList<EditorEnglishEntry> entries { get; } = new EditorEnglishEntry[]
    {
        new("literal", "导入对白", "Import dialogue"),
        new("literal", "导入为新片段及公开入口，保留原有分支和连线。先审核匹配，全部记录有效后作为一次原生 History 操作导入。图片、演出与选项继续在图/剧本中编辑。", "Import as a new fragment and public entry, preserving existing branches and connections. Review matches first; all records must be valid to import as one native History operation. Edit images, performances and choices in graph/script."),
        new("literal", "源格式", "Source format"),
        new("literal", "CSV 表格", "CSV table"),
        new("literal", "TSV 表格", "TSV table"),
        new("literal", "纯文本段落", "Plain-text paragraphs"),
        new("literal", "剧本文件路径", "Script file path"),
        new("literal", "读取剧本文件", "Read script file"),
        new("literal", "匹配所选游戏角色与语音：", "Match characters and voices from selected game: "),
        new("literal", "未指定游戏：可导入旁白和字面姓名；角色 ID 与语音需要先选择对应游戏。", "No game specified: narration and literal names can be imported; character IDs and voices require selecting the game first."),
        new("literal", "资源匹配上下文：", "Resource matching context: "),
        new("literal", "不分配", "Unassigned"),
        new("literal", "预览角色、语音与文本 ID 匹配", "Preview character, voice and text ID matches"),
        new("format", "{0} 条 · 错误 {1} · {2}/{3}", "{0} records · {1} errors · {2}/{3}"),
        new("literal", "上一页##import", "Previous##import"),
        new("literal", "下一页##import", "Next##import"),
        new("literal", "记录 ", "Record "),
        new("literal", "\n文本 ID：", "\nText ID: "),
        new("literal", " · 语音：", " · Voice: "),
        new("literal", "\n错误：", "\nError: "),
        new("literal", "新片段名称", "New fragment name"),
        new("literal", "将全部已审核对白导入新片段", "Import all reviewed dialogue as new fragment"),
        new("literal", "导入对白片段", "Import dialogue fragment"),
        new("literal", "阅读稿导出", "Proofread export"),
        new("literal", "导出当前章节阅读稿", "Export current chapter for proofreading"),
    };
}
