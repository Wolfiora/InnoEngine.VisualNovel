using System;
using InnoEngine.Scene;
using Inno.VisualNovel.Integration.Components.Interface;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.UI.Binding;

namespace Inno.VisualNovel.Integration.Interface.Engine;

internal static class NovelEngineUiToggle
{
    internal static bool Read(
        NovelUiToggleComponent toggle,
        UiDataSource? source
    ) {
        toggle.gameObject.TryGetComponent<NovelUiBindingComponent>(out var binding);
        if (binding is not { value.Length: > 0 } || source is null) return toggle.value;
        if (!source.TryRead(binding.value, NovelUiRowComponent.Find(toggle.gameObject), out var value) || value.kind != NovelValueKind.Boolean)
            throw new InvalidOperationException("Toggle bindings require a Boolean field: " + binding.value);
        return value.boolean;
    }

    internal static bool Activate(
        GameObject owner,
        UiDataSource? source
    ) {
        if (!owner.TryGetComponent<NovelUiToggleComponent>(out var toggle) || toggle is null) return true;
        bool next = !Read(toggle, source);
        owner.TryGetComponent<NovelUiBindingComponent>(out var binding);
        if (binding is { value.Length: > 0 })
        {
            if (source is null) throw new InvalidOperationException("A bound toggle requires an active data source.");
            return source.TryWrite(binding.value, NovelValue.Boolean(next), NovelUiRowComponent.Find(owner));
        }
        toggle.value = next;
        return true;
    }
}
