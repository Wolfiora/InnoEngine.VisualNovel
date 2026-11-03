using System;
using InnoEditor.ImGui;
using InnoEditor.Inspection;
using InnoEditor.Scene;
using Inno.VisualNovel.Authoring.SceneEditing;
using Inno.VisualNovel.Integration.Components;

namespace Inno.VisualNovel.Editor.Inspection;

/// <summary>Edits native character appearances with explicit operations and shared engine History.</summary>
[InspectionDrawer(typeof(NovelCharacterComponent))]
public sealed class NovelCharacterInspector : InspectionDrawer<NovelCharacterComponent>
{
    private readonly SceneEdits m_scenes;
    private readonly IInspectionPropertyEditService m_properties;
    private Guid m_target;
    private string m_pose = string.Empty, m_expression = string.Empty, m_error = string.Empty;

    /// <summary>Creates a current-generation native character Inspector.</summary>
    /// <param name="scenes">The engine scene edit service.</param>
    public NovelCharacterInspector(
        SceneEdits scenes
    ) { m_scenes = scenes; m_properties = new NativeScenePropertyEdits(scenes); }
    /// <inheritdoc />
    public override string icon => ImGuiIcon.File;
    /// <inheritdoc />
    protected override (string, Action<string>?) BindName(
        InspectionDrawContext context,
        NovelCharacterComponent target
    ) => ("VN Character", null);
    /// <inheritdoc />
    protected override void Draw(
        InspectionDrawContext context,
        NovelCharacterComponent target
    ) {
        context.DrawProperties();
        var widgets = EditorUiSession.current.widgets;
        if (target.character is not { } resource) { widgets.Hint("Assign a character resource, then create its native image parts."); return; }
        if (m_target != target.identity.persistentId)
        {
            m_target = target.identity.persistentId; m_pose = target.pose; m_expression = target.expression; m_error = string.Empty;
        }
        if (widgets.BeginBoundedCombo("Apply pose", m_pose.Length == 0 ? "Resource default" : m_pose))
        {
            try { foreach (var pose in resource.poses) if (ImGui.Selectable(pose.name + "##" + pose.id, m_pose == pose.id)) m_pose = pose.id; }
            finally { widgets.EndBoundedCombo(); }
        }
        if (widgets.BeginBoundedCombo("Apply expression", m_expression.Length == 0 ? "Resource default" : m_expression))
        {
            try { foreach (var expression in resource.expressions) if (ImGui.Selectable(expression.name + "##" + expression.id, m_expression == expression.id)) m_expression = expression.id; }
            finally { widgets.EndBoundedCombo(); }
        }
        widgets.Hint("Creating parts uses separate native Undo/Redo steps with this SDK. Applying an appearance is one property transaction.");
        var edits = new NovelCharacterEditService(m_scenes, context.interactions.history, m_properties);
        try
        {
            if (ImGui.Button("Create missing parts")) { edits.CreateMissingParts(target); m_error = string.Empty; }
            if (ImGui.Button("Apply appearance")) { edits.ApplyAppearance(target, m_pose, m_expression); m_error = string.Empty; }
        }
        catch (Exception failure) when (failure is not OutOfMemoryException) { m_error = failure.Message; }
        if (m_error.Length > 0) widgets.Hint(m_error);
        widgets.Hint("Parts are real child objects. Applying an appearance changes their image and Transform properties; ordinary rendering preserves your edits.");
    }
}
