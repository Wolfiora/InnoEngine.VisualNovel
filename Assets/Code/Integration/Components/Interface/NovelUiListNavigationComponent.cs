using System;
using InnoEngine.Reflection;
using InnoEngine.Scene;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Integration.Components.Interface;

/// <summary>Connects a native button to a bounded collection window without a script or hidden item tree.</summary>
[StableTypeId("b0f32c52-b546-4259-9e9e-d4cd7ebc9c3b")]
public sealed class NovelUiListNavigationComponent : GameComponent
{
    /// <summary>Gets or sets the real list in this scene; deleted references remain missing.</summary>
    [SerializableProperty] public NovelUiListComponent? target { get; set; }
    /// <summary>Gets or sets the requested page movement, using the list's capacity as page size.</summary>
    [SerializableProperty] public NovelUiListNavigation operation { get; set; } = NovelUiListNavigation.Next;

    /// <summary>Gets whether the destination is live and the requested movement changes its current window.</summary>
    public bool canNavigate => Destination() is { } list && Next(list) != list.first;

    /// <summary>Changes only the real list offset; its next update reconciles native Prefab rows.</summary>
    public void Apply()
    {
        if (isDestroyed || !gameObject.isRuntimeValid || !gameObject.activeInHierarchy)
            throw new InvalidOperationException("The list navigation source is no longer active.");
        var list = Destination() ?? throw new InvalidOperationException("Select an active collection list in this scene with a valid offset and capacity.");
        list.first = Next(list);
    }

    private NovelUiListComponent? Destination()
    {
        if (!Enum.IsDefined(operation)) throw new InvalidOperationException("Choose a supported list navigation operation.");
        return target is { isDestroyed: false, isActiveAndEnabled: true } list && list.gameObject.isRuntimeValid
            && ReferenceEquals(list.gameObject.scene, gameObject.scene) && list.first >= 0 && list.capacity is >= 1 and <= 512 ? list : null;
    }
    private int Next(NovelUiListComponent list)
    {
        int last = list.total == 0 ? 0 : (list.total - 1) / list.capacity * list.capacity;
        int current = Math.Min(list.first, last);
        return operation switch
        {
            NovelUiListNavigation.First => 0,
            NovelUiListNavigation.Previous => Math.Max(0, current - list.capacity),
            NovelUiListNavigation.Next => Math.Min(last, current + list.capacity),
            NovelUiListNavigation.Last => last,
            _ => throw new InvalidOperationException("Choose a supported list navigation operation.")
        };
    }
}

/// <summary>Describes movement through a collection using the destination list's actual capacity.</summary>
public enum NovelUiListNavigation
{
    /// <summary>Shows the first window.</summary>
    First,
    /// <summary>Moves one window toward the beginning, clamped to zero.</summary>
    Previous,
    /// <summary>Moves one window toward the end, clamped to the final page.</summary>
    Next,
    /// <summary>Shows the last nonempty page, or the empty first page.</summary>
    Last
}
