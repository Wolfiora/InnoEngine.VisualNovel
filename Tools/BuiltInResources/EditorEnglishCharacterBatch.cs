using System.Collections.Generic;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal static class EditorEnglishCharacterBatch
{
    internal static IReadOnlyList<EditorEnglishEntry> entries { get; } = new EditorEnglishEntry[]
    {
        new("literal", "将所选图像加入动画图像库", "Add selected image to the animation library"),
        new("literal", "表情显隐（-1 保持）", "Expression visibility (-1 keeps the pose value)"),
        new("literal", "表情水平偏移", "Expression horizontal offset"),
        new("literal", "表情垂直偏移", "Expression vertical offset"),
        new("literal", "将所选图像用作此表情差分", "Use selected image for this expression variant"),
        new("literal", "清除此部位表情覆盖，恢复姿势原值", "Clear this part's expression override and restore its pose values"),
        new("literal", "清除角色部位表情覆盖", "Clear character part expression override"),
        new("literal", "从全部姿势与表情移除此部位", "Remove this part from every pose and expression"),
        new("literal", "删除共享角色部位与差分", "Remove shared character part and variants"),
        new("literal", "编辑角色", "Edit character"),
        new("literal", "暂停角色预览", "Pause character preview"),
        new("literal", "播放角色预览", "Play character preview"),
        new("literal", "重置预览时间", "Reset preview time"),
        new("literal", "预览逻辑秒数", "Preview time (logical seconds)"),
        new("literal", "预览说话标记", "Preview speaking marker"),
        new("literal", "批量差分图像匹配与预览", "Match and preview variant images in bulk"),
        new("literal", "文件名：pose__姿势名称__部位名称.png 或 expression__表情名称__部位名称.png。也可使用稳定 ID；支持已导入的 PNG/JPEG。只读取所选文件夹的直接文件。", "Use pose__pose-name__part-name.png or expression__expression-name__part-name.png. Stable IDs are also accepted. Imported PNG/JPEG images are supported. Only direct files in the selected folder are read."),
        new("literal", "图片文件夹（相对 Assets）", "Image folder (relative to Assets)"),
        new("literal", "允许创建命名姿势/表情", "Allow named poses and expressions to be created"),
        new("literal", "替换姿势图像时同步像素尺寸", "Match pixel dimensions when replacing pose images"),
        new("literal", "准备并检查图像匹配", "Prepare and check image matches"),
        new("literal", "准备原生图像 · ", "Preparing imported images · "),
        new("literal", "取消图像准备", "Cancel image preparation"),
        new("literal", "已取消，角色文档未更改。", "Cancelled. The character document is unchanged."),
        new("literal", "审核来源 · ", "Reviewed source · "),
        new("literal", " · 可用 ", " · Ready: "),
        new("literal", "重复或无法匹配的条目不会应用。新姿势复制默认姿势，表情只覆盖匹配图像；变换、遮罩与其他覆盖保留。图像或文档变化后需重新准备。", "Duplicate and unmatched items are excluded. New poses copy the default pose. Expressions override only matched images, preserving transforms, masks and other overrides. Prepare again if an image or the document changes."),
        new("literal", "在下方舞台显示冻结的批次候选（未提交）", "Preview the reviewed candidate on the stage below (without applying)"),
        new("literal", "候选预览姿势", "Candidate preview pose"),
        new("literal", "候选预览表情", "Candidate preview expression"),
        new("literal", "应用全部 ", "Apply all "),
        new("literal", " 项可用匹配（一个可撤销操作）", " ready matches (one undoable operation)"),
        new("literal", "应用角色差分图像批次", "Apply character variant image batch"),
        new("literal", "已应用审核中的可用匹配；其他条目保留原值。可用引擎 Undo 撤销整个批次。", "Applied the ready matches from this review. Other entries retain their original values. Undo restores the whole batch."),
        new("literal", "上一页##character-batch", "Previous page##character-batch"),
        new("literal", "下一页##character-batch", "Next page##character-batch"),
        new("literal", "可用 · ", "Ready · "),
        new("literal", "待处理 · ", "Needs attention · "),
        new("literal", "文件夹没有可准备的 PNG/JPEG 图像。", "The folder contains no PNG/JPEG images to prepare.")
    };
}
