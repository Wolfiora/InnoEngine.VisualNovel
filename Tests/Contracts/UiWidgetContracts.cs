using System;
using System.Collections.Generic;
using System.Linq;
using Inno.Rendering.Assets;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Authoring.UiEditing;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.UI.Binding;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class UiWidgetContracts
{
    internal static void IndependentInstances()
    {
        foreach (var preset in UiWidgetTemplates.entries)
        {
            var template = UiWidgetTemplates.Create(preset.id, null!);
            _ = new UiDocumentInstance(template, static (_, _) => (80, 30)).BuildFrame(new Values());
        }
        var source = UiWidgetTemplates.Create("button", null!);
        source.root.options = [new() { id = "option", textId = "option-text", value = NovelValue.String("stable-value") }];
        var destination = new ScreenAsset { width = 800, height = 400, root = new() { id = "root", kind = UiElementKind.Horizontal,
            layout = new() { width = 800, height = 400, gap = 10 } } };
        string first = UiWidgetEditService.Insert(destination, "root", source);
        string second = UiWidgetEditService.Insert(destination, "root", source);
        Check(first != second && ScreenEditService.Flatten(destination).Select(static row => row.element.id).Distinct().Count() == 3,
            "Reusable controls reused page-local identities.");
        var a = ScreenEditService.Find(destination, first); var b = ScreenEditService.Find(destination, second);
        Check(a.textId != b.textId && a.options[0].id != b.options[0].id && a.options[0].textId != b.options[0].textId
            && a.options[0].value.text == "stable-value", "Instantiation lost stable authored values or reused localization identities.");
        a.style.fontSize = 50; a.options[0].text = "Changed";
        Check(b.style.fontSize == source.root.style.fontSize && b.options[0].text != "Changed", "Two component instances retained mutable declaration aliases.");
        var extracted = UiWidgetEditService.Extract(destination, second, "独立组件");
        extracted.root.text = "修改组件";
        Check(b.text != extracted.root.text && extracted.root.layout.x == 0 && extracted.root.layout.anchorX == 0, "Extraction changed the source or retained its external placement.");
        var ui = new UiDocumentInstance(destination, static (_, _) => (80, 30)); var data = new Values();
        ui.BuildFrame(data); ui.HandleInput(new() { next = true }, data);
        var action = ui.HandleInput(new() { confirm = true }, data);
        Check(action.consumed && action.actions.Single().action == "advance", "The inserted control did not execute its editable action through the real UI runtime.");
        TextureAsset one = new(2, 2, TextureColorSpace.Srgb, "png"), two = new(2, 2, TextureColorSpace.Srgb, "png");
        destination.textIcons = [new() { key = "icon", image = one }]; source.textIcons = [new() { key = "icon", image = two }];
        int count = ScreenEditService.Flatten(destination).Count; bool rejected = false;
        try { UiWidgetEditService.Insert(destination, "root", source); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && ScreenEditService.Flatten(destination).Count == count && destination.textIcons[0].image == one,
            "A conflicting borrowed resource modified the target before rejection.");
    }
    private sealed class Values : UiDataSource
    {
        public override bool TryRead(string path, out NovelValue value) { value = path == "settings.textSpeed" ? NovelValue.Number(1) : NovelValue.Boolean(true); return true; }
        public override bool TryWrite(string path, NovelValue value) => true;
        public override IReadOnlyList<UiDataRow> ReadRows(string path) => [];
    }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}
