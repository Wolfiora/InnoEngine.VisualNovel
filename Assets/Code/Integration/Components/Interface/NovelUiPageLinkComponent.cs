using System;
using InnoEngine.Reflection;
using InnoEngine.Scene;
using InnoEngine.Serialization;

namespace Inno.VisualNovel.Integration.Components.Interface;

/// <summary>Connects a native button to an explicitly referenced page without a script or a second page tree.</summary>
[StableTypeId("b0f32c52-b546-4259-9e9e-d4cd7ebc9c3a")]
public sealed class NovelUiPageLinkComponent : GameComponent
{
    /// <summary>Gets or sets the real destination page; deleted references remain missing.</summary>
    [SerializableProperty] public NovelPageComponent? target { get; set; }
    /// <summary>Gets or sets whether to show, close, or replace the source page.</summary>
    [SerializableProperty] public NovelUiPageNavigation operation { get; set; }

    /// <summary>Validates every affected object before changing native activation. Showing a page never activates an unrelated parent.</summary>
    public void Apply()
    {
        if (!Enum.IsDefined(operation)) throw new InvalidOperationException("Choose a supported page navigation operation.");
        if (isDestroyed || !gameObject.activeInHierarchy || !gameObject.isRuntimeValid)
            throw new InvalidOperationException("The source page link is no longer active.");
        if (target is null || target.isDestroyed || !target.gameObject.isRuntimeValid || !ReferenceEquals(target.gameObject.scene, gameObject.scene))
            throw new InvalidOperationException("Select an existing page in the same scene.");
        var destination = target.gameObject;
        if (operation == NovelUiPageNavigation.Close) { destination.SetActive(false); return; }
        if (destination.transform.parent is { } parent && !parent.gameObject.activeInHierarchy)
            throw new InvalidOperationException("The destination page has an inactive parent. Place navigable pages under an active canvas.");
        GameObject? source = null;
        for (var ancestor = transform; ancestor is not null; ancestor = ancestor.parent)
        {
            if (ancestor.gameObject.TryGetComponent<NovelUiRootComponent>(out _)) break;
            if (ancestor.gameObject.TryGetComponent<NovelPageComponent>(out _)) { source = ancestor.gameObject; break; }
        }
        if (operation == NovelUiPageNavigation.Replace)
        {
            if (source is null) throw new InvalidOperationException("Replace Page requires a source button inside a native page.");
            for (var ancestor = destination.transform.parent; ancestor is not null; ancestor = ancestor.parent)
                if (ReferenceEquals(ancestor.gameObject, source))
                    throw new InvalidOperationException("The replacement page cannot be a child of the page being closed.");
            if (!ReferenceEquals(source, destination)) source.SetActive(false);
        }
        destination.SetActive(true);
    }
}

/// <summary>Describes a native page activation change with no hidden shell navigation state.</summary>
public enum NovelUiPageNavigation
{
    /// <summary>Activates the destination and retains the source, suitable for a modal overlay.</summary>
    Show,
    /// <summary>Deactivates the destination while retaining all authored objects.</summary>
    Close,
    /// <summary>Closes the source control's nearest page and activates the destination.</summary>
    Replace
}
