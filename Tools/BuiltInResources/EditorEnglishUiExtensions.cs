using System.Collections.Generic;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal static class EditorEnglishUiExtensions
{
    internal static IReadOnlyList<EditorEnglishEntry> entries { get; } = new EditorEnglishEntry[]
    {
        new("literal", "列表使用动作默认值，或选择类型化列表字段作为参数。", "Lists use the action's default value, or a typed list field selected as the argument."),
        new("literal", "参数无效：", "Invalid argument: "),
        new("literal", "恢复动作默认参数##", "Restore action's default argument##"),
        new("literal", "VN 自定义 UI 动作 · Custom UI Actions", "VN Custom UI Actions"),
        new("literal", "清除提示", "Clear message"),
        new("literal", "动作目录", "Action catalog"),
        new("literal", "动作目录保存类型化表单。实现由 C# 注册；在游戏设置关联目录，再在界面设计器选择动作。仅保存通过现有绑定写入的状态。", "The action catalog stores typed forms. Register implementations in C#, link the catalog in game settings, then choose actions in Screen Designer. Only state written through existing bindings is saved."),
        new("literal", "添加动作契约", "Add action contract"),
        new("literal", "添加 UI 动作", "Add UI action"),
        new("literal", "自定义动作", "Custom action"),
        new("literal", "稳定 ID（custom. 开头）", "Stable ID (starts with custom.)"),
        new("literal", "动作说明", "Action description"),
        new("literal", "参数名称", "Parameter name"),
        new("literal", "允许受控预览", "Allow controlled preview"),
        new("literal", "允许跨越回滚", "Allow rollback across this action"),
        new("literal", "有外部副作用（建立屏障，预览禁用）", "External side effects (creates a barrier; disabled in preview)"),
        new("literal", "编辑 UI 动作契约", "Edit UI action contract"),
        new("literal", "删除契约##", "Remove contract##"),
        new("literal", "删除 UI 动作契约", "Remove UI action contract"),
        new("literal", "添加字段##", "Add field##"),
        new("literal", "字段", "Field"),
        new("literal", "字段 ID（单段）", "Field ID (single segment)"),
        new("literal", "允许控件写入", "Allow control writes"),
        new("literal", "VN 自定义 UI 数据 · Custom UI Data", "VN Custom UI Data"),
        new("literal", "UI 数据目录", "UI data catalog"),
        new("literal", "声明字段/列表的类型和读写权限。关联到游戏后，界面设计器会生成绑定菜单。C# 实现通过现有 UiDataSource 接入；持久状态保存在原生变量或页面字段。", "Declare field/list types and read/write permissions. Linking the catalog to a game adds binding menus in Screen Designer. C# implementations use the existing UiDataSource contract. Persistent state belongs in native variables or screen fields."),
        new("literal", "添加数据源", "Add data source"),
        new("literal", "添加 UI 数据源", "Add UI data source"),
        new("literal", "数据源", "Data source"),
        new("literal", "源 ID（单段）", "Source ID (single segment)"),
        new("literal", "用途与状态说明", "Purpose and state description"),
        new("literal", "允许预览查询", "Allow preview queries"),
        new("literal", "写入允许跨越回滚", "Allow rollback across writes"),
        new("literal", "依赖外部副作用（预览禁用）", "Depends on external side effects (disabled in preview)")
    };
}
