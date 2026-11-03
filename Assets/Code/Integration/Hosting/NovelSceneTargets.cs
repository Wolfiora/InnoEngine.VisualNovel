using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Scene;
using Inno.VisualNovel.Integration.Components;

namespace Inno.VisualNovel.Integration.Hosting;

/// <summary>Validates explicit scene-owned story references without searching by display name or repairing deleted objects.</summary>
public static class NovelSceneTargets
{
    /// <summary>Resolves one typed capability from the system's current explicit target bindings.</summary>
    /// <typeparam name="TComponent">The native component capability required by the calling bridge or command.</typeparam>
    /// <param name="system">The scene system that owns the binding scope.</param>
    /// <param name="targetId">The exact authored logical target identity.</param>
    /// <returns>The currently referenced real component, including inactive objects that an explicit show operation may reactivate.</returns>
    public static TComponent Resolve<TComponent>(
        NovelSceneSystem system,
        string targetId
    ) where TComponent : GameComponent
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);
        var bindings = Validate(system);
        if (!bindings.TryGetValue(targetId, out var binding))
            throw new InvalidOperationException("The scene system has no explicit story binding for '" + targetId + "'.");
        var components = binding.gameObject.GetComponents<TComponent>().Where(static component => !component.isDestroyed).ToArray();
        if (components.Length != 1)
            throw new InvalidOperationException("Story target '" + targetId + "' requires exactly one " + typeof(TComponent).Name + ".");
        return components[0];
    }

    /// <summary>Checks all explicit references before a session starts or a bridge resolves a target.</summary>
    /// <param name="system">The native owner; each reference must remain in this exact scene.</param>
    /// <returns>A detached lookup of the referenced components; it creates no objects or substitute identities.</returns>
    public static IReadOnlyDictionary<string, NovelTargetComponent> Validate(NovelSceneSystem system)
    {
        ArgumentNullException.ThrowIfNull(system);
        if (system.isDestroyed) throw new InvalidOperationException("The story binding system was destroyed.");
        var bindings = system.targets ?? throw new InvalidOperationException("The scene target array cannot be null.");
        if (bindings.Length > 10000) throw new InvalidOperationException("A scene system supports at most 10000 explicit story targets.");
        var result = new Dictionary<string, NovelTargetComponent>(StringComparer.Ordinal);
        for (int index = 0; index < bindings.Length; index++)
        {
            var target = bindings[index];
            if (target is null || target.isDestroyed || target.gameObject.isDestroyed || !target.gameObject.isRuntimeValid)
                throw new InvalidOperationException("Story target binding " + index + " is missing. Assign the intended object explicitly.");
            if (!ReferenceEquals(target.gameObject.scene, system.owningScene))
                throw new InvalidOperationException("Story target binding " + index + " belongs to another scene.");
            if (string.IsNullOrWhiteSpace(target.targetId) || target.targetId.Length > 256)
                throw new InvalidOperationException("Story target binding " + index + " needs an identity between 1 and 256 characters.");
            if (!result.TryAdd(target.targetId, target))
                throw new InvalidOperationException("Duplicate story target binding: '" + target.targetId + "'.");
        }
        return result;
    }
}
