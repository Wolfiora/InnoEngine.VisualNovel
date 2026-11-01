using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Interface.Themes;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.UI.Theming;

/// <summary>Freezes theme inheritance and applies explicit page-local overrides without mutating imported sources.</summary>
public sealed class UiThemeResolver
{
    private readonly ThemeAsset m_theme;
    private readonly Dictionary<string, UiStyleDefinition> m_styles = new(StringComparer.Ordinal);
    /// <summary>Resolves the complete bounded style library before making any result available.</summary>
    /// <param name="theme">The native or unsaved theme declaration.</param>
    public UiThemeResolver(ThemeAsset theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        if (string.IsNullOrWhiteSpace(theme.resourceId) || theme.resourceId.Length > 256 || theme.colors.Length > 512 || theme.metrics.Length > 512 || theme.styles.Length > 512
            || theme.font?.isMissing == true || theme.metrics.Any(static token => !float.IsFinite(token.value)))
            throw new InvalidOperationException("Theme identity, font, metrics or library size is invalid.");
        RequireKeys(theme.colors.Select(static token => token.key).Concat(theme.metrics.Select(static token => token.key)));
        RequireKeys(theme.styles.Select(static style => style.key));
        if (theme.screens.Length > 64 || theme.screens.Select(static binding => binding.role).Distinct(StringComparer.Ordinal).Count() != theme.screens.Length
            || theme.screens.Any(static binding => string.IsNullOrWhiteSpace(binding.role) || binding.screen is not { isMissing: false }))
            throw new InvalidOperationException("Theme screen roles must be unique and reference available native screens.");
        m_theme = ThemeAssetCopy.Copy(theme);
        var colors = m_theme.colors.ToDictionary(static token => token.key, static token => token.value, StringComparer.Ordinal);
        var metrics = m_theme.metrics.ToDictionary(static token => token.key, static token => token.value, StringComparer.Ordinal);
        var declarations = m_theme.styles.ToDictionary(static style => style.key, StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        foreach (string key in declarations.Keys) Resolve(key, 0);

        UiStyleDefinition Resolve(
            string key,
            int depth
        ) {
            if (m_styles.TryGetValue(key, out var cached)) return cached;
            if (depth > 32 || !visiting.Add(key)) throw new InvalidOperationException("Theme style inheritance contains a cycle or exceeds 32 levels: " + key);
            if (!declarations.TryGetValue(key, out var style)) throw new InvalidOperationException("Theme parent style is missing: " + key);
            if ((style.fields & ~UiStyleProperty.All) != 0 || style.bindings.Length > UiStyleFields.properties.Count
                || style.bindings.Select(static binding => binding.property).Distinct().Count() != style.bindings.Length)
                throw new InvalidOperationException("Theme style has unknown fields or repeated token bindings: " + key);
            UiStyleDefinition result = style.parent.Length == 0 ? new() : Resolve(style.parent, depth + 1).Copy();
            UiStyleDefinition own = style.values.Copy();
            foreach (var binding in style.bindings)
            {
                if (binding.property == UiStyleProperty.None || !style.fields.HasFlag(binding.property)) throw new InvalidOperationException("A token must bind one field owned by its style: " + key);
                if (UiStyleFields.IsColor(binding.property) && colors.TryGetValue(binding.token, out uint color)) UiStyleFields.Color(own, binding.property, color);
                else if (UiStyleFields.IsMetric(binding.property) && metrics.TryGetValue(binding.token, out float metric)) UiStyleFields.Metric(own, binding.property, metric);
                else throw new InvalidOperationException("Theme token is missing or has the wrong field type: " + key + "/" + binding.property + " → " + binding.token);
            }
            UiStyleFields.Apply(result, own, style.fields); UiStyleFields.Validate(result); visiting.Remove(key); m_styles.Add(key, result); return result;
        }
    }
    /// <summary>Gets the frozen theme library identity.</summary>
    public string resourceId => m_theme.resourceId;
    /// <summary>Gets an independent resolved style value.</summary>
    /// <param name="key">The exact named style.</param>
    /// <returns>A caller-owned copy; missing styles fail explicitly.</returns>
    public UiStyleDefinition GetStyle(string key) => m_styles.TryGetValue(key, out var style) ? style.Copy() : throw new InvalidOperationException("Theme style is missing: " + resourceId + "/" + key);
    /// <summary>Builds an independently owned page using theme classes and explicit literal overrides.</summary>
    /// <param name="source">The authored screen, kept unmodified.</param>
    /// <returns>A resolved page suitable for the normal UI runtime.</returns>
    public ScreenAsset Apply(ScreenAsset source)
    {
        ScreenAsset screen = UiDefinitionCopy.Copy(source); screen.font = m_theme.font ?? screen.font;
        var pending = new Stack<UiElementDefinition>(); pending.Push(screen.root);
        while (pending.TryPop(out var element))
        {
            if ((element.styleOverrides & ~UiStyleProperty.All) != 0) throw new InvalidOperationException("Page style overrides contain unknown fields: " + element.id);
            if (element.styleClass.Length > 0)
            {
                UiStyleDefinition style = GetStyle(element.styleClass);
                UiStyleFields.Apply(style, element.style, element.styleOverrides); UiStyleFields.Validate(style); element.style = style;
            }
            foreach (var child in element.children) pending.Push(child);
        }
        return screen;
    }
    private static void RequireKeys(IEnumerable<string> source)
    {
        string[] keys = source.ToArray();
        if (keys.Any(static key => string.IsNullOrWhiteSpace(key) || key.Length > 256) || keys.Distinct(StringComparer.Ordinal).Count() != keys.Length)
            throw new InvalidOperationException("Theme keys must be nonempty, bounded and unique.");
    }
}
