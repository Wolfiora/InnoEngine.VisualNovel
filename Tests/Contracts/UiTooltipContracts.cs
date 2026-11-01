using System;
using System.Collections.Generic;
using System.Linq;
using Inno.Core.Serialization;
using Inno.VisualNovel.Authoring.LocalizationEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Authoring.UiEditing;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Interface.Data;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.UI.Binding;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class UiTooltipContracts
{
    internal static void InputLayoutAndIsolation()
    {
        var data = new Data();
        var control = Button("help", "fallback"); control.tooltipTextId = "help/text";
        control.layout.x = 510; control.layout.y = 235;
        var page = Page(control);
        var document = new UiDocumentInstance(page, Measure);
        document.HandleInput(new() { pointerInside = true, x = 520, y = 245 }, data);
        Check(Help(document, data) is null, "Pointer help appeared without the dwell interval.");
        for (int index = 0; index < 10; index++) document.BuildFrame(data);
        Check(Help(document, data) is null, "Rendering advanced the pointer dwell clock.");
        document.Tick(.44); Check(Help(document, data) is null, "Pointer dwell rounded up before its deadline.");
        document.Tick(.02); UiDrawItem help = Help(document, data)!;
        Check(help is not null && help.text == "Translated <b>plain</b> help" && help.richText?.document.text == help.text,
            "Localized help was missing or interpreted untrusted formatting markup.");
        Check(help!.bounds.x >= 0 && help.bounds.y >= 0 && help.bounds.x + help.bounds.width <= 600 && help.bounds.y + help.bounds.height <= 300,
            "Bottom-right tooltip escaped its design canvas.");
        Check(!document.BuildFrame(data, false).items.Any(static item => item.id.EndsWith("/tooltip", StringComparison.Ordinal))
            && Help(document, data) is not null, "Capture included transient help or cancelled live input presentation.");
        document.SetFontScale(2); Check(Help(document, data)!.fontSize == 56 && Help(document, data)!.bounds.width <= 600, "Help ignored accessibility scaling or clipping.");
        var overlay = Help(document, data)!;
        var outside = document.HandleInput(new() { pointerInside = true, pressed = true, x = overlay.bounds.x + 1, y = overlay.bounds.y + 1 }, data);
        Check(!outside.consumed && outside.actions.Count == 0, "A painted tooltip acquired input ownership.");
        document.HandleInput(new() { next = true }, data);
        Check(Help(document, data)?.text == "Translated <b>plain</b> help", "Keyboard focus required a pointer dwell or could not show help.");
        document.CancelInput(); document.Tick(1); Check(Help(document, data) is null, "Retired page input retained its tooltip.");
        document.HandleInput(new() { composing = true, next = true }, data); Check(Help(document, data) is null, "IME composition displayed stale help.");
        Reject(() => document.Tick(double.NaN));

        var row = Button("row", ""); row.tooltipBinding = "item.reason"; row.binding = "item.text"; row.enabledBinding = "item.enabled";
        row.layout.width = 500; row.layout.height = 70;
        page = Page(); page.root.kind = UiElementKind.Vertical; page.root.repeatBinding = "rows"; page.root.children = [row];
        data.rows = [Row("a", "Alpha", "Locked Alpha"), Row("b", "Beta", "Locked Beta")];
        document = new(page, Measure); document.HandleInput(new() { pointerInside = true, x = 10, y = 10 }, data);
        document.BuildFrame(data); document.Tick(.5);
        Check(Help(document, data)?.text == "Locked Alpha", "Disabled repeated rows could not explain their own reason.");
        data.rows = [Row("b", "Beta", "Changed Beta"), Row("a", "Alpha", "Locked Alpha")];
        document.HandleInput(new() { pointerInside = true, x = 10, y = 10 }, data); document.BuildFrame(data);
        Check(Help(document, data) is null, "Reordered rows reused another row's pointer dwell or reason.");
        document.Tick(.5); Check(Help(document, data)?.text == "Changed Beta", "Help retained a stale repeated item value.");
        document.HandleInput(new() { pointerInside = true, x = 10, y = 10, pressed = true }, data);
        Check(document.HandleInput(new() { pointerInside = true, x = 10, y = 10, released = true }, data).actions.Count == 0 && data.writes == 0,
            "Explaining a disabled row activated or wrote its value.");
        data.rows = []; Check(Help(document, data) is null, "Removed rows retained their detached help overlay.");
        data.rows = [new("invalid", new Dictionary<string, NovelValue> { ["reason"] = NovelValue.Boolean(true), ["enabled"] = NovelValue.Boolean(false) })];
        Reject(() => document.BuildFrame(data));

        var selector = Button("selector", "Selector help"); selector.kind = UiElementKind.Selector; selector.action = "";
        selector.binding = "selected"; selector.options = [new() { id = "one", text = "One", value = NovelValue.String("one") }];
        data.values["selected"] = NovelValue.String("one"); document = new(Page(selector), Measure);
        document.HandleInput(new() { next = true }, data); Check(Help(document, data) is not null, "Focused selector lost its ordinary help.");
        document.HandleInput(new() { confirm = true }, data);
        Check(document.popupOpen && Help(document, data) is null, "A tooltip painted over an owning selector popup.");
    }

    internal static void NativeAuthoringAndLocalization(SerializationRegistry native)
    {
        var button = Button("one", "Original help"); button.tooltipTextId = "one/help"; button.tooltipBinding = "page.help";
        var page = Page(button); page.stateVariables = [new() { id = "help", name = "Help", scope = VariableScope.Local, initialValue = NovelValue.String("Bound help") }];
        page.root = native.Deserialize<UiElementDefinition>(native.Serialize(page.root));
        Check(page.root.children[0].tooltip == "Original help" && page.root.children[0].tooltipTextId == "one/help" && page.root.children[0].tooltipBinding == "page.help",
            "Native recursive element serialization dropped help metadata.");
        var copy = UiDefinitionCopy.Copy(page); string duplicate = ScreenEditService.Duplicate(copy, "one");
        Check(ScreenEditService.Find(copy, duplicate).tooltipTextId != "one/help" && page.root.children.Length == 1, "Copying controls shared translation identities or changed the source tree.");
        var widget = UiWidgetEditService.Extract(page, "one", "Help widget"); var target = Page();
        string inserted = UiWidgetEditService.Insert(target, "root", widget);
        var insertedControl = ScreenEditService.Find(target, inserted);
        Check(target.stateVariables.Length == 1 && insertedControl.tooltipTextId != "one/help" && insertedControl.tooltipBinding == "page." + target.stateVariables[0].id,
            "Reusable components dropped tooltip-only state or failed to remap its ownership.");
        Reject(() => UiStateEditService.Remove(target, target.stateVariables[0].id));
        UiStateEditService.Remove(target, target.stateVariables[0].id, true);
        Check(ScreenEditService.Find(target, inserted).tooltipBinding.Length == 0, "Explicit field deletion left a dangling help binding.");
        var invalid = UiDefinitionCopy.Copy(page); invalid.stateVariables[0].initialValue = NovelValue.Boolean(true);
        Reject(() => new UiDocumentInstance(invalid, Measure));

        var project = new NovelProjectAsset { screens = [new() { role = "reading", screen = page }] };
        Check(LocalizationExtractionService.Extract(project).Single(static item => item.id == "one/help").source == "Original help", "Localization omitted stable tooltip text.");
        Check(GlyphCoverageSources.Enumerate(project, "zh-CN").Any(static item => item.textId == "one/help" && item.text == "Original help" && !item.rich),
            "Static glyph coverage omitted plain tooltip content.");
        var reading = ShellScreenTemplates.Create("reading", null!, "Clear");
        var automatic = ScreenEditService.Find(reading, "auto");
        project.screens = [new() { role = "reading", screen = reading }];
        var table = StarterLocalizationTemplate.Create(project);
        Check(automatic.tooltip.Length > 0 && table.entries.Single(item => item.id == automatic.tooltipTextId).translations.Any(static item => item.language == "en"),
            "Built-in reading help lacked an English translation.");
        Check(ScreenEditService.Find(reading, "option").tooltipBinding == "item.reason", "Built-in choices omitted disabled-reason help.");

        var source = new UiDataSourceDefinition { id = "status", title = "Status", fields = [new() { schema = new() { id = "reason", defaultValue = NovelValue.String("") } }],
            collections = [new() { id = "items", title = "Items", fields = [new() { schema = new() { id = "reason", defaultValue = NovelValue.String("") } }] }] };
        page = Page(Button("typed", "")); page.root.children[0].tooltipBinding = "custom.status.reason";
        project = new() { screens = [new() { role = "reading", screen = page }], uiDataSources = [source] };
        UiDataBindingValidator.Validate(project); source.fields[0].schema.defaultValue = NovelValue.Integer(1); Reject(() => UiDataBindingValidator.Validate(project));
        page.root.repeatBinding = "custom.status.items"; page.root.children[0].tooltipBinding = "item.reason"; UiDataBindingValidator.Validate(project);
        source.collections[0].fields[0].schema.defaultValue = NovelValue.Boolean(false); Reject(() => UiDataBindingValidator.Validate(project));
        Check(BuiltInUiChoices.TextValues([new() { id = "number", initialValue = NovelValue.Integer(1) }, new() { id = "string", initialValue = NovelValue.String("") }])
            .Any(static item => item.id == "variables.string") && !BuiltInUiChoices.TextValues([]).Any(static item => item.id is "settings.fontScale" or "item.enabled" or "history.count"),
            "Help binding menus included incompatible numeric or Boolean fields.");
    }

    private static UiElementDefinition Button(
        string id,
        string tooltip
    ) => new() { id = id, kind = UiElementKind.Button, tooltip = tooltip, action = "activate", layout = new() { width = 80, height = 50 } };
    private static ScreenAsset Page(params UiElementDefinition[] controls) => new() { width = 600, height = 300, root = new() { id = "root", blocksPointer = false, layout = new() { width = 600, height = 300 }, children = controls } };
    private static (float, float) Measure(
        UiElementDefinition element,
        string text
    ) => (text.Length * element.style.fontSize, element.style.fontSize);
    private static UiDrawItem? Help(
        UiDocumentInstance document,
        Data data
    ) => document.BuildFrame(data).items.SingleOrDefault(static item => item.id.EndsWith("/tooltip", StringComparison.Ordinal));
    private static UiDataRow Row(
        string id,
        string text,
        string reason
    ) => new(id, new Dictionary<string, NovelValue> { ["text"] = NovelValue.String(text), ["reason"] = NovelValue.String(reason), ["enabled"] = NovelValue.Boolean(false) });
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
    private static void Reject(Action operation) { try { operation(); } catch (ArgumentException) { return; } catch (InvalidOperationException) { return; } throw new InvalidOperationException("Invalid help operation was accepted."); }
    private sealed class Data : UiDataSource
    {
        internal int writes;
        internal UiDataRow[] rows = [];
        internal readonly Dictionary<string, NovelValue> values = new(StringComparer.Ordinal);
        public override bool TryRead(string path, out NovelValue value) => values.TryGetValue(path, out value!);
        public override bool TryWrite(string path, NovelValue value) { writes++; values[path] = value.Copy(); return true; }
        public override IReadOnlyList<UiDataRow> ReadRows(string path) => rows;
        public override string Localize(string id, string source) => id == "help/text" ? "Translated <b>plain</b> help" : source;
    }
}
