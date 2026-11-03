using System;
using InnoEngine.Mathematics;
using InnoEngine.Scene;
using InnoEngine.Serialization;
using InnoEngine.Reflection;

namespace Inno.VisualNovel.Integration.Components;

/// <summary>Defines the game framing independently of the editor's navigation camera.</summary>
[StableTypeId("b0f32c52-b546-4259-9e9e-d4cd7ebc9c03")]
public sealed class NovelCameraComponent : GameComponent
{
    /// <summary>Gets or sets positive magnification around this object's native origin.</summary>
    [SerializableProperty] public float zoom { get; set; } = 1;
    /// <summary>Gets or sets the exact native target; names are never used to repair a missing reference.</summary>
    [SerializableProperty] public GameObject? followTarget { get; set; }
    /// <summary>Gets or sets whether runtime updates own this camera's position through the explicit target.</summary>
    [SerializableProperty] public bool following { get; set; }
    /// <summary>Gets or sets the world-space offset from the followed object's origin.</summary>
    [SerializableProperty] public Vector3 followOffset { get; set; }
    /// <summary>Gets or sets a separate camera-local shake channel; it never changes the authored Transform.</summary>
    [SerializableProperty] public Vector2 shakeOffset { get; set; }

    /// <summary>Applies the current follow target through the native Transform, optionally using editor History.</summary>
    /// <param name="change">Optional synchronous native property writer; never retained.</param>
    public void ApplyFollow(Action<EngineObject, string, Action>? change = null)
    {
        ValidateFollow();
        if (!following || !gameObject.activeInHierarchy || !followTarget!.activeInHierarchy) return;
        Vector3 position = followTarget.transform.worldPosition + followOffset;
        if (transform.parent is { } parent) position = Vector3.Transform(position, parent.worldToLocalMatrix);
        if (change is null) transform.localPosition = position;
        else change(transform, nameof(transform.localPosition), () => transform.localPosition = position);
    }

    internal void ValidateFollow()
    {
        if (!following) return;
        if (!float.IsFinite(followOffset.x) || !float.IsFinite(followOffset.y) || !float.IsFinite(followOffset.z))
            throw new InvalidOperationException("Camera follow offset must be finite.");
        if (followTarget is not { isDestroyed: false, isRuntimeValid: true } target
            || !ReferenceEquals(target.scene, gameObject.scene))
            throw new InvalidOperationException("Camera following requires a live object in the same scene.");
        for (var ancestor = target.transform; ancestor is not null; ancestor = ancestor.parent)
        {
            if (ReferenceEquals(ancestor, transform))
                throw new InvalidOperationException("A camera cannot follow itself or its own descendants.");
            foreach (var camera in ancestor.gameObject.GetComponents<NovelCameraComponent>())
                if (camera.following && camera.gameObject.activeInHierarchy)
                    throw new InvalidOperationException("A camera follow target cannot be controlled by another following camera. Bind both cameras to the original target.");
        }
        if (transform.parent is { } parent) _ = parent.worldToLocalMatrix;
    }
}
