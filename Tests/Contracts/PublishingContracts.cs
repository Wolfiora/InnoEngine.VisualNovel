using System;
using System.IO;
using System.Linq;
using Inno.Assets;
using Inno.Assets.Pipeline;
using Inno.Core.Diagnostics;
using Inno.Core.Identity;
using Inno.Core.Logging;
using Inno.Core.Serialization;
using Inno.Extensibility.Modules;
using Inno.Extensibility.Types;
using Inno.Rendering.Assets;
using Inno.VisualNovel.Authoring.ProjectEditing;
using Inno.VisualNovel.Authoring.Publishing;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.UI.Imaging;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class PublishingContracts
{
    internal static void NativeClosure(
        string projectRoot,
        ModuleHost modules,
        TypeCatalog types,
        SerializationRegistry serialization,
        IdentityAllocator identities
    ) {
        string root = Path.Combine(projectRoot, "artifacts", "AcceptanceProjects", "Publishing-" + Guid.NewGuid().ToString("N"));
        string assetRoot = Path.Combine(root, "Assets"); Directory.CreateDirectory(assetRoot);
        File.WriteAllBytes(Path.Combine(assetRoot, "Image.png"), UiPngImage.Encode(new(1, 1, new byte[] { 255, 128, 0, 255 })));
        using var logs = new LogRouter();
        using var assets = new AssetPipeline(modules, types, serialization, identities, new DiagnosticHub(), logs,
            AssetPipelineOptions.Create(assetRoot, Path.Combine(root, "Library")) with { enableFileSystemWatcher = false, deferUnavailableExtensions = true });
        using var scope = AssetExecutionContext.EnterScope(assets);
        TextureAsset image = Assets.Assets.Load<TextureAsset>(AssetPath.Project("Image.png"));
        VisualEffectAsset preset = ParticleContracts.Preset(); preset.particles.texture = image;
        Check(EditorAssets.Save(AssetPath.Project("Rain.ivneffect"), preset), "Native effect fixture did not import.");
        preset = Assets.Assets.Load<VisualEffectAsset>(AssetPath.Project("Rain.ivneffect"));
        var game = new NovelProjectAsset { title = "发布|检查\n测试" };
        NovelProjectEditService.AddPackagingRoot(game, preset); NovelProjectEditService.AddPackagingRoot(game, image); NovelProjectEditService.AddPackagingRoot(game, image);
        Check(game.additionalResources.Length == 2, "Adding the same dynamic root duplicated its persistent native identity.");
        var native = EditorAssets.CaptureProperties(game);
        Check(native.dependencies.Any(item => item.persistentId == preset.identity.persistentId)
            && native.dependencies.Any(item => item.persistentId == image.identity.persistentId), "Dynamic roots did not enter the engine-collected build dependency set.");
        var decoded = EditorAssets.DecodeNative<NovelProjectAsset>(EditorAssets.EncodeNative(game));
        Check(decoded.additionalResources.Length == 2 && decoded.additionalResources[0].identity.persistentId == preset.identity.persistentId,
            "Native game source roundtrip changed polymorphic packaging roots.");
        NovelPublishReport report = NovelPublishPreflight.Check(decoded);
        Check(report.assets.Count == 2 && report.assets.Single(item => item.id == image.identity.persistentId).referrers.Count == 2,
            "Transitive native references were not deduplicated or lost their direct referring locations: "
            + string.Join("; ", report.assets.Select(item => item.path + " <- " + string.Join(",", item.referrers))) + " | "
            + string.Join("; ", report.issues.Select(static item => item.code + ":" + item.message)));
        Check(!report.contentValid && report.requiresHostVerification
            && report.issues.Any(static item => item.code == "VN-PUBLISH-CONTENT")
            && report.issues.Count(static item => item.code == "VN-PUBLISH-SCREEN") == 18
            && report.issues.Count(static item => item.code == "VN-PUBLISH-SHADER") == 1,
            "Invalid content or missing native publication contracts appeared ready.");
        Check(report.ToMarkdown().Contains("发布\\|检查 测试", StringComparison.Ordinal)
            && report.ToMarkdown().Contains("not package size", StringComparison.Ordinal), "The report confused serialized properties with release size or broke its Markdown table.");
        Directory.CreateDirectory(Path.Combine(assetRoot, "~Samples"));
        File.WriteAllBytes(Path.Combine(assetRoot, "~Samples", "Teaching.png"), UiPngImage.Encode(new(1, 1, new byte[] { 128, 255, 0, 255 })));
        TextureAsset sample = Assets.Assets.Load<TextureAsset>(AssetPath.Project("~Samples/Teaching.png"));
        NovelProjectEditService.AddPackagingRoot(game, sample);
        report = NovelPublishPreflight.Check(game);
        Check(report.issues.Any(item => item.code == "VN-PUBLISH-SAMPLE" && item.location == "~Samples/Teaching.png"),
            "A runtime resource under the engine-excluded sample subtree passed publication audit.");
        Console.WriteLine("Native publishing acceptance project: " + root);
    }
    private static void Check(
        bool condition,
        string message
    ) {
        if (!condition) throw new InvalidOperationException(message);
    }
}
