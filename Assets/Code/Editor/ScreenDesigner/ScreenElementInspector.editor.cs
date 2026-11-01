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
        bool changed = fields.MultilineText(id + "/text", fields.Localize("文本"), ref text); element.text = text;
        bool rich = element.richText;
        changed |= fields.Toggle(id + "/rich-text", "解析文字格式", ref rich); element.richText = rich;
        if (rich) { changed |= m_formatting.Draw(id + "/text", ref text); element.text = text; }
        string textId = element.textId;
        changed |= fields.Text(id + "/text-id", fields.Localize("稳定翻译 ID"), ref textId); element.textId = textId;
        if (ImGui.CollapsingHeader(fields.Caption("悬停与焦点提示##" + id, fields.Localize("悬停与焦点提示##") + id)))
        {
            string help = element.tooltip, helpId = element.tooltipTextId, helpBinding = element.tooltipBinding;
            changed |= fields.MultilineText(id + "/tooltip", fields.Localize("提示说明（纯文本）"), ref help); element.tooltip = help;
            changed |= fields.Text(id + "/tooltip-id", fields.Localize("提示翻译 ID"), ref helpId); element.tooltipTextId = helpId;
            changed |= fields.Select(id + "/tooltip-binding", fields.Localize("提示文本绑定"), ref helpBinding,
                fields.BuiltIn(BuiltInUiChoices.TextValues(variables)).Concat(fields.BuiltIn(BuiltInUiChoices.PageFields(pageVariables, NovelValueKind.String)))
                    .Concat(UiDataSourceChoices.Values(sources, NovelValueKind.String, row: rowOwner))); element.tooltipBinding = helpBinding;
            EditorUiSession.current.widgets.Hint(fields.Localize("指针停留后显示；键盘焦点立即显示。可绑定 item.reason 解释禁用选项，提示本身不接收输入。"));
        }
        string imageBinding = element.imageBinding;
        changed |= fields.Select(id + "/image-binding", fields.Localize("图片绑定"), ref imageBinding, fields.BuiltIn(BuiltInUiChoices.Images).Concat(UiDataSourceChoices.Images(sources))); element.imageBinding = imageBinding;
        UiImageFit imageFit = element.imageFit;
        changed |= fields.SelectEnum(id + "/image-fit", "图片适配", ref imageFit); element.imageFit = imageFit;
        if (imageFit == UiImageFit.NineSlice)
        {
            UiImageSlice slice = element.imageSlice;
            changed |= Number("slice-left", "左边框源像素", slice.left, value => slice.left = value, 0, 16384);
            changed |= Number("slice-top", "上边框源像素", slice.top, value => slice.top = value, 0, 16384);
            changed |= Number("slice-right", "右边框源像素", slice.right, value => slice.right = value, 0, 16384);
            changed |= Number("slice-bottom", "下边框源像素", slice.bottom, value => slice.bottom = value, 0, 16384);
            changed |= Number("slice-scale", "边框像素缩放", slice.scale, value => slice.scale = value, .001, 64);
            bool center = slice.drawCenter; changed |= fields.Toggle(id + "/slice-center", "绘制中间区域", ref center); slice.drawCenter = center;
            if (element.image is { } image && ImGui.Button(fields.Caption("使用图像四分之一作为边框##" + id, fields.Localize("使用图像四分之一作为边框##") + id)))
            { fields.Finish(); slice.left = slice.right = image.width / 4f; slice.top = slice.bottom = image.height / 4f; changed = true; }
        }
        UiElementKind kind = element.kind;
        changed |= fields.SelectEnum(id + "/kind", "控件类型", ref kind); element.kind = kind;
        UiLayoutDefinition l = element.layout;
        changed |= Number("x", "横向位置", l.x, value => l.x = value);
        changed |= Number("y", "纵向位置", l.y, value => l.y = value);
        changed |= Number("w", "宽度", l.width, value => l.width = value, 0);
        changed |= Number("h", "高度", l.height, value => l.height = value, 0);
        if (ImGui.CollapsingHeader(fields.Caption("动态位置与尺寸##" + id, fields.Localize("动态位置与尺寸##") + id)))
        {
            bool normalized = element.geometryBinding.normalized;
            changed |= fields.Toggle(id + "/geometry-normalized", "按页面尺寸解释比例", ref normalized); element.geometryBinding.normalized = normalized;
            string x = element.geometryBinding.x, y = element.geometryBinding.y, w = element.geometryBinding.width, h = element.geometryBinding.height;
            var geometryChoices = fields.BuiltIn(BuiltInUiChoices.Geometry(variables)).Concat(fields.BuiltIn(BuiltInUiChoices.PageFields(pageVariables, NovelValueKind.Number))).Concat(UiDataSourceChoices.Values(sources, NovelValueKind.Number, row: rowOwner));
            changed |= fields.Select(id + "/geometry-x", fields.Localize("横向位置绑定"), ref x, geometryChoices); element.geometryBinding.x = x;
            changed |= fields.Select(id + "/geometry-y", fields.Localize("纵向位置绑定"), ref y, geometryChoices); element.geometryBinding.y = y;
            changed |= fields.Select(id + "/geometry-width", fields.Localize("宽度绑定"), ref w, geometryChoices); element.geometryBinding.width = w;
            changed |= fields.Select(id + "/geometry-height", fields.Localize("高度绑定"), ref h, geometryChoices); element.geometryBinding.height = h;
        }
        changed |= Number("min-w", "最小宽度", l.minWidth, value => l.minWidth = value, 0, l.maxWidth);
        changed |= Number("min-h", "最小高度", l.minHeight, value => l.minHeight = value, 0, l.maxHeight);
        changed |= Number("max-w", "最大宽度", l.maxWidth, value => l.maxWidth = value, l.minWidth, float.MaxValue);
        changed |= Number("max-h", "最大高度", l.maxHeight, value => l.maxHeight = value, l.minHeight, float.MaxValue);
        UiSizeMode width = l.widthMode, height = l.heightMode;
        changed |= fields.SelectEnum(id + "/width-mode", "水平尺寸", ref width); l.widthMode = width;
        changed |= fields.SelectEnum(id + "/height-mode", "垂直尺寸", ref height); l.heightMode = height;
        changed |= Number("anchor-x", "水平锚点", l.anchorX, value => l.anchorX = value, 0, 1);
        changed |= Number("anchor-y", "垂直锚点", l.anchorY, value => l.anchorY = value, 0, 1);
        changed |= Number("pivot-x", "水平枢轴", l.pivotX, value => l.pivotX = value, 0, 1);
        changed |= Number("pivot-y", "垂直枢轴", l.pivotY, value => l.pivotY = value, 0, 1);
        changed |= Number("padding", "内边距", l.padding, value => l.padding = value, 0);
        if (ImGui.CollapsingHeader(fields.Caption("外边距与自动布局对齐##" + id, fields.Localize("外边距与自动布局对齐##") + id)))
        {
            changed |= Number("margin-left", "左外边距", l.marginLeft, value => l.marginLeft = value, 0, 100000);
            changed |= Number("margin-top", "上外边距", l.marginTop, value => l.marginTop = value, 0, 100000);
            changed |= Number("margin-right", "右外边距", l.marginRight, value => l.marginRight = value, 0, 100000);
            changed |= Number("margin-bottom", "下外边距", l.marginBottom, value => l.marginBottom = value, 0, 100000);
            UiAlignment horizontalAlignment = l.horizontalAlignment, verticalAlignment = l.verticalAlignment;
            changed |= fields.SelectEnum(id + "/horizontal-alignment", "槽内水平对齐", ref horizontalAlignment); l.horizontalAlignment = horizontalAlignment;
            changed |= fields.SelectEnum(id + "/vertical-alignment", "槽内垂直对齐", ref verticalAlignment); l.verticalAlignment = verticalAlignment;
            if (kind is UiElementKind.Horizontal or UiElementKind.Vertical)
            {
                UiFlowAlignment distribution = l.flowAlignment;
                changed |= fields.SelectEnum(id + "/flow-alignment", "子项主轴分布", ref distribution); l.flowAlignment = distribution;
            }
            EditorUiSession.current.widgets.Hint(fields.Localize("外边距参与测量、滚动和点击边界。槽内对齐用于横排、竖排和网格；绝对定位继续使用锚点/枢轴。Fill 消耗可用空间，Stretch 仍受最小/最大尺寸限制。"));
        }
        changed |= Number("gap", "子项间距", l.gap, value => l.gap = value, 0);
        bool clip = l.clip; changed |= fields.Toggle(id + "/clip", "裁切子项", ref clip); l.clip = clip;
        if (element.kind == UiElementKind.Grid)
        { double columns = l.columns; changed |= fields.Number(id + "/columns", fields.Localize("网格列数"), ref columns, 1, 100); l.columns = (int)columns; }
        string styleClass = element.styleClass;
        changed |= theme is null ? fields.Text(id + "/style-class", fields.Localize("主题样式类"), ref styleClass)
            : fields.Select(id + "/style-class", fields.Localize("主题样式类"), ref styleClass, new[] { (string.Empty, fields.Localize("使用页面自身样式")) }.Concat(theme.styles.Select(static style => (style.key, style.key)))); element.styleClass = styleClass;
        UiStyleProperty styleChanges = UiStyleValueFields.Draw(fields, id + "/style", element.style, UiStyleProperty.All);
        changed |= new UiAnimationFields(fields).Draw(id, element.animation, fields.BuiltIn(BuiltInUiChoices.Conditions(variables))
            .Concat(fields.BuiltIn(BuiltInUiChoices.PageFields(pageVariables, NovelValueKind.Boolean))).Concat(UiDataSourceChoices.Values(sources, NovelValueKind.Boolean, row: rowOwner)));
        if (styleChanges != UiStyleProperty.None) { element.styleOverrides |= styleChanges; changed = true; }
        if (element.styleClass.Length > 0 && ImGui.CollapsingHeader(fields.Caption("页面局部覆盖##" + id, fields.Localize("页面局部覆盖##") + id)))
        {
            foreach (var property in UiStyleFields.properties)
            {
                bool owned = element.styleOverrides.HasFlag(property);
                if (!fields.Toggle(id + "/override/" + property, UiStyleValueFields.Label(property), ref owned)) continue;
                element.styleOverrides = owned ? element.styleOverrides | property : element.styleOverrides & ~property; changed = true;
            }
        }
        string binding = element.binding, repeat = element.repeatBinding, visible = element.visibleBinding, enabled = element.enabledBinding;
        var valueChoices = element.kind == UiElementKind.Toggle ? fields.BuiltIn(BuiltInUiChoices.WritableBooleans(variables))
            : element.kind == UiElementKind.Progress ? fields.BuiltIn(BuiltInUiChoices.NumericValues(variables))
            : element.kind == UiElementKind.Slider ? fields.BuiltIn(BuiltInUiChoices.Numbers(variables))
            : element.kind == UiElementKind.Input ? fields.BuiltIn(BuiltInUiChoices.WritableText(variables))
            : element.kind == UiElementKind.Selector ? fields.BuiltIn(BuiltInUiChoices.SelectorValues(variables)) : fields.BuiltIn(BuiltInUiChoices.Values(variables));
        NovelValueKind? pageKind = element.kind == UiElementKind.Toggle ? NovelValueKind.Boolean
            : element.kind is UiElementKind.Slider or UiElementKind.Progress ? NovelValueKind.Number : element.kind == UiElementKind.Input ? NovelValueKind.String : null;
        var pageChoices = fields.BuiltIn(BuiltInUiChoices.PageFields(element.kind == UiElementKind.Selector ? pageVariables.Where(static field => field.initialValue.kind != NovelValueKind.List).ToArray() : pageVariables, pageKind));
        bool writableSource = element.kind is UiElementKind.Toggle or UiElementKind.Input or UiElementKind.Slider or UiElementKind.Selector;
        changed |= fields.Select(id + "/binding", fields.Localize("值绑定"), ref binding, valueChoices.Concat(pageChoices).Concat(UiDataSourceChoices.Values(sources, pageKind, writableSource, rowOwner)).DistinctBy(static item => item.id)); element.binding = binding;
        if (element.kind is UiElementKind.Slider or UiElementKind.Progress)
        {
            double minimum = element.minimum, maximum = element.maximum;
            changed |= fields.Number(id + "/minimum", fields.Localize("数值下限"), ref minimum, -1e12, maximum);
            changed |= fields.Number(id + "/maximum", fields.Localize("数值上限"), ref maximum, minimum, 1e12);
            element.minimum = minimum; element.maximum = maximum;
            if (element.kind == UiElementKind.Slider)
            { double step = element.step; changed |= fields.Number(id + "/step", fields.Localize("步长（0 为连续 / 自动）"), ref step, 0, 1e12); element.step = step; }
        }
        if (element.kind == UiElementKind.Input)
        {
            double maximum = element.maxLength;
            changed |= fields.Number(id + "/text-limit", fields.Localize("最大字素数"), ref maximum, 1, 32768); element.maxLength = (int)maximum;
        }
        if (element.kind == UiElementKind.Selector) changed |= Selector(element, variables, pageVariables, sources, rowOwner);
        changed |= fields.Select(id + "/repeat", fields.Localize("列表绑定"), ref repeat, fields.BuiltIn(BuiltInUiChoices.Rows).Concat(fields.BuiltIn(BuiltInUiChoices.PageFields(pageVariables, NovelValueKind.List))).Concat(UiDataSourceChoices.Rows(sources))); element.repeatBinding = repeat;
        if (element.kind == UiElementKind.Scroll)
        {
            string follow = element.followEndBinding;
            changed |= fields.Select(id + "/follow-end", fields.Localize("新增内容跟随末尾"), ref follow, fields.BuiltIn(BuiltInUiChoices.Conditions(variables)).Concat(fields.BuiltIn(BuiltInUiChoices.PageFields(pageVariables, NovelValueKind.Boolean))).Concat(UiDataSourceChoices.Values(sources, NovelValueKind.Boolean, row: rowOwner))); element.followEndBinding = follow;
        }
        changed |= fields.Select(id + "/visible", fields.Localize("显隐绑定"), ref visible, fields.BuiltIn(BuiltInUiChoices.Conditions(variables)).Concat(fields.BuiltIn(BuiltInUiChoices.PageFields(pageVariables, NovelValueKind.Boolean))).Concat(UiDataSourceChoices.Values(sources, NovelValueKind.Boolean, row: rowOwner))); element.visibleBinding = visible;
        changed |= fields.Select(id + "/enabled", fields.Localize("可用绑定"), ref enabled, fields.BuiltIn(BuiltInUiChoices.Conditions(variables)).Concat(fields.BuiltIn(BuiltInUiChoices.PageFields(pageVariables, NovelValueKind.Boolean))).Concat(UiDataSourceChoices.Values(sources, NovelValueKind.Boolean, row: rowOwner))); element.enabledBinding = enabled;
        string action = element.action, argument = element.argument, argumentBinding = element.argumentBinding;
        if (fields.Select(id + "/action", fields.Localize("点击动作"), ref action, fields.BuiltIn(BuiltInUiChoices.Actions).Concat(actions.Select(static item => (item.id, "自定义 · " + item.title)))))
        {
            changed = true;
            if (actions.SingleOrDefault(item => item.id == action) is { } selectedAction)
            { argument = selectedAction.argument.defaultValue.kind == NovelValueKind.List ? string.Empty : selectedAction.argument.defaultValue.ToString(); argumentBinding = string.Empty; }
        }
        element.action = action;
        var customAction = actions.SingleOrDefault(item => item.id == action);
        if (customAction is not null) changed |= m_actions.Draw(id, customAction, ref argument);
        else changed |= action is "image.zoomIn" or "image.zoomOut" or "image.reset" ? fields.Select(id + "/argument", fields.Localize("图片视口"), ref argument, imageViewports.Select(static viewport => (viewport.id, viewport.id)))
            : action is "page.reset" or "page.toggle" ? fields.Select(id + "/argument", fields.Localize("目标页面字段"), ref argument,
            pageVariables.Where(field => action == "page.reset" || field.initialValue.kind == NovelValueKind.Boolean).Select(static field => (field.id, field.name + " · " + field.id)))
            : action == "open" ? fields.Select(id + "/argument", fields.Localize("目标页面"), ref argument, fields.BuiltIn(BuiltInUiChoices.Pages)) : fields.Text(id + "/argument", fields.Localize("固定参数"), ref argument); element.argument = argument;
        if (action is "page.reset" or "page.toggle") argumentBinding = string.Empty;
        else changed |= fields.Select(id + "/argument-binding", fields.Localize("参数字段（优先于固定参数）"), ref argumentBinding, fields.BuiltIn(BuiltInUiChoices.Arguments).Concat(fields.BuiltIn(BuiltInUiChoices.Values(variables))).Concat(fields.BuiltIn(BuiltInUiChoices.PageFields(pageVariables))).Concat(UiDataSourceChoices.Values(sources, row: rowOwner)).DistinctBy(static option => option.id));
        element.argumentBinding = argumentBinding;
        if (ImGui.CollapsingHeader(fields.Caption("自定义字段与动作##" + id, fields.Localize("自定义字段与动作##") + id)))
        {
            if (action == "open") { changed |= fields.Text(id + "/custom-page-target", fields.Localize("已关联的自定义页面角色"), ref argument); element.argument = argument; }
            changed |= fields.Text(id + "/custom-value", fields.Localize("值字段路径"), ref binding); element.binding = binding;
            changed |= fields.Text(id + "/custom-visible", fields.Localize("Boolean 显隐字段"), ref visible); element.visibleBinding = visible;
            changed |= fields.Text(id + "/custom-enabled", fields.Localize("Boolean 可用字段"), ref enabled); element.enabledBinding = enabled;
            changed |= fields.Text(id + "/custom-image", fields.Localize("图片字段"), ref imageBinding); element.imageBinding = imageBinding;
            changed |= fields.Text(id + "/custom-repeat", fields.Localize("集合字段"), ref repeat); element.repeatBinding = repeat;
            changed |= fields.Text(id + "/custom-action", fields.Localize("动作 ID（自定义宿主需处理）"), ref action); element.action = action;
            changed |= fields.Text(id + "/custom-argument", fields.Localize("参数字段"), ref argumentBinding); element.argumentBinding = argumentBinding;
        }
        bool interactable = element.interactable, blocks = element.blocksPointer;
        bool behindStage = element.behindStage;
        changed |= fields.Toggle(id + "/behind-stage", "绘制在舞台后方（装饰）", ref behindStage); element.behindStage = behindStage;
        changed |= fields.Toggle(id + "/interactable", "可交互", ref interactable); element.interactable = interactable;
        changed |= fields.Toggle(id + "/blocks", "阻止点击穿透", ref blocks); element.blocksPointer = blocks;
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
        bool changed = fields.Select(element.id + "/options-binding", fields.Localize("选项来源"), ref binding, fields.BuiltIn(BuiltInUiChoices.SelectorRows).Concat(fields.BuiltIn(BuiltInUiChoices.PageFields(pageVariables, NovelValueKind.List))).Concat(UiDataSourceChoices.Rows(sources))); element.optionsBinding = binding;
        if (binding.Length > 0) return changed;
        var variable = variables.SingleOrDefault(item => element.binding == "variables." + item.id)
            ?? pageVariables.SingleOrDefault(item => element.binding == "page." + item.id);
        var sourceField = UiDataBindingValidator.Field(sources, element.binding) ?? rowOwner?.fields.SingleOrDefault(item => element.binding == "item." + item.schema.id);
        if (sourceField is not null) variable = new() { id = element.binding, initialValue = sourceField.schema.defaultValue, members = sourceField.schema.members };
        if (variable?.initialValue.kind == NovelValueKind.Enum && ImGui.Button(fields.Caption("替换为枚举声明选项##" + element.id, fields.Localize("替换为枚举声明选项##") + element.id)))
        { fields.Finish(); UiOptionEditService.FromEnum(element, variable); changed = true; }
        if (ImGui.Button(fields.Caption("添加选择器选项##" + element.id, fields.Localize("添加选择器选项##") + element.id))) { fields.Finish(); UiOptionEditService.Add(element, variable?.initialValue); changed = true; }
        string remove = "";
        foreach (var option in element.options)
        {
            if (!ImGui.CollapsingHeader(option.text + "##" + option.id)) continue;
            string text = option.text; bool enabled = option.enabled; NovelValue value = option.value;
            changed |= fields.Text(option.id + "/text", fields.Localize("显示名"), ref text); option.text = text;
            changed |= fields.Toggle(option.id + "/enabled", "允许选择", ref enabled); option.enabled = enabled;
            changed |= m_values.Draw(option.id + "/value", ref value, variable is null, variable?.members); option.value = value;
            if (ImGui.Button(fields.Caption("删除此选项##" + option.id, fields.Localize("删除此选项##") + option.id))) { fields.Finish(); remove = option.id; changed = true; }
        }
        if (remove.Length > 0) element.options = element.options.Where(option => option.id != remove).ToArray();
        return changed;
    }
}
