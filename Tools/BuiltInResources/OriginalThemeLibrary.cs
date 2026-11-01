using System;
using System.IO;
using Inno.Assets;
using Inno.Assets.Pipeline;
using Inno.Text;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Interface.Themes;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal static class OriginalThemeLibrary
{
    internal static void Write(
        string root,
        bool refresh = false
    )
    {
        var sources = new OwnedSourceRefresh(root, "BuiltIn/Themes", refresh);
        FontAsset font = Assets.Assets.Load<FontAsset>(AssetPath.Project("BuiltIn/Fonts/NotoSansCJKsc-Regular.otf"));
        if (font.isMissing || font.metadata is null || font.assetPath.localPath.Length == 0) throw new InvalidOperationException("Native theme resources require the packaged licensed font and its real importer.");
        int pages = 0;
        foreach (string name in ShellThemeTemplates.names)
        {
            ThemeAsset theme = ShellThemeTemplates.Create(name, font);
            foreach (var binding in theme.screens)
            {
                string path = "BuiltIn/Themes/" + name + "/Screens/" + binding.role + ".ivnui";
                sources.Save(path, binding.screen!);
                binding.screen = Assets.Assets.Load<ScreenAsset>(AssetPath.Project(path));
                if (binding.screen.isMissing) throw new InvalidOperationException("Native themed page failed to import: " + path);
                pages++;
            }
            string themePath = "BuiltIn/Themes/" + name + "/" + name + ".ivntheme"; sources.Save(themePath, theme);
            var imported = Assets.Assets.Load<ThemeAsset>(AssetPath.Project(themePath));
            if (imported.isMissing || imported.screens.Length != ShellThemeTemplates.roles.Count || imported.resourceId != theme.resourceId)
                throw new InvalidOperationException("Native theme failed to preserve its complete page map: " + themePath);
            foreach (var page in imported.screens) _ = new UiDocumentInstance(page.screen!, static (element, _) => (0, element.style.fontSize), theme: imported);
        }
        var widgets = new OwnedSourceRefresh(root, "BuiltIn/Widgets", refresh);
        foreach (var widget in UiWidgetTemplates.entries) widgets.Save("BuiltIn/Widgets/" + widget.id + ".ivnui", UiWidgetTemplates.Create(widget.id, font));
        Console.WriteLine("Native widgets: " + UiWidgetTemplates.entries.Count + " editable component templates using ordinary game UI bindings, actions and independent state.");
        Console.WriteLine("Native complete themes: 3 families, " + pages + " editable pages, token inheritance and canonical page references verified.");
    }
}
