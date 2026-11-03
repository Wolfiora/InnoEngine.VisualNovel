using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Interface.Themes;
using Inno.VisualNovel.UI.Theming;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Authoring.UiEditing;
using InnoEditor.ImGui;
using System.Linq;
using Inno.VisualNovel.Model.Interface.Actions;
using Inno.VisualNovel.Model.Interface.Data;

namespace Inno.VisualNovel.Editor.ScreenDesigner;

internal sealed class ScreenElementInspector(StoryInspectorFields fields)
{
    private readonly NovelValueView m_values = new(fields);
    private readonly TextFormattingView m_formatting = new(fields);
    private readonly UiActionArgumentView m_actions = new(fields);
    internal bool Draw(
        UiElementDefinition element,
        VariableDefinition[] variables,
        ThemeAsset? theme = null,
        VariableDefinition[]? pageVariables = null,
        UiElementDefinition[]? imageViewports = null,
        UiActionDefinition[]? actions = null,
        UiDataSourceDefinition[]? sources = null,
        UiDataCollectionDefinition? rowOwner = null
    ) {
        pageVariables ??= [];
        imageViewports ??= [];
        actions ??= [];
        sources ??= [];
        string id = element.id;
        string text = element.text;
        bool changed = fields.MultilineText(id + "/text", "Text", ref text); element.text = text;
        bool rich = element.richText;
        changed |= fields.Toggle(id + "/rich-text", "Parse text formatting", ref rich); element.richText = rich;
        if (rich) { changed |= m_formatting.Draw(id + "/text", ref text); element.text = text; }
        string textId = element.textId;
        changed |= fields.Text(id + "/text-id", "Stable translation ID", ref textId); element.textId = textId;
        if (ImGui.CollapsingHeader("Hover and focus help##" + id))
        {
            string help = element.tooltip, helpId = element.tooltipTextId, helpBinding = element.tooltipBinding;
            changed |= fields.MultilineText(id + "/tooltip", "Help text (plain text)", ref help); element.tooltip = help;
            changed |= fields.Text(id + "/tooltip-id", "Help translation ID", ref helpId); element.tooltipTextId = helpId;
            changed |= fields.Select(id + "/tooltip-binding", "Help text binding", ref helpBinding,
                BuiltInUiChoices.TextValues(variables).Concat(BuiltInUiChoices.PageFields(pageVariables, NovelValueKind.String))
                    .Concat(UiDataSourceChoices.Values(sources, NovelValueKind.String, row: rowOwner))); element.tooltipBinding = helpBinding;
            EditorUiSession.current.widgets.Hint("Appears after pointer dwell, or immediately for keyboard focus. Bind item.reason to explain disabled choices. Help overlays do not receive input.");
        }
        string imageBinding = element.imageBinding;
        changed |= fields.Select(id + "/image-binding", "Image binding", ref imageBinding, BuiltInUiChoices.Images.Concat(UiDataSourceChoices.Images(sources))); element.imageBinding = imageBinding;
        UiImageFit imageFit = element.imageFit;
        changed |= fields.SelectEnum(id + "/image-fit", "Image fit", ref imageFit); element.imageFit = imageFit;
        if (imageFit == UiImageFit.NineSlice)
        {
            UiImageSlice slice = element.imageSlice;
            changed |= Number("slice-left", "Left border source pixels", slice.left, value => slice.left = value, 0, 16384);
            changed |= Number("slice-top", "Top border source pixels", slice.top, value => slice.top = value, 0, 16384);
            changed |= Number("slice-right", "Right border source pixels", slice.right, value => slice.right = value, 0, 16384);
            changed |= Number("slice-bottom", "Bottom border source pixels", slice.bottom, value => slice.bottom = value, 0, 16384);
            changed |= Number("slice-scale", "Border pixel scale", slice.scale, value => slice.scale = value, .001, 64);
            bool center = slice.drawCenter; changed |= fields.Toggle(id + "/slice-center", "Draw center region", ref center); slice.drawCenter = center;
            if (element.image is { } image && ImGui.Button("Use one quarter of the image for borders##" + id))
            { fields.Finish(); slice.left = slice.right = image.width / 4f; slice.top = slice.bottom = image.height / 4f; changed = true; }
        }
        UiElementKind kind = element.kind;
        changed |= fields.SelectEnum(id + "/kind", "Control kind", ref kind); element.kind = kind;
        UiLayoutDefinition l = element.layout;
        changed |= Number("x", "Position X", l.x, value => l.x = value);
        changed |= Number("y", "Position Y", l.y, value => l.y = value);
        changed |= Number("w", "Width", l.width, value => l.width = value, 0);
        changed |= Number("h", "Height", l.height, value => l.height = value, 0);
        if (ImGui.CollapsingHeader("Dynamic position and size##" + id))
        {
            bool normalized = element.geometryBinding.normalized;
            changed |= fields.Toggle(id + "/geometry-normalized", "Interpret proportions relative to screen size", ref normalized); element.geometryBinding.normalized = normalized;
            string x = element.geometryBinding.x, y = element.geometryBinding.y, w = element.geometryBinding.width, h = element.geometryBinding.height;
            var geometryChoices = BuiltInUiChoices.Geometry(variables).Concat(BuiltInUiChoices.PageFields(pageVariables, NovelValueKind.Number)).Concat(UiDataSourceChoices.Values(sources, NovelValueKind.Number, row: rowOwner));
            changed |= fields.Select(id + "/geometry-x", "Position X binding", ref x, geometryChoices); element.geometryBinding.x = x;
            changed |= fields.Select(id + "/geometry-y", "Position Y binding", ref y, geometryChoices); element.geometryBinding.y = y;
            changed |= fields.Select(id + "/geometry-width", "Width binding", ref w, geometryChoices); element.geometryBinding.width = w;
            changed |= fields.Select(id + "/geometry-height", "Height binding", ref h, geometryChoices); element.geometryBinding.height = h;
        }
        changed |= Number("min-w", "Minimum width", l.minWidth, value => l.minWidth = value, 0, l.maxWidth);
        changed |= Number("min-h", "Minimum height", l.minHeight, value => l.minHeight = value, 0, l.maxHeight);
        changed |= Number("max-w", "Maximum width", l.maxWidth, value => l.maxWidth = value, l.minWidth, float.MaxValue);
        changed |= Number("max-h", "Maximum height", l.maxHeight, value => l.maxHeight = value, l.minHeight, float.MaxValue);
        UiSizeMode width = l.widthMode, height = l.heightMode;
        changed |= fields.SelectEnum(id + "/width-mode", "Width mode", ref width); l.widthMode = width;
        changed |= fields.SelectEnum(id + "/height-mode", "Height mode", ref height); l.heightMode = height;
        changed |= Number("anchor-x", "Horizontal anchor", l.anchorX, value => l.anchorX = value, 0, 1);
        changed |= Number("anchor-y", "Vertical anchor", l.anchorY, value => l.anchorY = value, 0, 1);
        changed |= Number("pivot-x", "Horizontal pivot", l.pivotX, value => l.pivotX = value, 0, 1);
        changed |= Number("pivot-y", "Vertical pivot", l.pivotY, value => l.pivotY = value, 0, 1);
        changed |= Number("padding", "Padding", l.padding, value => l.padding = value, 0);
        if (ImGui.CollapsingHeader("Margins and automatic layout alignment##" + id))
        {
            changed |= Number("margin-left", "Left margin", l.marginLeft, value => l.marginLeft = value, 0, 100000);
            changed |= Number("margin-top", "Top margin", l.marginTop, value => l.marginTop = value, 0, 100000);
            changed |= Number("margin-right", "Right margin", l.marginRight, value => l.marginRight = value, 0, 100000);
            changed |= Number("margin-bottom", "Bottom margin", l.marginBottom, value => l.marginBottom = value, 0, 100000);
            UiAlignment horizontalAlignment = l.horizontalAlignment, verticalAlignment = l.verticalAlignment;
            changed |= fields.SelectEnum(id + "/horizontal-alignment", "Horizontal slot alignment", ref horizontalAlignment); l.horizontalAlignment = horizontalAlignment;
            changed |= fields.SelectEnum(id + "/vertical-alignment", "Vertical slot alignment", ref verticalAlignment); l.verticalAlignment = verticalAlignment;
            if (kind is UiElementKind.Horizontal or UiElementKind.Vertical)
            {
                UiFlowAlignment distribution = l.flowAlignment;
                changed |= fields.SelectEnum(id + "/flow-alignment", "Main-axis child distribution", ref distribution); l.flowAlignment = distribution;
            }
            EditorUiSession.current.widgets.Hint("Margins participate in measurement, scrolling and hit bounds. Slot alignment applies to horizontal, vertical and grid layouts. Absolute positioning retains anchors and pivots. Fill consumes available space; Stretch still respects minimum and maximum sizes.");
        }
        changed |= Number("gap", "Child gap", l.gap, value => l.gap = value, 0);
        bool clip = l.clip; changed |= fields.Toggle(id + "/clip", "Clip children", ref clip); l.clip = clip;
        if (element.kind == UiElementKind.Grid)
        { double columns = l.columns; changed |= fields.Number(id + "/columns", "Grid columns", ref columns, 1, 100); l.columns = (int)columns; }
        string styleClass = element.styleClass;
        changed |= theme is null ? fields.Text(id + "/style-class", "Theme style class", ref styleClass)
            : fields.Select(id + "/style-class", "Theme style class", ref styleClass, new[] { (string.Empty, "Use screen's own styling") }.Concat(theme.styles.Select(static style => (style.key, style.key)))); element.styleClass = styleClass;
        UiStyleProperty styleChanges = UiStyleValueFields.Draw(fields, id + "/style", element.style, UiStyleProperty.All);
        changed |= new UiAnimationFields(fields).Draw(id, element.animation, BuiltInUiChoices.Conditions(variables)
            .Concat(BuiltInUiChoices.PageFields(pageVariables, NovelValueKind.Boolean)).Concat(UiDataSourceChoices.Values(sources, NovelValueKind.Boolean, row: rowOwner)));
        if (styleChanges != UiStyleProperty.None) { element.styleOverrides |= styleChanges; changed = true; }
        if (element.styleClass.Length > 0 && ImGui.CollapsingHeader("Screen-local overrides##" + id))
        {
            foreach (var property in UiStyleFields.properties)
            {
                bool owned = element.styleOverrides.HasFlag(property);
                if (!fields.Toggle(id + "/override/" + property, UiStyleValueFields.Label(property), ref owned)) continue;
                element.styleOverrides = owned ? element.styleOverrides | property : element.styleOverrides & ~property; changed = true;
            }
        }
        string binding = element.binding, repeat = element.repeatBinding, visible = element.visibleBinding, enabled = element.enabledBinding;
        var valueChoices = element.kind == UiElementKind.Toggle ? BuiltInUiChoices.WritableBooleans(variables)
            : element.kind == UiElementKind.Progress ? BuiltInUiChoices.NumericValues(variables)
            : element.kind == UiElementKind.Slider ? BuiltInUiChoices.Numbers(variables)
            : element.kind == UiElementKind.Input ? BuiltInUiChoices.WritableText(variables)
            : element.kind == UiElementKind.Selector ? BuiltInUiChoices.SelectorValues(variables) : BuiltInUiChoices.Values(variables);
        NovelValueKind? pageKind = element.kind == UiElementKind.Toggle ? NovelValueKind.Boolean
            : element.kind is UiElementKind.Slider or UiElementKind.Progress ? NovelValueKind.Number : element.kind == UiElementKind.Input ? NovelValueKind.String : null;
        var pageChoices = BuiltInUiChoices.PageFields(element.kind == UiElementKind.Selector ? pageVariables.Where(static field => field.initialValue.kind != NovelValueKind.List).ToArray() : pageVariables, pageKind);
        bool writableSource = element.kind is UiElementKind.Toggle or UiElementKind.Input or UiElementKind.Slider or UiElementKind.Selector;
        changed |= fields.Select(id + "/binding", "Value binding", ref binding, valueChoices.Concat(pageChoices).Concat(UiDataSourceChoices.Values(sources, pageKind, writableSource, rowOwner)).DistinctBy(static item => item.id)); element.binding = binding;
        if (element.kind is UiElementKind.Slider or UiElementKind.Progress)
        {
            double minimum = element.minimum, maximum = element.maximum;
            changed |= fields.Number(id + "/minimum", "Minimum value", ref minimum, -1e12, maximum);
            changed |= fields.Number(id + "/maximum", "Maximum value", ref maximum, minimum, 1e12);
            element.minimum = minimum; element.maximum = maximum;
            if (element.kind == UiElementKind.Slider)
            { double step = element.step; changed |= fields.Number(id + "/step", "Step (0 for continuous / automatic)", ref step, 0, 1e12); element.step = step; }
        }
        if (element.kind == UiElementKind.Input)
        {
            double maximum = element.maxLength;
            changed |= fields.Number(id + "/text-limit", "Maximum graphemes", ref maximum, 1, 32768); element.maxLength = (int)maximum;
        }
        if (element.kind == UiElementKind.Selector) changed |= Selector(element, variables, pageVariables, sources, rowOwner);
        changed |= fields.Select(id + "/repeat", "List binding", ref repeat, BuiltInUiChoices.Rows.Concat(BuiltInUiChoices.PageFields(pageVariables, NovelValueKind.List)).Concat(UiDataSourceChoices.Rows(sources))); element.repeatBinding = repeat;
        if (element.kind == UiElementKind.Scroll)
        {
            string follow = element.followEndBinding;
            changed |= fields.Select(id + "/follow-end", "Follow new content at the end", ref follow, BuiltInUiChoices.Conditions(variables).Concat(BuiltInUiChoices.PageFields(pageVariables, NovelValueKind.Boolean)).Concat(UiDataSourceChoices.Values(sources, NovelValueKind.Boolean, row: rowOwner))); element.followEndBinding = follow;
        }
        changed |= fields.Select(id + "/visible", "Visibility binding", ref visible, BuiltInUiChoices.Conditions(variables).Concat(BuiltInUiChoices.PageFields(pageVariables, NovelValueKind.Boolean)).Concat(UiDataSourceChoices.Values(sources, NovelValueKind.Boolean, row: rowOwner))); element.visibleBinding = visible;
        changed |= fields.Select(id + "/enabled", "Enabled binding", ref enabled, BuiltInUiChoices.Conditions(variables).Concat(BuiltInUiChoices.PageFields(pageVariables, NovelValueKind.Boolean)).Concat(UiDataSourceChoices.Values(sources, NovelValueKind.Boolean, row: rowOwner))); element.enabledBinding = enabled;
        string action = element.action, argument = element.argument, argumentBinding = element.argumentBinding;
        if (fields.Select(id + "/action", "Activation action", ref action, BuiltInUiChoices.Actions.Concat(actions.Select(static item => (item.id, "Custom · " + item.title)))))
        {
            changed = true;
            if (actions.SingleOrDefault(item => item.id == action) is { } selectedAction)
            { argument = selectedAction.argument.defaultValue.kind == NovelValueKind.List ? string.Empty : selectedAction.argument.defaultValue.ToString(); argumentBinding = string.Empty; }
        }
        element.action = action;
        var customAction = actions.SingleOrDefault(item => item.id == action);
        if (customAction is not null) changed |= m_actions.Draw(id, customAction, ref argument);
        else changed |= action is "image.zoomIn" or "image.zoomOut" or "image.reset" ? fields.Select(id + "/argument", "Image viewport", ref argument, imageViewports.Select(static viewport => (viewport.id, viewport.id)))
            : action is "page.reset" or "page.toggle" ? fields.Select(id + "/argument", "Destination screen field", ref argument,
            pageVariables.Where(field => action == "page.reset" || field.initialValue.kind == NovelValueKind.Boolean).Select(static field => (field.id, field.name + " · " + field.id)))
            : action == "open" ? fields.Select(id + "/argument", "Target screen", ref argument, BuiltInUiChoices.Pages) : fields.Text(id + "/argument", "Fixed argument", ref argument); element.argument = argument;
        if (action is "page.reset" or "page.toggle") argumentBinding = string.Empty;
        else changed |= fields.Select(id + "/argument-binding", "Argument field (overrides fixed argument)", ref argumentBinding, BuiltInUiChoices.Arguments.Concat(BuiltInUiChoices.Values(variables)).Concat(BuiltInUiChoices.PageFields(pageVariables)).Concat(UiDataSourceChoices.Values(sources, row: rowOwner)).DistinctBy(static option => option.id));
        element.argumentBinding = argumentBinding;
        if (ImGui.CollapsingHeader("Custom fields and actions##" + id))
        {
            if (action == "open") { changed |= fields.Text(id + "/custom-page-target", "Linked custom screen role", ref argument); element.argument = argument; }
            changed |= fields.Text(id + "/custom-value", "Value field path", ref binding); element.binding = binding;
            changed |= fields.Text(id + "/custom-visible", "Boolean visibility field", ref visible); element.visibleBinding = visible;
            changed |= fields.Text(id + "/custom-enabled", "Boolean enabled field", ref enabled); element.enabledBinding = enabled;
            changed |= fields.Text(id + "/custom-image", "Image field", ref imageBinding); element.imageBinding = imageBinding;
            changed |= fields.Text(id + "/custom-repeat", "Collection field", ref repeat); element.repeatBinding = repeat;
            changed |= fields.Text(id + "/custom-action", "Action ID (handled by a custom host)", ref action); element.action = action;
            changed |= fields.Text(id + "/custom-argument", "Argument field", ref argumentBinding); element.argumentBinding = argumentBinding;
        }
        bool interactable = element.interactable, blocks = element.blocksPointer;
        bool behindStage = element.behindStage;
        changed |= fields.Toggle(id + "/behind-stage", "Draw behind stage (decoration)", ref behindStage); element.behindStage = behindStage;
        changed |= fields.Toggle(id + "/interactable", "Interactable", ref interactable); element.interactable = interactable;
        changed |= fields.Toggle(id + "/blocks", "Block pointer pass-through", ref blocks); element.blocksPointer = blocks;
        return changed;

        bool Number(string key, string label, float value, System.Action<float> apply, double minimum = -100000, double maximum = 100000)
        {
            double candidate = value;
            if (!fields.Number(id + "/" + key, label, ref candidate, minimum, maximum)) return false;
            apply((float)candidate); return true;
        }
    }
    private bool Selector(
        UiElementDefinition element,
        VariableDefinition[] variables,
        VariableDefinition[] pageVariables,
        UiDataSourceDefinition[] sources,
        UiDataCollectionDefinition? rowOwner
    ) {
        string binding = element.optionsBinding;
        bool changed = fields.Select(element.id + "/options-binding", "Options source", ref binding, BuiltInUiChoices.SelectorRows.Concat(BuiltInUiChoices.PageFields(pageVariables, NovelValueKind.List)).Concat(UiDataSourceChoices.Rows(sources))); element.optionsBinding = binding;
        if (binding.Length > 0) return changed;
        var variable = variables.SingleOrDefault(item => element.binding == "variables." + item.id)
            ?? pageVariables.SingleOrDefault(item => element.binding == "page." + item.id);
        var sourceField = UiDataBindingValidator.Field(sources, element.binding) ?? rowOwner?.fields.SingleOrDefault(item => element.binding == "item." + item.schema.id);
        if (sourceField is not null) variable = new() { id = element.binding, initialValue = sourceField.schema.defaultValue, members = sourceField.schema.members };
        if (variable?.initialValue.kind == NovelValueKind.Enum && ImGui.Button("Replace with declared enum options##" + element.id))
        { fields.Finish(); UiOptionEditService.FromEnum(element, variable); changed = true; }
        if (ImGui.Button("Add selector option##" + element.id)) { fields.Finish(); UiOptionEditService.Add(element, variable?.initialValue); changed = true; }
        string remove = "";
        foreach (var option in element.options)
        {
            if (!ImGui.CollapsingHeader(option.text + "##" + option.id)) continue;
            string text = option.text; bool enabled = option.enabled; NovelValue value = option.value;
            changed |= fields.Text(option.id + "/text", "Display name", ref text); option.text = text;
            changed |= fields.Toggle(option.id + "/enabled", "Selectable", ref enabled); option.enabled = enabled;
            changed |= m_values.Draw(option.id + "/value", ref value, variable is null, variable?.members); option.value = value;
            if (ImGui.Button("Remove this option##" + option.id)) { fields.Finish(); remove = option.id; changed = true; }
        }
        if (remove.Length > 0) element.options = element.options.Where(option => option.id != remove).ToArray();
        return changed;
    }
}
