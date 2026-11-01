using System.Linq;
using Inno.VisualNovel.Authoring.ThemeEditing;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Model.Interface.Themes;
using Inno.VisualNovel.UI.Theming;

namespace Inno.VisualNovel.Editor.ThemeDesigner;

internal sealed class ThemeStyleFields(StoryInspectorFields fields)
{
    internal bool Draw(
        ThemeAsset theme,
        ThemeStyle style
    ) {
        string prefix = "theme/style/" + style.key, parent = style.parent;
        bool changed = fields.Select(prefix + "/parent", fields.Localize("继承样式"), ref parent,
            new[] { (string.Empty, fields.Localize("不继承")) }.Concat(theme.styles.Where(item => item.key != style.key).Select(static item => (item.key, item.key)))); style.parent = parent;
        foreach (var property in UiStyleFields.properties)
        {
            bool owned = style.fields.HasFlag(property);
            if (fields.Toggle(prefix + "/owns/" + property, "定义 · " + UiStyleValueFields.Label(property), ref owned))
            {
                style.fields = owned ? style.fields | property : style.fields & ~property;
                if (!owned) style.bindings = style.bindings.Where(binding => binding.property != property).ToArray(); changed = true;
            }
            if (!owned) continue;
            string token = style.bindings.SingleOrDefault(binding => binding.property == property)?.token ?? string.Empty;
            var choices = UiStyleFields.IsColor(property) ? theme.colors.Select(static value => (value.key, value.key))
                : UiStyleFields.IsMetric(property) ? theme.metrics.Select(static value => (value.key, value.key)) : [];
            if (fields.Select(prefix + "/token/" + property, fields.Localize("值来源"), ref token, new[] { (string.Empty, fields.Localize("本样式的固定值")) }.Concat(choices)))
            { ThemeEditService.Bind(theme, style, property, token); changed = true; }
            if (token.Length == 0) changed |= UiStyleValueFields.DrawField(fields, prefix + "/literal", style.values, property);
        }
        return changed;
    }
}
