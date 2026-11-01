using System.Collections.Generic;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal static class EditorEnglishStageBatches
{
    internal static IReadOnlyList<EditorEnglishEntry> entries { get; } = new EditorEnglishEntry[]
    {
        new("literal", "批量修改舞台图层", "Batch edit stage layers"),
        new("literal", "筛选批量图层", "Filter batch layers"),
        new("literal", "勾选所有筛选结果", "Select all filtered layers"),
        new("literal", "清空批量勾选", "Clear batch selection"),
        new("format", "已勾选 {0} 层 · 筛选结果 {1} 层", "{0} selected layers · {1} matching layers"),
        new("literal", "上一页##stage-batch", "Previous##stage-batch"),
        new("literal", "下一页##stage-batch", "Next##stage-batch"),
        new("literal", "批量横向偏移", "Batch horizontal offset"),
        new("literal", "批量纵向偏移", "Batch vertical offset"),
        new("literal", "批量缩放倍数（保留镜像）", "Batch scale factor (preserve mirroring)"),
        new("literal", "批量旋转偏移（度）", "Batch rotation offset (degrees)"),
        new("literal", "覆盖所选不透明度", "Override selected opacity"),
        new("literal", "统一不透明度", "Shared opacity"),
        new("literal", "覆盖所选显隐", "Override selected visibility"),
        new("literal", "统一可见状态", "Shared visibility"),
        new("literal", "覆盖所选颜色", "Override selected color"),
        new("literal", "统一图层颜色", "Shared layer color"),
        new("literal", "覆盖所选合成分组", "Override selected paint band"),
        new("literal", "统一合成分组", "Shared paint band"),
        new("literal", "位置与旋转相对偏移；缩放保留各层镜像和枢轴。仅开启的外观覆盖生效，勾选包含其他筛选页的图层。所有更改可一起撤销。", "Position and rotation are relative offsets; scaling preserves each layer's mirroring and pivot. Only enabled appearance overrides apply. Selection includes layers on other filter pages. Undo restores the whole change."),
        new("literal", "应用到全部勾选图层", "Apply to all selected layers"),
        new("literal", "批量编辑舞台图层", "Batch edit stage layers"),
    };
}
