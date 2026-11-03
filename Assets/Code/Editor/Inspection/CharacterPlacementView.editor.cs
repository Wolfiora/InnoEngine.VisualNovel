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
        if (!ImGui.CollapsingHeader("Quick placement, relative anchors and safe area##" + id)) return false;
        if (NovelResourceSelection.Get<NovelProjectAsset>(interactions) is { isMissing: false } game && ImGui.SmallButton("Use the selected game's design canvas"))
        { fields.Finish(); m_width = game.width; m_height = game.height; }
        if (NovelResourceSelection.Get<StageAsset>(interactions) is { isMissing: false } stage && ImGui.SmallButton("Use the selected stage's design canvas"))
        { fields.Finish(); m_width = stage.width; m_height = stage.height; }
        fields.Number(id + "/placement-width", "Reference width", ref m_width, 1, 100000);
        fields.Number(id + "/placement-height", "Reference height", ref m_height, 1, 100000);
        m_margin = Math.Clamp(m_margin, 0, Math.Max(0, (Math.Min(m_width, m_height) - 1) / 2));
        fields.Number(id + "/placement-margin", "Safe margin (px)", ref m_margin, 0, Math.Max(0, (Math.Min(m_width, m_height) - 1) / 2));
        if (fields.Select(id + "/placement", "Placement", ref m_position,
            new[] { ("left", "Left"), ("center", "Center"), ("right", "Right"), ("custom", "Custom relative position") }) && m_position != "custom")
        { m_x = m_position == "left" ? .25 : m_position == "right" ? .75 : .5; m_y = 1; }
        if (m_position == "custom")
        {
            fields.Number(id + "/placement-x", "Relative X", ref m_x, -2, 3);
            fields.Number(id + "/placement-y", "Relative Y", ref m_y, -2, 3);
        }
        fields.Select(id + "/placement-anchor", "Character bounds anchor", ref m_anchor,
            new[] { ("top-left", "Top left"), ("top-center", "Top center"), ("top-right", "Top right"), ("middle-left", "Middle left"), ("middle-center", "Center"),
                ("middle-right", "Middle right"), ("bottom-left", "Bottom left"), ("bottom-center", "Bottom center"), ("bottom-right", "Bottom right") });
        EditorUiSession.current.widgets.Hint("Align using the bounds after the actual pose, expression, crop, mirroring and rotation. Only position changes; pivot, scale and opacity are retained. Relative placement is resolved when applied without assuming the current running stage.");
        if (!ImGui.Button("Apply quick placement##" + id)) return false;
        fields.Finish();
        double anchorX = m_anchor.EndsWith("left", StringComparison.Ordinal) ? 0 : m_anchor.EndsWith("right", StringComparison.Ordinal) ? 1 : .5;
        double anchorY = m_anchor.StartsWith("top", StringComparison.Ordinal) ? 0 : m_anchor.StartsWith("bottom", StringComparison.Ordinal) ? 1 : .5;
        command.transform = CharacterPlacementEditService.Place(character, pose, expression, command.transform,
            new((float)m_margin, (float)m_margin, (float)(m_width - m_margin * 2), (float)(m_height - m_margin * 2)), m_x, m_y, anchorX, anchorY);
        return true;
    }
}
