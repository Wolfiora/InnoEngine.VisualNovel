using System;
using System.Linq;
using Inno.VisualNovel.Model.Collections;

namespace Inno.VisualNovel.Player.Collections;

internal sealed class GalleryImageController(NovelCollectionService collections)
{
    private string m_entry = string.Empty;
    private string m_variant = string.Empty;
    private long m_revision = -1;
    private GalleryVariant[] m_images = [];

    internal void Select(string id) { m_entry = id; m_variant = string.Empty; m_revision = -1; Refresh(); }
    internal GalleryVariant? current { get { Refresh(); return m_images.FirstOrDefault(image => image.id == m_variant); } }
    internal int count { get { Refresh(); return m_images.Length; } }
    internal int index { get { Refresh(); return Array.FindIndex(m_images, image => image.id == m_variant); } }
    internal void Move(int direction)
    {
        Refresh(); if (m_images.Length == 0) return;
        m_variant = m_images[Math.Clamp(index + Math.Sign(direction), 0, m_images.Length - 1)].id;
    }
    private void Refresh()
    {
        if (m_revision == collections.revision) return;
        m_revision = collections.revision;
        if (m_entry.Length == 0 || !collections.IsUnlocked(m_entry)) { m_images = []; m_variant = string.Empty; return; }
        var entry = collections.Get(m_entry);
        m_images = entry.variants.Length > 0 ? entry.variants.Where(variant => variant.unlockEntryId.Length == 0 || collections.IsUnlocked(variant.unlockEntryId)).ToArray()
            : entry.image is { isMissing: false } ? [new() { id = "cover", title = entry.title, image = entry.image }] : [];
        if (!m_images.Any(image => image.id == m_variant)) m_variant = m_images.FirstOrDefault()?.id ?? string.Empty;
    }
}
