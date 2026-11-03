using System;
using InnoEngine.Scene;
using Inno.VisualNovel.Integration.Components.Interface;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.UI.Binding;
using Inno.VisualNovel.UI.Input;

namespace Inno.VisualNovel.Integration.Interface;

internal static class NovelUiActionReader
{
    internal static UiActionRequest? Read(
        GameObject owner,
        UiDataSource? source
    ) {
        owner.TryGetComponent<NovelUiPageLinkComponent>(out var link);
        owner.TryGetComponent<NovelUiListNavigationComponent>(out var navigation);
        if (link is not null || navigation is not null)
        {
            if (link is not null && navigation is not null || owner.TryGetComponent<NovelUiActionComponent>(out var other) && other is { action.Length: > 0 })
                throw new InvalidOperationException("A button must choose one page navigation, list navigation, or story action.");
            return new(link is not null ? "scene.page" : "scene.list", string.Empty, ownerId: owner.identity.persistentId.ToString());
        }
        if (!owner.TryGetComponent<NovelUiActionComponent>(out var declaration) || declaration is null || declaration.action.Length == 0) return null;
        NovelValue? value = null;
        if (declaration.argumentBinding.Length > 0 && source is not null)
        {
            if (!source.TryRead(declaration.argumentBinding, NovelUiRowComponent.Find(owner), out value))
                throw new InvalidOperationException("UI action argument is missing: " + declaration.argumentBinding);
            value = value.Copy();
        }
        return new(declaration.action, value?.ToString() ?? declaration.argument, value, owner.identity.persistentId.ToString(), declaration.argumentBinding);
    }
    internal static bool Condition(
        UiDataSource? source,
        string path,
        UiDataRow? row = null
    ) {
        if (source is null || path.Length == 0) return true;
        if (!source.TryRead(path, row, out var value) || value.kind != NovelValueKind.Boolean)
            throw new InvalidOperationException("Native UI conditions require a declared Boolean field: " + path);
        return value.boolean;
    }
}
