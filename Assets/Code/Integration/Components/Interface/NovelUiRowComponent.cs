using InnoEngine.Reflection;
using InnoEngine.Scene;
using Inno.VisualNovel.UI.Binding;

namespace Inno.VisualNovel.Integration.Components.Interface;

/// <summary>Associates a real repeated Prefab instance with its current neutral data row; regenerated view data is not saved as an alternate UI tree.</summary>
[StableTypeId("b0f32c52-b546-4259-9e9e-d4cd7ebc9c2f")]
public sealed class NovelUiRowComponent : GameComponent
{
    /// <summary>Gets the list that owns this runtime instance, or null before explicit synchronization.</summary>
    public NovelUiListComponent? owner { get; internal set; }
    /// <summary>Gets the detached item data for current bindings, or null before synchronization.</summary>
    public UiDataRow? row { get; internal set; }

    internal static UiDataRow? Find(GameObject item)
    {
        for (var current = item.transform; current is not null; current = current.parent)
        {
            if (current.gameObject.TryGetComponent<NovelUiRowComponent>(out var marker) && marker?.row is not null) return marker.row;
            if (current.gameObject.TryGetComponent<NovelUiRootComponent>(out _)) break;
        }
        return null;
    }
}
