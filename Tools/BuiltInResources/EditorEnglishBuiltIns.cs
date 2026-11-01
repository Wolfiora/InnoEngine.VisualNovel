using System.Collections.Generic;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal static class EditorEnglishBuiltIns
{
    internal static IReadOnlyList<EditorEnglishEntry> entries { get; } = new EditorEnglishEntry[]
    {
        new("literal", "VN 内置资源 · Built-in Resources", "VN Built-in Resources"),
        new("literal", "搜索名称、用途或路径", "Search name, usage or path"),
        new("literal", "全部内置资源", "All built-in resources"),
        new("format", "{0} 项 · 第 {1} 页", "{0} items · Page {1}"),
        new("literal", "批量选择 · ", "Batch selection · "),
        new("literal", " 项（跨页保留）", " items (retained across pages)"),
        new("literal", "选择筛选内可编辑资源", "Select editable resources in this filter"),
        new("literal", "清空批量选择", "Clear batch selection"),
        new("literal", "批量创建可编辑副本", "Create editable copies in bulk"),
        new("literal", "刷新缩略图", "Refresh thumbnails"),
        new("literal", "预览", "Preview"),
        new("literal", "资源", "Resource"),
        new("literal", "批量##", "Batch##"),
        new("literal", "缩略图不可用：", "Thumbnail unavailable: "),
        new("literal", "项目副本批次 · ", "Project copy batch · "),
        new("literal", "停止批量复制", "Stop batch copy"),
        new("literal", "每次编辑器更新复制一个资源。一个主题连同它的页面作为一项；原生导入不可中途抢占。", "Copies one resource per editor update. A theme and its screens count as one item. A native import cannot be interrupted midway."),
        new("literal", "批次已停止：", "Batch stopped: "),
        new("literal", "已停止。以下已创建资源保留在项目中。", "Stopped. The created assets below remain in the project."),
        new("literal", "批量复制完成。以下均为独立项目资产。", "Batch copy complete. These are independent project assets."),
        new("literal", "上一页副本", "Previous output page"),
        new("literal", "下一页副本", "Next output page"),
        new("literal", "原创资源：Apache License 2.0；保留许可与版权说明。安装资源只读，参数修改先创建项目副本。", "Original resources: Apache License 2.0. Retain the license and copyright notices. Installed resources are read-only; create a project copy before editing parameters."),
        new("literal", "选择此资源供其他面板使用", "Select this resource for other panels"),
        new("literal", "创建可编辑项目副本", "Create editable project copy"),
        new("literal", "已创建独立副本：", "Created independent copy: "),
        new("literal", "。在对应设计器继续编辑；游戏设置可关联这个资源。", ". Continue in the corresponding designer, or link this resource in game settings."),
        new("literal", "暂停演出", "Pause presentation"),
        new("literal", "播放演出", "Play presentation"),
        new("literal", "从头预览", "Preview from start"),
        new("literal", "检查减少动态效果", "Preview reduced motion"),
        new("literal", "逻辑时间（秒）", "Logical time (seconds)"),
        new("literal", "预览页面", "Preview screen"),
        new("literal", "组件", "Widget"),
        new("literal", "背景", "Background"),
        new("literal", "角色", "Character"),
        new("literal", "动作", "Motion"),
        new("literal", "粒子", "Particles"),
        new("literal", "图标", "Icons")
    };
}
