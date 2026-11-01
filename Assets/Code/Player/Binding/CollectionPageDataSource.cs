using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InnoEngine.Rendering;
using Inno.VisualNovel.Model.Collections;
using Inno.VisualNovel.Model.Localization;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Player.Collections;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.UI.Binding;

namespace Inno.VisualNovel.Player.Binding;

internal sealed class CollectionPageDataSource(
    NovelSession session,
    MusicGalleryController music
) : UiDataSource
{
    private const int pageSize = 12;
    private int m_page;
    private string m_search = string.Empty;
    private readonly GalleryImageController m_images = new(session.collections);
    internal string role { get; private set; } = "gallery";
    internal string selected { get; private set; } = string.Empty;
    internal void Open(string value) { role = value; m_page = 0; m_search = string.Empty; }
    internal void Select(string id)
    {
        if (!session.collections.IsUnlocked(id)) throw new InvalidOperationException("This collection item is locked.");
        _ = session.collections.Get(id); selected = id; m_images.Select(id);
    }
    internal void MoveImage(int direction) => m_images.Move(direction);
    internal void Move(int direction) => m_page = Math.Clamp(m_page + direction, 0, Pages() - 1);
    /// <inheritdoc />
    public override bool TryRead(
        string path,
        out NovelValue value
    ) {
        var detail = selected.Length > 0 && session.collections.IsUnlocked(selected) ? session.collections.Get(selected) : null;
        value = path switch
        {
            "collections.search" => NovelValue.String(m_search),
            "collections.empty" => NovelValue.Boolean(Items().Length == 0),
            "collections.page" => NovelValue.String((m_page + 1).ToString(CultureInfo.InvariantCulture) + " / " + Pages().ToString(CultureInfo.InvariantCulture)),
            "collections.previous" => NovelValue.Boolean(m_page > 0),
            "collections.next" => NovelValue.Boolean(m_page + 1 < Pages()),
            "collection.title" => NovelValue.String(detail is null ? "" : Text(detail, "title", detail.title)),
            "collection.description" => NovelValue.String(detail is null ? "" : Text(detail, "description", detail.description)),
            "collection.author" => NovelValue.String(detail?.author ?? ""),
            "collection.hasImage" => NovelValue.Boolean(m_images.current?.image is not null),
            "collection.imagePrevious" => NovelValue.Boolean(m_images.index > 0),
            "collection.imageNext" => NovelValue.Boolean(m_images.index >= 0 && m_images.index + 1 < m_images.count),
            "collection.imageCount" => NovelValue.Integer(m_images.count),
            "collection.imagePage" => NovelValue.String((m_images.index + 1).ToString(CultureInfo.InvariantCulture) + " / " + m_images.count.ToString(CultureInfo.InvariantCulture)),
            "collection.variantName" => NovelValue.String(m_images.current is { } variant && detail is not null
                ? detail.variants.Length == 0 ? Text(detail, "title", detail.title) : session.localization.Resolve(StoryTextKeys.Collection(detail.id, "variant/" + variant.id + "/title"), variant.title) : ""),
            "music.title" => NovelValue.String(music.selected.Length > 0 ? Text(session.collections.Get(music.selected), "title", session.collections.Get(music.selected).title) : ""),
            "music.available" => NovelValue.Boolean(music.available),
            "music.paused" => NovelValue.Boolean(music.paused),
            "music.repeat" => NovelValue.Boolean(music.repeat),
            "music.shuffle" => NovelValue.Boolean(music.shuffle),
            _ => NovelValue.String("")
        };
        return path is "collections.search" or "collections.empty" or "collections.page" or "collections.previous" or "collections.next" or "collection.title" or "collection.description" or "collection.author"
            or "music.title" or "music.available" or "music.paused" or "music.repeat" or "music.shuffle"
            or "collection.hasImage" or "collection.imagePrevious" or "collection.imageNext" or "collection.imageCount" or "collection.imagePage" or "collection.variantName";
    }
    /// <inheritdoc />
    public override bool TryWrite(
        string path,
        NovelValue value
    ) {
        if (path == "collections.search" && value.kind == NovelValueKind.String) { m_search = value.text; m_page = 0; return true; }
        if (path == "music.repeat" && value.kind == NovelValueKind.Boolean) { music.repeat = value.boolean; return true; }
        if (path == "music.shuffle" && value.kind == NovelValueKind.Boolean) { music.shuffle = value.boolean; return true; }
        return false;
    }
    /// <inheritdoc />
    public override TextureAsset? ReadImage(
        string path,
        UiDataRow? row
    ) {
        if (path == "collection.image") return m_images.current?.image;
        string id = path == "item.image" ? row?.key ?? "" : path == "collection.image" ? selected : path == "music.image" ? music.selected : "";
        return id.Length > 0 && session.collections.IsUnlocked(id) ? session.collections.Get(id).image : null;
    }
    /// <inheritdoc />
    public override IReadOnlyList<UiDataRow> ReadRows(string path)
    {
        if (path != "collections.items") return [];
        CollectionEntry[] items = Items(); m_page = Math.Min(m_page, Pages() - 1);
        return items.Skip(m_page * pageSize).Take(pageSize).Select(entry =>
        {
            bool unlocked = session.collections.IsUnlocked(entry.id);
            return new UiDataRow(entry.id, new Dictionary<string, NovelValue>
            {
                ["id"] = NovelValue.String(entry.id), ["title"] = NovelValue.String(unlocked ? Text(entry, "title", entry.title) : session.localization.Resolve("collection/locked", "未解锁")),
                ["description"] = NovelValue.String(unlocked ? Text(entry, "description", entry.description) : ""), ["unlocked"] = NovelValue.Boolean(unlocked)
            });
        }).ToArray();
    }
    private int Pages() => Math.Max(1, (Items().Length + pageSize - 1) / pageSize);
    private string Text(
        CollectionEntry entry,
        string field,
        string source
    ) => session.localization.Resolve(StoryTextKeys.Collection(entry.id, field), source);
    private CollectionEntry[] Items()
    {
        var kind = role switch { "music" => CollectionKind.Music, "replays" => CollectionKind.Replay, "endings" => CollectionKind.Ending, "glossary" => CollectionKind.Glossary, "characters" => CollectionKind.Character, _ => CollectionKind.Cg };
        return session.collections.entries.Where(entry => entry.kind == kind && (m_search.Length == 0
            || session.collections.IsUnlocked(entry.id) && Text(entry, "title", entry.title).Contains(m_search, StringComparison.OrdinalIgnoreCase))).ToArray();
    }
}
