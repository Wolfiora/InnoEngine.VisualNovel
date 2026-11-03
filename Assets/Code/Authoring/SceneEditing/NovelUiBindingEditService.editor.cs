using System;
using System.Linq;
using InnoEditor.Inspection;
using InnoEngine.Scene;
using Inno.VisualNovel.Integration.Components.Interface;
using Inno.VisualNovel.Integration.Hosting;
using Inno.VisualNovel.Model.Project;

namespace Inno.VisualNovel.Authoring.SceneEditing;

/// <summary>Applies native UI binding and action edits through the engine's property History.</summary>
public sealed class NovelUiBindingEditService
{
    private readonly IInspectionPropertyEditService m_properties;
    /// <summary>Uses the current editor generation's reversible property writer.</summary>
    /// <param name="properties">The engine-owned History adapter.</param>
    public NovelUiBindingEditService(IInspectionPropertyEditService properties) => m_properties = properties;

    /// <summary>Reads the explicitly scene-owned game's metadata without searching other scenes or starting a session.</summary>
    /// <param name="owner">The UI object's actual scene owner.</param>
    /// <returns>The assigned game, or null before a story is configured.</returns>
    public static NovelProjectAsset? Project(GameObject owner)
    {
        var systems = owner.scene.GetSystems().OfType<NovelSceneSystem>().ToArray();
        if (systems.Length > 1) throw new InvalidOperationException("Keep one Visual Novel System in this scene before selecting game bindings.");
        return systems.Length == 0 ? null : systems[0].project;
    }

    /// <summary>Selects a collection through native History without instantiating runtime rows in the edit scene.</summary>
    /// <param name="target">The actual list component.</param>
    /// <param name="collection">A stable built-in or extension collection path.</param>
    public void SetCollection(
        NovelUiListComponent target,
        string collection
    ) {
        Validate(collection);
        if (string.IsNullOrWhiteSpace(collection)) throw new ArgumentException("Choose a collection for the list.", nameof(collection));
        m_properties.ChangeProperty(target, nameof(target.collection), () => target.collection = collection, "Change UI collection", null, null);
    }
    /// <summary>Changes one declared binding path as a native, undoable property edit.</summary>
    /// <param name="target">The actual scene component.</param>
    /// <param name="property">One of its declared binding property names.</param>
    /// <param name="value">The selected stable field path, or empty to use authored values.</param>
    public void SetBinding(
        NovelUiBindingComponent target,
        string property,
        string value
    ) {
        Validate(value);
        Action apply = property switch
        {
            nameof(target.text) => () => target.text = value,
            nameof(target.visible) => () => target.visible = value,
            nameof(target.interactable) => () => target.interactable = value,
            nameof(target.value) => () => target.value = value,
            _ => throw new ArgumentException("Select a supported native binding property.", nameof(property))
        };
        m_properties.ChangeProperty(target, property, apply, "Change UI binding", null, null);
    }
    /// <summary>Changes a story or registered extension action without modifying the button's native appearance.</summary>
    /// <param name="target">The action component.</param>
    /// <param name="property">The action, argument or argumentBinding property.</param>
    /// <param name="value">The selected stable identifier or literal value.</param>
    /// <param name="mergeKey">Optional engine-owned input gesture identity for typing.</param>
    public void SetAction(
        NovelUiActionComponent target,
        string property,
        string value,
        string? mergeKey = null
    ) {
        Validate(value);
        Action apply = property switch
        {
            nameof(target.action) => () => target.action = value,
            nameof(target.argument) => () => target.argument = value,
            nameof(target.argumentBinding) => () => target.argumentBinding = value,
            _ => throw new ArgumentException("Select a supported UI action property.", nameof(property))
        };
        m_properties.ChangeProperty(target, property, apply, "Change UI action", mergeKey, null);
    }
    private static void Validate(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length > 4096 || value.IndexOf('\0') >= 0) throw new ArgumentException("UI bindings and arguments must be bounded text.", nameof(value));
    }
}
