using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Player.Persistence;
using Inno.VisualNovel.UI.Binding;

namespace Inno.VisualNovel.Player.Binding;

/// <summary>Projects bounded native slot metadata into editable save and load page bindings.</summary>
/// <param name="controller">The optional configured native IO owner.</param>
/// <param name="manualSlots">The displayed manual capacity when persistence is unavailable.</param>
/// <param name="automaticSlots">The displayed automatic capacity when persistence is unavailable.</param>
public sealed class SavePageDataSource(
    SaveSlotController? controller,
    int manualSlots = 100,
    int automaticSlots = 3
) : UiDataSource
{
    private readonly int m_manualSlots = manualSlots is >= 1 and <= 10000 ? manualSlots : throw new ArgumentOutOfRangeException(nameof(manualSlots));
    private readonly int m_automaticSlots = automaticSlots is >= 1 and <= 100 ? automaticSlots : throw new ArgumentOutOfRangeException(nameof(automaticSlots));
    private int m_page;
    private SaveSlotKind m_category;
    private int m_imageRevision = -1;
    private readonly Dictionary<string, Inno.VisualNovel.UI.Imaging.UiBitmap?> m_images = new(StringComparer.Ordinal);
    private int m_metadataRevision = -1;
    private Dictionary<SaveSlot, SaveSlotMetadata> m_metadata = [];
    /// <inheritdoc />
    public override Inno.VisualNovel.UI.Imaging.UiBitmap? ReadBitmap(
        string path,
        UiDataRow? row
    ) {
        if (path != "item.thumbnail" || row is null || controller is null || !TrySlot(row.key, out var slot)) return null;
        if (m_imageRevision != controller.revision) { m_imageRevision = controller.revision; m_images.Clear(); }
        if (m_images.TryGetValue(row.key, out var image)) return image;
        byte[]? png = controller.ReadThumbnail(slot);
        try { image = png is { Length: > 0 } ? Inno.VisualNovel.UI.Imaging.UiPngImage.Decode(png) : null; }
        catch (Exception failure) when (failure is System.IO.InvalidDataException or ArgumentException or OverflowException) { image = null; }
        m_images.Add(row.key, image); return image;
    }
    /// <summary>Gets the currently selected independent slot category.</summary>
    public SaveSlotKind category => m_category;
    /// <summary>Gets the zero-based page index.</summary>
    public int page => m_page;
    private int count => m_category switch { SaveSlotKind.Quick => 1, SaveSlotKind.Automatic => controller?.automaticSlotCount ?? m_automaticSlots, _ => controller?.manualSlotCount ?? m_manualSlots };

    /// <summary>Selects a category without conflating quick, automatic and manual numbering.</summary>
    /// <param name="kind">The explicit category.</param>
    public void Select(SaveSlotKind kind)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        m_category = kind; m_page = 0;
    }
    /// <summary>Changes the displayed page within the configured slot bounds.</summary>
    /// <param name="delta">A signed page step.</param>
    public void Move(int delta) => m_page = (int)Math.Clamp((long)m_page + delta, 0, (count - 1) / 10);

    /// <inheritdoc />
    public override bool TryRead(string path, out NovelValue value)
    {
        value = path switch
        {
            "saves.available" => NovelValue.Boolean(controller is not null),
            "saves.busy" => NovelValue.Boolean(controller?.busy ?? false),
            "saves.ready" => NovelValue.Boolean(controller is { busy: false }),
            "saves.canSave" => NovelValue.Boolean(controller?.canSave ?? false),
            "saves.canContinue" => NovelValue.Boolean(controller?.canContinue ?? false),
            "saves.failed" => NovelValue.Boolean(controller is { error.Length: > 0 }),
            "saves.previous" => NovelValue.Boolean(m_page > 0),
            "saves.next" => NovelValue.Boolean((m_page + 1) * 10 < count),
            "saves.page" => NovelValue.String((m_page + 1).ToString(CultureInfo.InvariantCulture) + " / " + ((count + 9) / 10).ToString(CultureInfo.InvariantCulture)),
            "saves.message" => NovelValue.String(controller is null ? "当前宿主尚未接入原生存档服务。" : controller.busy ? "正在处理…" : controller.error),
            _ => NovelValue.String(string.Empty)
        };
        return path is "saves.available" or "saves.busy" or "saves.ready" or "saves.canSave" or "saves.canContinue" or "saves.failed" or "saves.previous" or "saves.next" or "saves.page" or "saves.message";
    }
    /// <inheritdoc />
    public override bool TryWrite(string path, NovelValue value) => false;
    /// <inheritdoc />
    public override IReadOnlyList<UiDataRow> ReadRows(string path)
    {
        if (path != "saves.slots") return [];
        if (m_metadataRevision != (controller?.revision ?? -1))
        { m_metadataRevision = controller?.revision ?? -1; m_metadata = controller?.ReadMetadata().ToDictionary(static item => item.slot) ?? []; }
        var slots = m_metadata;
        return Enumerable.Range(m_page * 10, Math.Min(10, count - m_page * 10)).Select(index =>
        {
            var slot = new SaveSlot(m_category, index); slots.TryGetValue(slot, out SaveSlotMetadata? metadata);
            string key = ((int)m_category).ToString(CultureInfo.InvariantCulture) + ":" + index.ToString(CultureInfo.InvariantCulture);
            string label = m_category switch { SaveSlotKind.Quick => "快速存档", SaveSlotKind.Automatic => "自动存档 ", _ => "存档 " };
            if (m_category != SaveSlotKind.Quick) label += (index + 1).ToString(CultureInfo.InvariantCulture);
            string detail = metadata is null ? "空槽位" : metadata.error.Length > 0 ? metadata.error
                : new DateTime(metadata.utcTicks, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "\n" + metadata.summary;
            return new UiDataRow(key, new Dictionary<string, NovelValue>
            {
                ["key"] = NovelValue.String(key), ["label"] = NovelValue.String(label + "\n" + detail),
                ["canSave"] = NovelValue.Boolean(m_category == SaveSlotKind.Manual && (controller?.canSave ?? false)),
                ["canLoad"] = NovelValue.Boolean(controller is { busy: false } && metadata is { compatible: true }),
                ["canDelete"] = NovelValue.Boolean(controller is { busy: false } && metadata is not null)
            });
        }).ToArray();
    }

    /// <summary>Parses a neutral action key without accepting paths or unbounded integer categories.</summary>
    /// <param name="key">The category:index key from a displayed slot row.</param>
    /// <param name="slot">Receives the parsed logical slot.</param>
    /// <returns>Whether its category and numeric bounds are valid.</returns>
    public bool TrySlot(
        string key,
        out SaveSlot slot
    ) {
        slot = default; string[] parts = key.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int kind)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int index) || !Enum.IsDefined((SaveSlotKind)kind)) return false;
        int limit = (SaveSlotKind)kind switch { SaveSlotKind.Quick => 1, SaveSlotKind.Automatic => controller?.automaticSlotCount ?? 3, _ => controller?.manualSlotCount ?? 100 };
        if (index < 0 || index >= limit) return false;
        slot = new((SaveSlotKind)kind, index); return true;
    }
}
