using System;
using InnoEngine.Mathematics;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using Inno.VisualNovel.Authoring.StageEditing;
using Inno.VisualNovel.Editor.Presentation;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Model.Story.Nodes.Stage;

namespace Inno.VisualNovel.Editor.Inspection;

internal sealed class CharacterPlacementView(
    StoryInspectorFields fields,
    EditorInteractions interactions
)
{
    private string m_position = "center", m_anchor = "bottom-center";
    private double m_width = 1920, m_height = 1080, m_margin = 32, m_x = .5, m_y = 1;
    internal bool Draw(
        string id,
        CharacterAsset character,
        string pose,
        string expression,
        StageNode command
    ) {
        if (!ImGui.CollapsingHeader(fields.Caption("快捷站位、相对锚点与安全区##" + id, fields.Localize("快捷站位、相对锚点与安全区##") + id))) return false;
        if (NovelResourceSelection.Get<NovelProjectAsset>(interactions) is { isMissing: false } game && ImGui.SmallButton(fields.Label("使用所选游戏的设计画布")))
        { fields.Finish(); m_width = game.width; m_height = game.height; }
        if (NovelResourceSelection.Get<StageAsset>(interactions) is { isMissing: false } stage && ImGui.SmallButton(fields.Label("使用所选舞台的设计画布")))
        { fields.Finish(); m_width = stage.width; m_height = stage.height; }
        fields.Number(id + "/placement-width", fields.Localize("参考宽度"), ref m_width, 1, 100000);
        fields.Number(id + "/placement-height", fields.Localize("参考高度"), ref m_height, 1, 100000);
        m_margin = Math.Clamp(m_margin, 0, Math.Max(0, (Math.Min(m_width, m_height) - 1) / 2));
        fields.Number(id + "/placement-margin", fields.Localize("安全边距（px）"), ref m_margin, 0, Math.Max(0, (Math.Min(m_width, m_height) - 1) / 2));
        if (fields.Select(id + "/placement", fields.Localize("站位"), ref m_position,
            new[] { ("left", fields.Localize("左侧")), ("center", fields.Localize("中央")), ("right", fields.Localize("右侧")), ("custom", fields.Localize("自定义相对位置")) }) && m_position != "custom")
        { m_x = m_position == "left" ? .25 : m_position == "right" ? .75 : .5; m_y = 1; }
        if (m_position == "custom")
        {
            fields.Number(id + "/placement-x", fields.Localize("相对 X"), ref m_x, -2, 3);
            fields.Number(id + "/placement-y", fields.Localize("相对 Y"), ref m_y, -2, 3);
        }
        fields.Select(id + "/placement-anchor", fields.Localize("角色范围锚点"), ref m_anchor,
            new[] { ("top-left", fields.Localize("左上")), ("top-center", fields.Localize("中上")), ("top-right", fields.Localize("右上")), ("middle-left", fields.Localize("左中")), ("middle-center", fields.Localize("中心")),
                ("middle-right", fields.Localize("右中")), ("bottom-left", fields.Localize("左下")), ("bottom-center", fields.Localize("中下")), ("bottom-right", fields.Localize("右下")) });
        EditorUiSession.current.widgets.Hint(fields.Localize("按实际姿势、表情、裁剪、镜像和旋转后的范围对齐。只写入位置，保留枢轴、缩放和透明度；相对计算在应用时完成，不猜测当前运行舞台。"));
        if (!ImGui.Button(fields.Caption("应用快捷站位##" + id, fields.Localize("应用快捷站位##") + id))) return false;
        fields.Finish();
        double anchorX = m_anchor.EndsWith("left", StringComparison.Ordinal) ? 0 : m_anchor.EndsWith("right", StringComparison.Ordinal) ? 1 : .5;
        double anchorY = m_anchor.StartsWith("top", StringComparison.Ordinal) ? 0 : m_anchor.StartsWith("bottom", StringComparison.Ordinal) ? 1 : .5;
        command.transform = CharacterPlacementEditService.Place(character, pose, expression, command.transform,
            new((float)m_margin, (float)m_margin, (float)(m_width - m_margin * 2), (float)(m_height - m_margin * 2)), m_x, m_y, anchorX, anchorY);
        return true;
    }
}
