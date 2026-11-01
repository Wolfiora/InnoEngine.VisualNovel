using System;
using System.Linq;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Text;
using Inno.VisualNovel.UI.Text;

namespace Inno.VisualNovel.Authoring.UiEditing;

/// <summary>Resolves authored text resource keys before a screen or game is published.</summary>
public static class ScreenTextValidator
{
    /// <summary>Validates literal rich controls against their page-owned native references.</summary>
    /// <param name="screen">The native page declaration.</param>
    public static void Validate(ScreenAsset screen)
    {
        foreach (var element in ScreenEditService.Flatten(screen).Select(static row => row.element).Where(static item => item.richText))
            ValidateText(screen, element.text, screen.title + "/" + element.id);
    }
    /// <summary>Validates a story line or translation against the page used to paint it.</summary>
    /// <param name="screen">The owner of named font and image references.</param>
    /// <param name="text">Authored rich markup before literal variable interpolation.</param>
    /// <param name="location">The stable diagnostic location.</param>
    public static void ValidateText(
        ScreenAsset screen,
        string text,
        string location
    ) {
        try
        {
            var resources = new UiRichTextResources(screen.textFonts, screen.textIcons);
            foreach (NovelTextRun run in NovelTextMarkup.Parse(text).runs)
            {
                if (run.style.font.Length > 0) _ = resources.Font(run.style.font);
                if (run.icon.Length > 0) _ = resources.Icon(run.icon);
            }
        }
        catch (Exception failure) when (failure is ArgumentException or InvalidOperationException or FormatException)
        { throw new InvalidOperationException(location + ": " + failure.Message, failure); }
    }
}
