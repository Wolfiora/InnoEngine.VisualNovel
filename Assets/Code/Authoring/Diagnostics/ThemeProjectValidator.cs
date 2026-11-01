using System;
using System.Linq;
using Inno.VisualNovel.Authoring.UiEditing;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.UI.Documents;
using Inno.VisualNovel.UI.Theming;

namespace Inno.VisualNovel.Authoring.Diagnostics;

/// <summary>Validates every selectable theme against the game's actual story and editable role map.</summary>
public static class ThemeProjectValidator
{
    /// <summary>Rejects missing styles, invalid role overrides and narrative bindings in any theme variant.</summary>
    /// <param name="project">The complete candidate content definition.</param>
    public static void Validate(NovelProjectAsset project)
    {
        if (project.themes.Length > 32 || project.themes.Any(static theme => theme is null || theme.isMissing)
            || project.themes.Select(static theme => theme.resourceId).Distinct(StringComparer.Ordinal).Count() != project.themes.Length
            || project.defaultTheme.Length > 0 && !project.themes.Any(theme => theme.resourceId == project.defaultTheme))
            throw new InvalidOperationException("Game themes must be unique, available, bounded to 32 and contain the selected default.");
        foreach (var theme in project.themes)
        {
            var resolver = new UiThemeResolver(theme);
            if (theme.screens.Any(binding => !project.screens.Any(page => page.role == binding.role)))
                throw new InvalidOperationException("Theme overrides an undeclared system page: " + theme.resourceId);
            var variant = NovelProjectCopy.Copy(project);
            variant.screens = NovelThemeScreens.Resolve(project.screens, theme).Select(binding =>
            {
                var screen = resolver.Apply(binding.screen ?? throw new InvalidOperationException("A theme page is unassigned."));
                screen.font ??= project.font;
                _ = new UiDocumentInstance(screen, static (element, _) => (0, element.style.fontSize));
                return new NovelScreenBinding { role = binding.role, screen = screen };
            }).ToArray();
            UiSelectorValidator.Validate(variant); PresentationValidator.Validate(variant);
        }
    }
}
