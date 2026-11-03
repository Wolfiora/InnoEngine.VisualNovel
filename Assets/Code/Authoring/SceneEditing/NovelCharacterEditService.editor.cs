using System;
using System.Linq;
using InnoEngine.Scene;
using InnoEditor.Scene;
using InnoEditor.Interactions;
using InnoEditor.Inspection;
using Inno.VisualNovel.Integration.Components;
using Inno.VisualNovel.Model.Characters;

namespace Inno.VisualNovel.Authoring.SceneEditing;

/// <summary>Creates and edits real character hierarchies through native scene History transactions.</summary>
public sealed class NovelCharacterEditService
{
    private readonly SceneEdits m_scenes;
    private readonly IEditorHistory m_history;
    private readonly IInspectionPropertyEditService m_properties;

    /// <summary>Creates the current editor generation's character operations.</summary>
    /// <param name="scenes">The native scene mutation service.</param>
    /// <param name="history">The current host-owned history branch.</param>
    /// <param name="properties">The native scene property writer.</param>
    public NovelCharacterEditService(
        SceneEdits scenes,
        IEditorHistory history,
        IInspectionPropertyEditService properties
    ) { m_scenes = scenes; m_history = history; m_properties = properties; }

    /// <summary>Creates a real root and independently editable image children for all declared pose parts.</summary>
    /// <param name="scene">The destination scene.</param>
    /// <param name="resource">Imported character metadata and appearances.</param>
    /// <param name="parent">Optional native parent.</param>
    /// <returns>The component attached to the newly created root.</returns>
    public NovelCharacterComponent Create(
        GameScene scene,
        CharacterAsset resource,
        Transform? parent = null
    ) {
        ArgumentNullException.ThrowIfNull(resource); CharacterAnimationRules.Validate(resource);
        var root = m_scenes.CreateGameObject(scene, parent);
        m_scenes.RenameGameObject(root, resource.displayName);
        var owner = (NovelCharacterComponent)m_scenes.AddComponent(root, typeof(NovelCharacterComponent));
        Change(owner, nameof(owner.character), () => owner.character = resource);
        Change(owner, nameof(owner.breathing), () => owner.breathing = resource.breathing.Copy());
        CreateMissingParts(owner);
        ApplyAppearance(owner, resource.defaultPose, resource.defaultExpression);
        return owner;
    }

    /// <summary>Adds missing native image objects explicitly; ordinary rendering never recreates deleted parts.</summary>
    /// <param name="owner">The native character whose author requested this operation.</param>
    public void CreateMissingParts(NovelCharacterComponent owner)
    {
        var resource = owner.character ?? throw new InvalidOperationException("Assign a character resource first.");
        CharacterAnimationRules.Validate(resource);
        var existing = owner.Parts().Select(static part => part.partId).ToHashSet(StringComparer.Ordinal);
        var missing = resource.poses.SelectMany(static pose => pose.parts).GroupBy(static part => part.id, StringComparer.Ordinal)
            .Where(group => !existing.Contains(group.Key)).Select(static group => group.First()).ToArray();
        if (missing.Any(static part => string.IsNullOrWhiteSpace(part.id)))
            throw new InvalidOperationException("Character parts require nonempty semantic identities.");
        foreach (var declaration in missing)
        {
            var item = m_scenes.CreateGameObject(owner.gameObject.scene, owner.transform);
            m_scenes.RenameGameObject(item, declaration.name.Length == 0 ? declaration.id : declaration.name);
            var image = (NovelImageComponent)m_scenes.AddComponent(item, typeof(NovelImageComponent));
            Change(image, nameof(image.visible), () => image.visible = false);
            var part = (NovelCharacterPartComponent)m_scenes.AddComponent(item, typeof(NovelCharacterPartComponent));
            Change(part, nameof(part.character), () => part.character = owner);
            Change(part, nameof(part.partId), () => part.partId = declaration.id);
        }
    }

    /// <summary>Uses the runtime appearance operation with native property history for every affected component and Transform.</summary>
    /// <param name="owner">The actual character component.</param>
    /// <param name="pose">The selected pose identity.</param>
    /// <param name="expression">The selected expression identity.</param>
    public void ApplyAppearance(
        NovelCharacterComponent owner,
        string pose,
        string expression
    ) {
        using var transaction = m_history.BeginTransaction("Apply character appearance");
        owner.ApplyAppearance(pose, expression, Change);
        transaction.Commit();
    }

    private void Change(
        EngineObject owner,
        string property,
        Action edit
    ) => m_properties.ChangeProperty(owner, property, edit, "Edit character appearance", null, null);
}
