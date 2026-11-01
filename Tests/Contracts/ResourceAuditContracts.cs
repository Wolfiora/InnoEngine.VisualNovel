using System;
using System.IO;
using System.Linq;
using Inno.Assets;
using Inno.Assets.Pipeline;
using Inno.Core.Diagnostics;
using Inno.Core.Identity;
using Inno.Core.Logging;
using Inno.Core.Serialization;
using Inno.Editor.Assets;
using Inno.Extensibility.Modules;
using Inno.Extensibility.Types;
using Inno.Rendering.Assets;
using Inno.VisualNovel.Authoring.AssetAuditing;
using Inno.VisualNovel.Authoring.AssetAuditing.Licenses;
using Inno.VisualNovel.Authoring.Publishing;
using Inno.VisualNovel.UI.Imaging;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Programs;
using Inno.VisualNovel.Model.Story.Nodes.Interface;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class ResourceAuditContracts
{
    internal static void NativeInventoryProvenanceAndFailureEvidence(
        string project,
        ModuleHost modules,
        TypeCatalog types,
        SerializationRegistry native,
        IdentityAllocator identities
    ) {
        string root = Path.Combine(project, "artifacts", "AcceptanceProjects", "ResourceAudit-" + Guid.NewGuid().ToString("N"));
        string source = Path.Combine(root, "Assets"); Directory.CreateDirectory(source);
        byte[] picture = UiPngImage.Encode(new(1, 1, [255, 0, 0, 255]));
        File.WriteAllBytes(Path.Combine(source, "used.png"), picture); File.WriteAllBytes(Path.Combine(source, "duplicate.png"), picture);
        File.WriteAllBytes(Path.Combine(source, "unused.png"), UiPngImage.Encode(new(1, 1, [0, 255, 0, 255])));
        File.WriteAllText(Path.Combine(source, "invalid.png"), "not an image");
        Directory.CreateDirectory(Path.Combine(source, "~Samples")); File.WriteAllBytes(Path.Combine(source, "~Samples", "ignored.png"), picture);
        using var logs = new LogRouter(); using var assets = new AssetPipeline(modules, types, native, identities, new DiagnosticHub(), logs,
            AssetPipelineOptions.Create(source, Path.Combine(root, "Library")) with { enableFileSystemWatcher = false });
        using var scope = AssetExecutionContext.EnterScope(assets);
        Check(assets.Import(AssetPath.Project("used.png")) && assets.Import(AssetPath.Project("duplicate.png")) && assets.Import(AssetPath.Project("unused.png")), "Native audit fixture images failed to import.");
        var used = Inno.Assets.Assets.Load<TextureAsset>(AssetPath.Project("used.png"));
        var organized = ResourceLabelContracts.Exercise(used, Inno.Assets.Assets.Load<TextureAsset>(AssetPath.Project("duplicate.png")));
        Check(EditorAssets.Save(AssetPath.Project("Labels.ivnlabels"), organized), "Native resource labels failed to save/import.");
        var labels = Inno.Assets.Assets.Load<Inno.VisualNovel.Authoring.AssetOrganization.ResourceLabelCatalogAsset>(AssetPath.Project("Labels.ivnlabels"));
        Check(labels.entries.Length == 2 && labels.entries[0].group == "共享" && assets.CaptureProperties(labels).dependencies.Count == 2,
            "Native resource labels lost annotation values or ordinary engine dependency identity.");
        var labelPublication = NovelPublishPreflight.Check(new NovelProjectAsset { additionalResources = [labels] });
        Check(labelPublication.issues.Any(issue => issue.code == "VN-PUBLISH-EDITOR-ONLY" && issue.location == labels.assetPath.ToString()),
            "An editor label catalog was accepted as a dynamic Player content root.");
        var catalog = new ResourceLicenseCatalogAsset();
        ResourceLicenseEditService.Set(catalog, new() { asset = used, author = "Test author", source = "Original fixture", license = "Test permission", attributionRequired = true, notice = "Exact notice\r\nSecond line", redistributionConfirmed = true });
        Check(EditorAssets.Save(AssetPath.Project("Rights.ivnlicenses"), catalog), "Native provenance catalog did not save/import.");
        var imported = Inno.Assets.Assets.Load<ResourceLicenseCatalogAsset>(AssetPath.Project("Rights.ivnlicenses"));
        Check(imported.entries.Single().asset?.identity.persistentId == used.identity.persistentId && assets.CaptureProperties(imported).dependencies.Any(dependency => dependency.persistentId == used.identity.persistentId), "Native provenance lost identity or reference dependency.");
        Check(EditorAssets.Save(AssetPath.Project("Picture.ivnui"), new ScreenAsset { root = new() { id = "root", kind = UiElementKind.Panel,
            children = [new() { id = "picture", kind = UiElementKind.Image, image = used }] } }), "Native usage fixture screen did not import.");
        var screen = Inno.Assets.Assets.Load<ScreenAsset>(AssetPath.Project("Picture.ivnui"));
        var usageClosure = new NovelPublishReport("Usage", "content", [new(used.identity.persistentId, used.assetPath, 0, [screen.assetPath.ToString()]), new(screen.identity.persistentId, screen.assetPath, 0, [])], []);
        var chapter = new StoryAsset { program = new StoryProgram { instructions = [new() { nodeId = "nonmatch" },
            new() { nodeId = "open", sourceNodeId = "authored-open", system = new() { operation = SystemOperation.OpenPage, argument = "custom.page" } }] } };
        var usageGame = new NovelProjectAsset { chapters = [chapter], screens = [new() { role = "custom.page", screen = screen }] };
        using (var usage = new ResourceUsageSearch(usageGame, usageClosure, used.identity.persistentId))
        {
            chapter.program.instructions[1].system!.argument = "changed after lookup began";
            usage.Step(1); usage.Step(1);
            Check(usage.scanned == 2 && usage.results.All(static result => result.nodeId.Length == 0), "Resource usage skipped its nonmatch budget or expanded future candidates eagerly.");
            for (int budget = 0; !usage.completed && budget < 100; budget++) usage.Step(1);
            Check(usage.results.Any(static result => result.nodeId == "authored-open" && result.throughLibrary)
                && usage.results.Any(static result => result.elementId == "picture" && !result.throughLibrary), "Usage lookup lost a frozen authored node, containing native library or exact UI declaration.");
        }
        var closure = new NovelPublishReport("One game", "content", [new(used.identity.persistentId, used.assetPath, 0, ["Game.ivnproject"])], []);
        using (var audit = new ProjectResourceAudit(root, closure, imported))
        {
            for (int budget = 0; !audit.completed && budget < 10000; budget++) audit.Step(16);
            var report = audit.Capture();
            Check(report.inventoryComplete && report.closureComplete && report.items.Single(item => item.path.localPath == "used.png").use == ResourceAuditUse.Referenced
                && report.items.Single(item => item.path.localPath == "unused.png").use == ResourceAuditUse.NotReferenced, "Native audit replaced explicit reference evidence with filename guesses.");
            Check(report.items.Single(item => item.path.localPath == "invalid.png").use == ResourceAuditUse.Unknown && report.items.All(item => !item.path.localPath.Contains("~Samples", StringComparison.Ordinal)), "Unimported or sample sources became false unused candidates.");
            Check(report.duplicateGroups.Single().Count == 2 && report.items.Single(item => item.path.localPath == "Rights.ivnlicenses").use == ResourceAuditUse.AuthoringOnly,
                "Byte duplicates or expected authoring-only exclusion were misreported.");
            Check(report.items.Single(item => item.path.localPath == "Labels.ivnlabels").use == ResourceAuditUse.AuthoringOnly,
                "Editor resource labels were reported as unused Player content.");
            Check(report.NoticesText().Contains("Exact notice\r\nSecond line", StringComparison.Ordinal) && report.ToMarkdown().Contains("不删除文件", StringComparison.Ordinal), "Notice export changed its body or implied destructive automatic cleanup.");
        }
        var broken = new NovelPublishReport("Broken game", "content", closure.assets, [new(PublishIssueLevel.Error, "VN-PUBLISH-CONTENT", "game", "Missing root")]);
        using (var audit = new ProjectResourceAudit(root, broken, imported))
        {
            for (int budget = 0; !audit.completed && budget < 10000; budget++) audit.Step(64);
            Check(audit.Capture().items.All(static item => item.use != ResourceAuditUse.NotReferenced), "An incomplete native closure produced confident unused-resource claims.");
        }
        string changing = Path.Combine(source, "changing.png"); File.WriteAllBytes(changing, new byte[1024 * 1024]);
        using (var audit = new ProjectResourceAudit(root, closure, imported))
        {
            for (int budget = 0; audit.currentSource?.localPath != "changing.png" && !audit.completed && budget < 10000; budget++) audit.Step(1);
            Check(audit.currentSource?.localPath == "changing.png", "Incremental hashing never exposed its owned source progress.");
            using (var writer = new FileStream(changing, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete)) writer.WriteByte(1);
            for (int budget = 0; !audit.completed && budget < 10000; budget++) audit.Step(32);
            var changed = audit.Capture().items.Single(item => item.path.localPath == "changing.png");
            Check(changed.hash.Length == 0 && changed.use == ResourceAuditUse.Unknown && changed.message.Contains("发生变化", StringComparison.Ordinal), "A concurrently modified source retained a trusted hash or unused classification.");
        }
        using (var audit = new ProjectResourceAudit(root, closure))
        {
            for (int budget = 0; audit.currentSource?.localPath != "changing.png" && !audit.completed && budget < 10000; budget++) audit.Step(1);
            audit.Cancel(); int count = audit.items.Count; audit.Step(10);
            Check(audit.cancelled && !audit.Capture().inventoryComplete && audit.items.Count == count, "Cancellation continued reading or falsely completed its report.");
            using var exclusive = new FileStream(changing, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        Console.WriteLine("Native resource audit project: " + root);
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
