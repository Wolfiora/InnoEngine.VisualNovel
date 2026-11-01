using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Rendering;
using InnoEngine.Text;
using InnoEngine.Assets;
using Inno.VisualNovel.Model.Localization;
using Inno.VisualNovel.Stage.Composition;
using Inno.VisualNovel.UI.Text;

namespace Inno.VisualNovel.Player.Localization;

/// <summary>Freezes one language's borrowed image/font policy without modifying authored assets or stage state.</summary>
public sealed class NovelLocalizedResources
{
    private readonly Dictionary<Guid, TextureAsset> m_images;
    private readonly Dictionary<TextureAsset, TextureAsset> m_transientImages;
    private readonly FontAsset[] m_fallbacks;
    private readonly Dictionary<UiRichTextResources, UiRichTextResources> m_text = new();
    /// <summary>Validates bounded explicit mappings and available imported fonts for one language.</summary>
    /// <param name="language">The neutral language declaration.</param>
    public NovelLocalizedResources(LanguageDefinition language)
    {
        if (language.fallbackFonts.Length > 16 || language.images.Length > 1024 || language.font is { isMissing: true }
            || language.fallbackFonts.Any(static font => font is null || font.isMissing) || language.fallbackFonts.Distinct().Count() != language.fallbackFonts.Length
            || language.images.Any(static item => item.source is null || item.source.isMissing || item.image is null || item.image.isMissing))
            throw new InvalidOperationException("Language presentation requires available images, at most 16 distinct fallback fonts and 1024 image mappings.");
        primaryFont = language.font; m_fallbacks = language.fallbackFonts.ToArray();
        m_images = language.images.Where(static item => item.source!.identity.persistentId != Guid.Empty).ToDictionary(static item => item.source!.identity.persistentId, static item => item.image!);
        m_transientImages = language.images.Where(static item => item.source!.identity.persistentId == Guid.Empty).ToDictionary(static item => item.source!, static item => item.image!);
    }
    /// <summary>Gets the optional language-specific default UI font.</summary>
    public FontAsset? primaryFont { get; }
    /// <summary>Gets the frozen ordered language fallback chain.</summary>
    public IReadOnlyList<FontAsset> fallbackFonts => Array.AsReadOnly(m_fallbacks);
    /// <summary>Resolves exactly one explicit language mapping; replacements are never followed as chains.</summary>
    /// <param name="source">The original imported image, or null.</param>
    /// <returns>The selected borrowed image.</returns>
    public TextureAsset? Image(TextureAsset? source) => source is null ? null : source.identity.persistentId == Guid.Empty ? m_transientImages.GetValueOrDefault(source, source) : m_images.GetValueOrDefault(source.identity.persistentId, source);
    /// <summary>Resolves inline images and the language fallback chain once per immutable page resource view.</summary>
    /// <param name="source">The authored named fonts and icons.</param>
    /// <returns>An immutable localized text resource view.</returns>
    public UiRichTextResources Text(UiRichTextResources source)
    {
        if (m_images.Count == 0 && m_transientImages.Count == 0 && primaryFont is null && m_fallbacks.Length == 0) return source;
        if (!m_text.TryGetValue(source, out var result))
        {
            if (m_text.Count >= 128) m_text.Clear();
            var chain = (primaryFont is null ? Array.Empty<FontAsset>() : new[] { primaryFont }).Concat(m_fallbacks).Concat(source.fallbackFonts).Distinct();
            result = new(source.fontBindings, source.iconBindings.Select(item => new Model.Interface.UiTextIcon { key = item.key, image = Image(item.image) }), chain);
            m_text.Add(source, result);
        }
        return result;
    }
    /// <summary>Maps current and frozen previous stage images while retaining authored composition geometry.</summary>
    /// <param name="source">The pure extracted stage, whose transition has no nested transition.</param>
    /// <returns>A detached localized extraction.</returns>
    public StageFrame Stage(StageFrame source) => source with
    {
        items = Array.AsReadOnly(source.items.Select(item => item with { texture = Image(item.texture), mask = Image(item.mask) }).ToArray()),
        transition = source.transition is not { } transition ? null : transition with
        { before = transition.before with { items = Array.AsReadOnly(transition.before.items.Select(item => item with { texture = Image(item.texture), mask = Image(item.mask) }).ToArray()) } }
    };
}
