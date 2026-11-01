using System;
using System.Linq;
using Inno.Assets;
using Inno.Assets.Pipeline;
using Inno.VisualNovel.Authoring.AssetOrganization;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class ResourceLabelContracts
{
    internal static ResourceLabelCatalogAsset Exercise(
        AssetObject first,
        AssetObject second
    ) {
        var catalog = new ResourceLabelCatalogAsset();
        var entry = new ResourceLabelEntry { asset = first, group = " 教学 ", tags = [" 夜晚 ", "城市", "夜晚"], description = "窗边的灯光" };
        ResourceLabelEditService.Set(catalog, entry); entry.tags[0] = "Changed";
        ResourceLabelEditService.Set(catalog, new() { asset = second, group = "音效", tags = ["UI", "通用"], description = "确认" });
        Check(catalog.entries[0].group == "教学" && catalog.entries[0].tags.SequenceEqual(new[] { "夜晚", "城市" })
            && ResourceLabelEditService.Search(catalog, "灯光").Single().asset == first
            && ResourceLabelEditService.Search(catalog, first.identity.persistentId.ToString("D")).Single().asset == first
            && ResourceLabelEditService.Search(catalog, "ui", "音效").Single().asset == second
            && ResourceLabelEditService.Search(catalog, "", "").Count == 0,
            "Resource labels lost normalized ownership, Unicode search or the difference between all and ungrouped entries.");
        var found = ResourceLabelEditService.Search(catalog, "夜晚"); found[0].tags[0] = "Changed query";
        Check(catalog.entries[0].tags[0] == "夜晚", "Query results exposed mutable authored label arrays.");
        byte[] before = EditorAssets.EncodeNative(catalog);
        Reject(() => ResourceLabelEditService.Set(catalog, new() { asset = first, tags = [new string('x', 65)] }));
        Reject(() => ResourceLabelEditService.ApplyBatch(catalog, [first.identity.persistentId, Guid.NewGuid()], new() { replaceGroup = true, group = "坏批次" }));
        Reject(() => ResourceLabelEditService.ApplyBatch(catalog, [first.identity.persistentId, first.identity.persistentId], new() { addTags = ["重复目标"] }));
        Check(EditorAssets.EncodeNative(catalog).SequenceEqual(before), "A rejected label edit or batch partially changed authored records.");
        ResourceLabelEditService.ApplyBatch(catalog, [first.identity.persistentId, second.identity.persistentId],
            new() { replaceGroup = true, group = "共享", addTags = ["预览", "ui"], removeTags = ["UI", "城市"] });
        Check(catalog.entries.All(static item => item.group == "共享" && item.tags.Contains("预览"))
            && catalog.entries[0].description == "窗边的灯光" && catalog.entries[1].description == "确认"
            && catalog.entries[0].tags.SequenceEqual(new[] { "夜晚", "预览", "ui" })
            && catalog.entries[1].tags.SequenceEqual(new[] { "通用", "预览", "ui" }),
            "A reviewed batch lost independent notes or failed deterministic remove-then-add tag semantics.");
        ResourceLabelEditService.ApplyBatch(catalog, [second.identity.persistentId], new() { replaceDescription = true, description = "新说明" });
        Check(catalog.entries[0].description == "窗边的灯光" && catalog.entries[1].description == "新说明", "Explicit description replacement changed unselected records.");
        var restored = EditorAssets.DecodeNative<ResourceLabelCatalogAsset>(EditorAssets.EncodeNative(catalog));
        ResourceLabelEditService.Remove(restored, first.identity.persistentId);
        Check(restored.entries.Length == 1 && restored.entries[0].asset?.identity.persistentId == second.identity.persistentId
            && catalog.entries.Length == 2 && !first.isMissing, "Removing native label metadata changed the original document or its borrowed resource.");
        return catalog;
    }
    private static void Reject(Action action)
    {
        try { action(); } catch (ArgumentException) { return; } catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Invalid resource label metadata was accepted.");
    }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}
