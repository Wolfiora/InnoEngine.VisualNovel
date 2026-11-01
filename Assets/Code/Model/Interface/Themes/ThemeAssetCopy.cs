using System.Linq;
using Inno.VisualNovel.Model.Project;

namespace Inno.VisualNovel.Model.Interface.Themes;

/// <summary>Freezes theme value declarations while borrowing canonical page and font identities.</summary>
public static class ThemeAssetCopy
{
    /// <summary>Copies token, inheritance, literal and binding data without mutable aliases.</summary>
    /// <param name="source">The canonical or detached native theme.</param>
    /// <returns>An independently editable theme declaration; pages remain imported assets.</returns>
    public static ThemeAsset Copy(ThemeAsset source) => new()
    {
        resourceId = source.resourceId, title = source.title, description = source.description, font = source.font,
        colors = source.colors.Select(static token => new ThemeColorToken { key = token.key, value = token.value }).ToArray(),
        metrics = source.metrics.Select(static token => new ThemeMetricToken { key = token.key, value = token.value }).ToArray(),
        styles = source.styles.Select(static style => new ThemeStyle { key = style.key, parent = style.parent, fields = style.fields, values = style.values.Copy(),
            bindings = style.bindings.Select(static binding => new ThemeStyleBinding { property = binding.property, token = binding.token }).ToArray() }).ToArray(),
        screens = source.screens.Select(static binding => new NovelScreenBinding { role = binding.role, screen = binding.screen }).ToArray()
    };
}
