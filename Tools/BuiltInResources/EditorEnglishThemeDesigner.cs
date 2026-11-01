using System.Collections.Generic;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal static class EditorEnglishThemeDesigner
{
    internal static IReadOnlyList<EditorEnglishEntry> entries { get; } = new EditorEnglishEntry[]
    {
        new("literal", "VN 主题设计 · Theme Designer", "VN Theme Designer"),
        new("literal", "使用所选默认字体", "Use selected default font"),
        new("literal", "使用页面自身字体", "Use each screen's font"),
        new("literal", "颜色 token", "Color tokens"),
        new("literal", "稳定键", "Stable key"),
        new("literal", "移除##color-", "Remove##color-"),
        new("literal", "添加颜色 token", "Add color token"),
        new("literal", "尺寸 token", "Size tokens"),
        new("literal", "移除##metric-", "Remove##metric-"),
        new("literal", "添加尺寸 token", "Add size token"),
        new("literal", "继承样式与控件状态", "Style inheritance and control states"),
        new("literal", "编辑样式", "Edit style"),
        new("literal", "添加样式", "Add style"),
        new("literal", "样式键是页面引用身份。继承字段可单独覆盖；token 重命名会更新本主题内的绑定。发布检查验证所有主题页面。", "Style keys identify screen references. Inherited fields can be overridden individually; renaming a token updates bindings in this theme. Publishing checks validate all themed screens."),
        new("literal", "页面映射", "Screen mappings"),
        new("literal", "系统页面", "System screen"),
        new("literal", "启动与加载", "Startup and loading"),
        new("literal", "将所选页面用于此角色", "Use selected screen for this role"),
        new("literal", "恢复游戏默认页面", "Restore game default screen"),
        new("literal", "编辑主题", "Edit theme"),
        new("literal", "预览文字缩放", "Preview text scale"),
        new("literal", "主题 · 普通与键盘焦点", "Theme · Normal and keyboard focus"),
        new("literal", "主题 · 禁用状态", "Theme · Disabled state"),
        new("literal", "继承样式", "Base style"),
        new("literal", "不继承", "No inheritance"),
        new("literal", "定义 · ", "Definition · "),
        new("literal", "值来源", "Value source"),
        new("literal", "本样式的固定值", "Fixed value in this style"),
    };
}
