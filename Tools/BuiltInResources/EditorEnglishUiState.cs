using System.Collections.Generic;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal static class EditorEnglishUiState
{
    internal static IReadOnlyList<EditorEnglishEntry> entries { get; } = new EditorEnglishEntry[]
    {
        new("literal", "页面设计预览", "Screen design preview"),
        new("literal", "角色姓名", "Character name"),
        new("literal", "这是使用正式游戏 UI 与文字渲染器的预览。\n切换布局与字号，检查长台词和控件位置。", "This preview uses the production game UI and text renderer.\nSwitch layouts and font sizes to check long lines and control placement."),
        new("literal", "第一个示例选项", "First example choice"),
        new("literal", "用于检查文本扩张和布局的较长选项", "A longer choice for checking text expansion and layout"),
        new("literal", "页面设置", "Screen settings"),
        new("literal", "页面名称", "Screen name"),
        new("literal", "消费未命中控件的输入", "Consume input outside controls"),
        new("literal", "初始键盘焦点", "Initial keyboard focus"),
        new("literal", "自动选择可交互控件", "Automatically choose an interactive control"),
        new("literal", "使用所选字体作为页面默认字体", "Use selected font as screen default"),
        new("literal", "页面局部状态", "Screen-local state"),
        new("literal", "“页面”字段只属于当前页面角色。临时字段离开页面即重置；游戏字段随存档与回滚恢复，和剧情变量/全局解锁分开。", "Screen fields belong only to the current screen role. Temporary fields reset when leaving the screen. Game fields restore with saves and rollback and remain separate from story variables and global unlocks."),
        new("literal", "添加页面字段", "Add screen field"),
        new("literal", "页面字段", "Screen field"),
        new("literal", "删除时同时清除引用（显式选择）", "Also clear references when deleting (explicit choice)"),
        new("literal", "分组", "Group"),
        new("literal", "用途说明", "Purpose"),
        new("literal", "生命周期", "Lifetime"),
        new("literal", "本次页面 · 离开后重置", "Current screen visit · Reset on leaving"),
        new("literal", "游戏状态 · 保存与回滚", "Game state · Save and rollback"),
        new("literal", "枚举成员（逗号分隔）", "Enum members (comma-separated)"),
        new("literal", "引用控件 · ", "Referencing control · "),
        new("literal", "删除字段##", "Remove field##"),
        new("literal", "页面富文本资源", "Screen rich-text resources"),
        new("literal", "为字体与行内图像命名。正文的字体/图片资源键引用这里的原生资源；插件打包沿这些引用收集依赖。", "Assign names to fonts and inline images. Text uses these resource keys to reference imported assets; packaging follows their native dependencies."),
        new("literal", "字体资源键", "Font resource key"),
        new("literal", "资源键需要唯一，且仅包含字母、数字、_、-、.、/。", "Resource keys must be unique and contain only letters, digits, _, -, . or /."),
        new("literal", "Missing · 尚未绑定字体", "Missing · No font bound"),
        new("literal", "已绑定字体 · ", "Bound font · "),
        new("literal", "绑定所选字体##", "Bind selected font##"),
        new("literal", "移除此字体绑定##", "Remove this font binding##"),
        new("literal", "添加字体绑定", "Add font binding"),
        new("literal", "行内图片资源键", "Inline image resource key"),
        new("literal", "Missing · 尚未绑定图片", "Missing · No image bound"),
        new("literal", "已绑定图片 · ", "Bound image · "),
        new("literal", "绑定所选行内图像##", "Bind selected inline image##"),
        new("literal", "移除此图片绑定##", "Remove this image binding##"),
        new("literal", "添加行内图片绑定", "Add inline image binding")
    };
}
