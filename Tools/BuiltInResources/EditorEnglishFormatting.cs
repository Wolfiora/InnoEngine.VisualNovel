using System.Collections.Generic;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal static class EditorEnglishFormatting
{
    internal static IReadOnlyList<EditorEnglishEntry> entries { get; } = new EditorEnglishEntry[]
    {
        new("literal", "简体中文###inno-vn-editor-zh", "简体中文###inno-vn-editor-zh"),
        new("literal", "在当前行后换段##", "Insert paragraph after current line##"),
        new("literal", "删除当前空行##", "Remove current empty line##"),
        new("format", "{0}：请输入 {1:G} 至 {2:G} 的有限数值。", "{0}: enter a finite number from {1:G} to {2:G}."),
        new("literal", "：请输入有效的 64 位整数。", ": enter a valid 64-bit integer."),
        new("literal", "编辑剧情参数", "Edit story parameter"),
        new("literal", "搜索名称或标识", "Search name or ID"),
        new("literal", "清除筛选##choice/", "Clear filter##choice/"),
        new("literal", "没有匹配的选项。当前值保持不变。", "No matching options. The current value is retained."),
        new("literal", "上一页##choice/", "Previous page##choice/"),
        new("literal", "下一页##choice/", "Next page##choice/"),
        new("literal", "文字格式与节奏##", "Text formatting and pacing##"),
        new("literal", "正文预览 · ", "Text preview · "),
        new("literal", "起始字素（从 0 开始）", "Starting grapheme (zero-based)"),
        new("literal", "选择字素数", "Selected grapheme count"),
        new("literal", "粗体", "Bold"),
        new("literal", "斜体", "Italic"),
        new("literal", "字号（0 继承页面）", "Font size (0 inherits the screen)"),
        new("literal", "覆盖文字颜色", "Override text color"),
        new("literal", "文字颜色", "Text color"),
        new("literal", "页面字体资源键（可留空）", "Screen font resource key (optional)"),
        new("literal", "描边半径", "Outline radius"),
        new("literal", "描边颜色", "Outline color"),
        new("literal", "阴影偏移", "Shadow offset"),
        new("literal", "阴影颜色", "Shadow color"),
        new("literal", "应用格式到所选范围##", "Apply formatting to selected range##"),
        new("literal", "注音（留空可移除）", "Ruby annotation (empty to remove)"),
        new("literal", "应用注音到所选范围##", "Apply ruby annotation to selected range##"),
        new("literal", "句末停顿秒数", "End-of-line pause (seconds)"),
        new("literal", "追加停顿##", "Append pause##"),
        new("literal", "所选文字速度倍率", "Selected text speed multiplier"),
        new("literal", "应用局部速度##", "Apply local speed##"),
        new("literal", "页面图片资源键", "Screen image resource key"),
        new("literal", "追加行内图片##", "Append inline image##"),
        new("literal", "普通背景", "Normal background"),
        new("literal", "悬停背景", "Hover background"),
        new("literal", "按下背景", "Pressed background"),
        new("literal", "禁用背景", "Disabled background"),
        new("literal", "禁用文字", "Disabled text"),
        new("literal", "图片着色", "Image tint"),
        new("literal", "焦点边框", "Focus border"),
        new("literal", "字号", "Font size"),
        new("literal", "行距倍率", "Line spacing multiplier"),
        new("literal", "文字内边距", "Text padding"),
        new("literal", "文字对齐", "Text alignment")
    };
}
