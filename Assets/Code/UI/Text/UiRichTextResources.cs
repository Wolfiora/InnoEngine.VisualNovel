using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Text;
using InnoEngine.Rendering;
using Inno.VisualNovel.Model.Interface;

namespace Inno.VisualNovel.UI.Text;

/// <summary>Freezes the borrowed font and image bindings used by one page generation.</summary>
public sealed class UiRichTextResources
{
    private readonly IReadOnlyDictionary<string, FontAsset?> m_fonts;
    private readonly IReadOnlyDictionary<string, TextureAsset?> m_icons;
    private readonly FontAsset[] m_fallbacks;
    /// <summary>Freezes unique resource keys without owning the imported resources.</summary>
    /// <param name="fonts">Page-declared font bindings.</param>
    /// <param name="icons">Page-declared image bindings.</param>
    /// <param name="fallbackFonts">Optional ordered imported fonts used for missing native glyphs.</param>
    public UiRichTextResources(
        IEnumerable<UiTextFont> fonts,
        IEnumerable<UiTextIcon> icons,
        IEnumerable<FontAsset>? fallbackFonts = null
    ) {
        UiTextFont[] fontItems = fonts.ToArray(); UiTextIcon[] iconItems = icons.ToArray();
        if (fontItems.Length > 128 || iconItems.Length > 256 || fontItems.Any(static item => !Inno.VisualNovel.Model.Text.NovelTextMarkup.IsResourceKey(item.key)) || iconItems.Any(static item => !Inno.VisualNovel.Model.Text.NovelTextMarkup.IsResourceKey(item.key)))
            throw new ArgumentException("Text resources require nonempty unique keys within the page budget.");
        m_fonts = fontItems.ToDictionary(static item => item.key, static item => item.font, StringComparer.Ordinal);
        m_icons = iconItems.ToDictionary(static item => item.key, static item => item.image, StringComparer.Ordinal);
        m_fallbacks = (fallbackFonts ?? []).ToArray();
        if (m_fallbacks.Length > 32 || m_fallbacks.Any(static font => font is null || font.isMissing) || m_fallbacks.Distinct().Count() != m_fallbacks.Length)
            throw new ArgumentException("Text fallback fonts must be available, distinct and bounded to 32 entries.");
    }
    /// <summary>Gets the immutable imported fallback chain for this resolved page view.</summary>
    public IReadOnlyList<FontAsset> fallbackFonts => Array.AsReadOnly(m_fallbacks);
    /// <summary>Gets an empty immutable resource view.</summary>
    public static UiRichTextResources empty { get; } = new([], []);
    /// <summary>Gets detached declarations for native image snapshots.</summary>
    public IEnumerable<UiTextFont> fontBindings => m_fonts.Select(static item => new UiTextFont { key = item.Key, font = item.Value });
    /// <summary>Gets detached inline-image declarations for native image snapshots.</summary>
    public IEnumerable<UiTextIcon> iconBindings => m_icons.Select(static item => new UiTextIcon { key = item.Key, image = item.Value });
    /// <summary>Resolves a declared alternate font, preserving missing references as an error.</summary>
    /// <param name="key">The authored font key.</param>
    /// <returns>The imported font.</returns>
    public FontAsset Font(string key) => m_fonts.GetValueOrDefault(key) is { isMissing: false } font ? font : throw new InvalidOperationException("Missing rich-text font: " + key);
    /// <summary>Resolves a declared inline image, preserving missing references as an error.</summary>
    /// <param name="key">The authored image key.</param>
    /// <returns>The imported image.</returns>
    public TextureAsset Icon(string key) => m_icons.GetValueOrDefault(key) is { isMissing: false } image ? image : throw new InvalidOperationException("Missing inline text image: " + key);
}
