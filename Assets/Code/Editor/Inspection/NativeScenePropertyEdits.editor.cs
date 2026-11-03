using System;
using InnoEditor.Inspection;
using InnoEditor.Scene;
using InnoEngine.Scene;

namespace Inno.VisualNovel.Editor.Inspection;

/// <summary>Adapts the Inspector's supplied SceneEdits service to authoring operations without owning History.</summary>
internal sealed class NativeScenePropertyEdits(SceneEdits scenes) : IInspectionPropertyEditService
{
    /// <inheritdoc />
    public bool ChangeProperty(
        object owner,
        string propertyName,
        Action mutation,
        string historyName,
        string? mergeKey,
        string? componentName
    ) {
        if (owner is not EngineObject target) throw new ArgumentException("Native UI authoring requires a scene object.", nameof(owner));
        if (componentName is not null) throw new ArgumentException("Native scene operations edit complete root properties.", nameof(componentName));
        return scenes.ChangeProperty(target, propertyName, mutation, historyName, mergeKey);
    }
}
