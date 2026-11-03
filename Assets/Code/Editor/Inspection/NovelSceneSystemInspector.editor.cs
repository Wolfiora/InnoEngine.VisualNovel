using System;
using InnoEditor.ImGui;
using InnoEditor.Inspection;
using InnoEditor.Scene;
using Inno.VisualNovel.Authoring.SceneEditing;
using Inno.VisualNovel.Integration.Hosting;

namespace Inno.VisualNovel.Editor.Inspection;

/// <summary>Provides discoverable native scene creation and selects each new object for its next authoring step.</summary>
[InspectionDrawer(typeof(NovelSceneSystem))]
public sealed class NovelSceneSystemInspector : InspectionDrawer<NovelSceneSystem>
{
    private readonly NovelUiEditService m_edits;
    private readonly NovelSceneEditService m_sceneEdits;
    private string m_error = string.Empty;
    /// <summary>Connects the System Inspector to native scene creation.</summary>
    /// <param name="scenes">The native scene edit service.</param>
    public NovelSceneSystemInspector(
        SceneEdits scenes
    ) { m_edits = new(scenes, new NativeScenePropertyEdits(scenes)); m_sceneEdits = new(scenes, new NativeScenePropertyEdits(scenes)); }
    /// <inheritdoc />
    public override string icon => ImGuiIcon.File;
    /// <inheritdoc />
    protected override (string, Action<string>?) BindName(
        InspectionDrawContext context,
        NovelSceneSystem target
    ) => ("Visual Novel Scene", null);
    /// <inheritdoc />
    protected override void Draw(
        InspectionDrawContext context,
        NovelSceneSystem target
    ) {
        context.DrawProperties();
        var widgets = EditorUiSession.current.widgets;
        if (target.sceneUiError.Length > 0) widgets.Hint("Scene UI action: " + target.sceneUiError);
        if (target.project is null) widgets.Hint("You can compose this scene without a story. Assign a VN project when you are ready to run dialogue and choices.");
        if (ImGui.CollapsingHeader("Add scene content", ImGuiTreeNodeFlags.DefaultOpen))
        {
            Add("Create background image", () => m_sceneEdits.CreateImage(target.owningScene, "Background", null, new(target.width, target.height)).gameObject);
            Add("Create character", () => m_sceneEdits.CreateCharacter(target.owningScene).gameObject);
            Add("Create UI canvas", () => m_edits.CreateCanvas(target.owningScene).gameObject);
            Add("Create audio object", () => m_sceneEdits.CreateAudio(target.owningScene).gameObject);
            if (ImGui.CollapsingHeader("Camera and performance"))
            {
                Add("Create and use camera", () => m_sceneEdits.CreateCamera(target).gameObject);
                Add("Create performance", () => m_sceneEdits.CreatePerformance(target.owningScene).gameObject);
                Add("Create particles", () => m_sceneEdits.CreateParticles(target.owningScene).gameObject);
                Add("Create screen effect", () => m_sceneEdits.CreateScreenEffect(target.owningScene).gameObject);
            }
            widgets.Hint("New objects appear in Hierarchy and open in Inspector. Assign their resources there; Scene View edits the same objects. Native creation uses separate Undo steps in this SDK.");
        }
        if (m_error.Length > 0) widgets.Hint(m_error);
        if (ImGui.CollapsingHeader("Story target bindings"))
        {
            widgets.Hint("Add VN Target to an object and assign its Target Id. Bind declared scene targets to keep explicit object references for commands and gameplay bridges. Deleted references remain Missing; names are never used to repair them. Story presentation migration is still in progress.");
            try
            {
                if (ImGui.Button("Bind declared scene targets")) { m_sceneEdits.BindSceneTargets(target); m_error = string.Empty; }
                ImGui.SameLine();
                if (ImGui.Button("Validate bindings")) { _ = NovelSceneTargets.Validate(target); m_error = "Story target bindings are valid."; }
            }
            catch (InvalidOperationException error) { m_error = error.Message; }
        }
        void Add(string label, Func<InnoEngine.Scene.GameObject> create)
        {
            if (!ImGui.Button(label)) return;
            try { context.interactions.SetSelection(create()); m_error = string.Empty; }
            catch (Exception error) when (error is not OutOfMemoryException) { m_error = error.Message; }
        }
    }
}
