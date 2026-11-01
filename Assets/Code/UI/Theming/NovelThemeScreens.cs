using System;
using System.Linq;
using Inno.VisualNovel.Model.Interface.Themes;
using Inno.VisualNovel.Model.Project;

namespace Inno.VisualNovel.UI.Theming;

/// <summary>Composes explicit theme page overrides over the game's ordinary screen role map.</summary>
public static class NovelThemeScreens
{
    /// <summary>Resolves role overrides without changing either source declaration or page identity.</summary>
    /// <param name="defaults">The game's fallback page roots.</param>
    /// <param name="theme">An optional theme with partial or complete role overrides.</param>
    /// <returns>An independently owned binding array, borrowing imported page references.</returns>
    public static NovelScreenBinding[] Resolve(
        NovelScreenBinding[] defaults,
        ThemeAsset? theme
    ) {
        var pages = defaults.ToDictionary(static binding => binding.role, StringComparer.Ordinal);
        foreach (var binding in theme?.screens ?? []) pages[binding.role] = binding;
        return pages.Values.Select(static binding => new NovelScreenBinding { role = binding.role, screen = binding.screen }).ToArray();
    }
}
