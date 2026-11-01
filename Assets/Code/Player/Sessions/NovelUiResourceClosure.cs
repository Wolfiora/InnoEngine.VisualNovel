using System;
using System.Collections.Generic;
using InnoEngine.Text;
using InnoEngine.Assets;
using InnoEngine.Rendering;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.Player.Sessions;

internal sealed class NovelUiResourceClosure
{
    private readonly Dictionary<string, FontAsset> m_fonts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TextureAsset> m_images = new(StringComparer.Ordinal);
    internal NovelUiResourceClosure(NovelProjectAsset project)
    {
        AddFont(project.font);
        foreach (var language in project.localization?.languages ?? [])
        {
            AddFont(language.font); foreach (var font in language.fallbackFonts) AddFont(font);
            foreach (var mapping in language.images) { AddImage(mapping.source); AddImage(mapping.image); }
        }
        foreach (var resource in project.additionalResources) { if (resource is FontAsset font) AddFont(font); if (resource is TextureAsset image) AddImage(image); }
        AddScreens(project.screens);
        foreach (var theme in project.themes) { AddFont(theme.font); AddScreens(theme.screens); }
        foreach (var entry in project.collections?.entries ?? []) { AddImage(entry.image); foreach (var variant in entry.variants) AddImage(variant.image); }
        foreach (var stage in project.stages) foreach (var item in stage.textures) AddImage(item);
        foreach (var character in project.characters) { AddImage(character.portrait); foreach (var item in character.textures) AddImage(item); }
    }
    private void AddScreens(NovelScreenBinding[] bindings)
    {
        foreach (var binding in bindings)
        {
            if (binding.screen is not { } screen) continue;
            AddFont(screen.font); Walk(screen.root);
            foreach (var item in screen.textFonts) AddFont(item.font);
            foreach (var item in screen.textIcons) AddImage(item.image);
        }
    }
    internal UiFrame Restore(UiFrameSnapshot snapshot) => snapshot.Restore(
        id => m_fonts.TryGetValue(id, out var font) && !font.isMissing ? font : throw new InvalidOperationException("Transition font is absent from the game closure: " + id),
        id => m_images.TryGetValue(id, out var image) && !image.isMissing ? image : throw new InvalidOperationException("Transition image is absent from the game closure: " + id));
    private void Walk(UiElementDefinition element)
    {
        AddFont(element.font); AddImage(element.image);
        foreach (var child in element.children) Walk(child);
    }
    private void AddFont(FontAsset? font) { if (font is not null && font.identity.persistentId != Guid.Empty) m_fonts[font.identity.persistentId.ToString("D")] = font; }
    private void AddImage(TextureAsset? image) { if (image is not null && image.identity.persistentId != Guid.Empty) m_images[image.identity.persistentId.ToString("D")] = image; }
}
