using System;
using InnoEditor.ImGui;
using InnoEditor.Inspection;
using InnoEditor.Scene;
using Inno.VisualNovel.Integration.Components;

namespace Inno.VisualNovel.Editor.Inspection;

/// <summary>Explains native framing and makes follow sampling an explicit reversible authoring operation.</summary>
[InspectionDrawer(typeof(NovelCameraComponent))]
public sealed class NovelCameraInspector : InspectionDrawer<NovelCameraComponent>
{
    private readonly IInspectionPropertyEditService m_properties;
    private string m_error = string.Empty;
    /// <summary>Creates a camera inspector with the native property history service.</summary>
    /// <param name="scenes">The native scene edit service supplied by the Inspector host.</param>
    public NovelCameraInspector(SceneEdits scenes) => m_properties = new NativeScenePropertyEdits(scenes);
    /// <inheritdoc />
    public override string icon => ImGuiIcon.File;
    /// <inheritdoc />
    protected override (string, Action<string>?) BindName(
        InspectionDrawContext context,
        NovelCameraComponent target
    ) => ("VN Camera", null);
    /// <inheritdoc />
    protected override void Draw(
        InspectionDrawContext context,
        NovelCameraComponent target
    ) {
        context.DrawProperties();
        var widgets = EditorUiSession.current.widgets;
        widgets.Hint("Assign this camera on the Scene's VN System to frame Game View. Scene View keeps its own navigation camera.");
        widgets.Hint("Following writes the native Transform during Play. Its offset uses world coordinates. Turn Following off before using position tracks or moving the camera manually.");
        widgets.Hint("Shake Offset is a separate camera-local channel and can be animated while following. Edit mode never changes the Transform automatically.");
        if (target.following && ImGui.Button("Apply follow position"))
        {
            try
            {
                target.ApplyFollow((owner, property, mutation) => m_properties.ChangeProperty(owner, property, mutation, "Apply camera follow position", null, null));
                m_error = string.Empty;
            }
            catch (InvalidOperationException failure) { m_error = failure.Message; }
        }
        if (m_error.Length > 0) widgets.Hint(m_error);
    }
}
