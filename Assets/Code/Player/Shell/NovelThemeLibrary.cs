using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Interface.Themes;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.UI.Documents;
using Inno.VisualNovel.UI.Theming;

namespace Inno.VisualNovel.Player.Shell;

internal sealed class NovelThemeLibrary
{
    private readonly Dictionary<string, ThemeAsset> m_themes;
    internal NovelThemeLibrary(NovelProjectAsset project)
    {
        if (project.themes.Length > 32 || project.themes.Any(static theme => theme is null || theme.isMissing)
            || project.themes.Select(static theme => theme.resourceId).Distinct(StringComparer.Ordinal).Count() != project.themes.Length)
            throw new InvalidOperationException("Game theme roots must be available, unique and bounded to 32 themes.");
        m_themes = project.themes.ToDictionary(static theme => theme.resourceId, theme =>
        {
            var copy = ThemeAssetCopy.Copy(theme);
            if (copy.screens.Any(binding => !project.screens.Any(page => page.role == binding.role)))
                throw new InvalidOperationException("Theme overrides an undeclared system page: " + copy.resourceId);
            foreach (var binding in copy.screens) binding.screen = UiDefinitionCopy.Copy(binding.screen ?? throw new InvalidOperationException("Theme screen is unassigned."));
            _ = new UiThemeResolver(copy);
            foreach (var binding in NovelThemeScreens.Resolve(project.screens, copy))
            {
                var screen = UiDefinitionCopy.Copy(binding.screen ?? throw new InvalidOperationException("Game screen is unassigned.")); screen.font ??= project.font;
                _ = new UiDocumentInstance(screen, static (element, _) => (0, element.style.fontSize), theme: copy);
            }
            return copy;
        }, StringComparer.Ordinal);
        _ = Get(project.defaultTheme);
        options = Array.AsReadOnly(m_themes.Values.Select(static theme => (id: theme.resourceId, title: theme.title)).ToArray());
    }
    internal IReadOnlyList<(string id, string title)> options { get; }
    internal ThemeAsset? Get(string id) => id.Length == 0 ? null : m_themes.TryGetValue(id, out var theme) ? theme
        : throw new InvalidOperationException("Game theme is unavailable: " + id);
}
