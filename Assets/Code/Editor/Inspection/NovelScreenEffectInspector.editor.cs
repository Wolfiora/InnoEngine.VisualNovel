using System;
using InnoEditor.ImGui;
using InnoEditor.Inspection;
using InnoEditor.Scene;
using Inno.VisualNovel.Integration.Components;

namespace Inno.VisualNovel.Editor.Inspection;

/// <summary>Explains preset ownership and edits per-object effect overrides through native property history.</summary>
[InspectionDrawer(typeof(NovelScreenEffectComponent))]
public sealed class NovelScreenEffectInspector : InspectionDrawer<NovelScreenEffectComponent>
{
    private readonly IInspectionPropertyEditService m_properties;
    private string m_error = string.Empty;
    /// <summary>Creates an inspector using the editor's native property transaction service.</summary>
    /// <param name="scenes">The native scene edit service supplied by the Inspector host.</param>
    public NovelScreenEffectInspector(SceneEdits scenes) => m_properties = new NativeScenePropertyEdits(scenes);
    /// <inheritdoc />
    public override string icon => ImGuiIcon.File;
    /// <inheritdoc />
    protected override (string, Action<string>?) BindName(
        InspectionDrawContext context,
        NovelScreenEffectComponent target
    ) => ("VN Screen Effect", null);
    /// <inheritdoc />
    protected override void Draw(
        InspectionDrawContext context,
        NovelScreenEffectComponent target
    ) {
        context.DrawProperties();
        var widgets = EditorUiSession.current.widgets;
        widgets.Hint(target.parameters is null
            ? "Parameters inherit the preset. Create an override to adjust this object without changing other instances."
            : "Parameters belong to this object. Use preset values to remove the override, including values written by a performance clip.");
        widgets.Hint("Edit mode displays the current elapsed time without advancing it. A strength track requires Duration = 0; parameter tracks can accompany a fade.");
        if (target.parameters is null && ImGui.Button("Override preset parameters"))
        {
            try
            {
                var copy = target.CaptureParameters();
                m_properties.ChangeProperty(target, nameof(target.parameters), () => target.parameters = copy, "Override screen-effect parameters", null, null);
                m_error = string.Empty;
            }
            catch (InvalidOperationException failure) { m_error = failure.Message; }
        }
        else if (target.parameters is not null && ImGui.Button("Use preset values"))
        {
            m_properties.ChangeProperty(target, nameof(target.parameters), () => target.parameters = null, "Reset screen-effect parameters", null, null);
            m_error = string.Empty;
        }
        if (m_error.Length > 0) widgets.Hint(m_error);
    }
}
