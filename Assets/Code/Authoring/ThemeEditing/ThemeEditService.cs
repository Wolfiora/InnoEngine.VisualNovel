using System;
using System.Linq;
using Inno.VisualNovel.Model.Interface.Themes;
using Inno.VisualNovel.UI.Theming;

namespace Inno.VisualNovel.Authoring.ThemeEditing;

/// <summary>Maintains native theme token references and explicit style ownership for visual editors.</summary>
public static class ThemeEditService
{
    /// <summary>Creates a uniquely named token, keeping color and metric identities in one namespace.</summary>
    /// <param name="theme">The detached resource document.</param>
    /// <param name="color">Whether to create a color instead of a numeric metric.</param>
    /// <returns>The new stable theme-local key.</returns>
    public static string AddToken(
        ThemeAsset theme,
        bool color
    ) {
        string key = Unique(theme, color ? "color.new" : "size.new");
        if (color) theme.colors = [.. theme.colors, new() { key = key, value = 0xFFFFFFFF }];
        else theme.metrics = [.. theme.metrics, new() { key = key, value = 28 }];
        return key;
    }
    /// <summary>Renames a token and its style bindings atomically within one resource transaction.</summary>
    /// <param name="theme">The detached resource document.</param>
    /// <param name="key">An existing token key.</param>
    /// <param name="replacement">The new unique key.</param>
    public static void RenameToken(
        ThemeAsset theme,
        string key,
        string replacement
    ) {
        if (key == replacement) return;
        if (string.IsNullOrWhiteSpace(replacement) || replacement.Length > 256 || theme.colors.Any(token => token.key == replacement) || theme.metrics.Any(token => token.key == replacement))
            throw new InvalidOperationException("Token keys must be nonempty, bounded and unique.");
        var color = theme.colors.SingleOrDefault(token => token.key == key); var metric = theme.metrics.SingleOrDefault(token => token.key == key);
        if (color is null && metric is null) throw new InvalidOperationException("The token is missing: " + key);
        if (color is not null) color.key = replacement; else metric!.key = replacement;
        foreach (var binding in theme.styles.SelectMany(static style => style.bindings).Where(binding => binding.token == key)) binding.token = replacement;
    }
    /// <summary>Removes an unused token without silently changing an inherited visual value.</summary>
    /// <param name="theme">The detached resource document.</param>
    /// <param name="key">An existing token key.</param>
    public static void RemoveToken(
        ThemeAsset theme,
        string key
    ) {
        if (theme.styles.Any(style => style.bindings.Any(binding => binding.token == key))) throw new InvalidOperationException("Token is used by a style; replace its binding before removing it: " + key);
        theme.colors = theme.colors.Where(token => token.key != key).ToArray(); theme.metrics = theme.metrics.Where(token => token.key != key).ToArray();
    }
    /// <summary>Creates an independently editable named style.</summary>
    /// <param name="theme">The detached resource document.</param>
    /// <returns>The new unique style key.</returns>
    public static string AddStyle(ThemeAsset theme)
    {
        int index = 1; while (theme.styles.Any(style => style.key == "style." + index)) index++;
        string key = "style." + index; theme.styles = [.. theme.styles, new() { key = key }]; return key;
    }
    /// <summary>Assigns a compatible token to one owned field, or restores literal ownership.</summary>
    /// <param name="theme">The resource defining token types.</param>
    /// <param name="style">The detached style in that document.</param>
    /// <param name="property">One explicitly owned field.</param>
    /// <param name="token">A compatible key, or empty for a literal value.</param>
    public static void Bind(
        ThemeAsset theme,
        ThemeStyle style,
        UiStyleProperty property,
        string token
    ) {
        if (!UiStyleFields.properties.Contains(property) || !style.fields.HasFlag(property)) throw new InvalidOperationException("A token requires one explicitly owned field.");
        if (token.Length > 0 && !(UiStyleFields.IsColor(property) && theme.colors.Any(item => item.key == token)
            || UiStyleFields.IsMetric(property) && theme.metrics.Any(item => item.key == token))) throw new InvalidOperationException("Token type is incompatible with this style field.");
        style.bindings = [.. style.bindings.Where(binding => binding.property != property)];
        if (token.Length > 0) style.bindings = [.. style.bindings, new() { property = property, token = token }];
    }
    private static string Unique(
        ThemeAsset theme,
        string prefix
    ) {
        int index = 1; while (theme.colors.Any(token => token.key == prefix + index) || theme.metrics.Any(token => token.key == prefix + index)) index++;
        return prefix + index;
    }
}
