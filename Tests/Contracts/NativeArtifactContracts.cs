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
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Interface.Themes;
using Inno.VisualNovel.Model.Story;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class NativeArtifactContracts
{
    internal static void SourceMountOwnership(
        string project,
        ModuleHost modules,
        TypeCatalog types,
        SerializationRegistry native,
        IdentityAllocator identities
    ) {
        string root = Path.Combine(project, "artifacts", "AcceptanceProjects", "NativePackage-" + Guid.NewGuid().ToString("N"));
        string source = Path.Combine(root, "Authoring", "Assets"); Directory.CreateDirectory(source);
        Guid pageId;
        using var logs = new LogRouter();
        using (var assets = new AssetPipeline(modules, types, native, identities, new DiagnosticHub(), logs,
            AssetPipelineOptions.Create(source, Path.Combine(root, "Authoring", "Library")) with { enableFileSystemWatcher = false, deferUnavailableExtensions = true }))
        using (AssetExecutionContext.EnterScope(assets))
        {
            Check(EditorAssets.Save(AssetPath.Project("Page.ivnui"), ThemeContracts.Screen()), "The development source page did not save.");
            var page = Assets.Assets.Load<ScreenAsset>(AssetPath.Project("Page.ivnui")); pageId = page.identity.persistentId;
            var theme = ThemeContracts.Create(); theme.screens = [new() { role = "title", screen = page }];
            Check(EditorAssets.Save(AssetPath.Project("Theme.ivntheme"), theme), "The development theme did not save its source-local dependency.");
        }
        string package = Path.Combine(root, "ReadOnlyPlugin"), consumer = Path.Combine(root, "Consumer", "Assets");
        Directory.CreateDirectory(package); Directory.CreateDirectory(consumer);
        foreach (string path in Directory.EnumerateFiles(source)) File.Copy(path, Path.Combine(package, Path.GetFileName(path)));
        var plugin = new AssetSourceId("inno.visualnovel.fixture");
        using (var assets = new AssetPipeline(modules, types, native, identities, new DiagnosticHub(), logs,
            AssetPipelineOptions.Create(consumer, Path.Combine(root, "Consumer", "Library")) with { enableFileSystemWatcher = false, deferUnavailableExtensions = true,
                sourceMounts = [new(AssetSourceId.project, consumer, false, [plugin]), new(plugin, package, true)] }))
        using (AssetExecutionContext.EnterScope(assets))
        {
            var themePath = new AssetPath(plugin, "Theme.ivntheme");
            var theme = Assets.Assets.Load<ThemeAsset>(themePath);
            if (theme.assetPath.localPath.Length == 0 || theme.screens.Any(static binding => binding.screen is null || binding.screen.assetPath.localPath.Length == 0))
            {
                Console.WriteLine("HOST PRECONDITION: explicitly preparing native page/theme ownership through public import after read-only source discovery. Initial theme path=" + theme.assetPath);
                Check(assets.Import(new AssetPath(plugin, "Page.ivnui")) && assets.Import(themePath), "The selected headless host could not prepare the read-only native reference fixture.");
                theme = Assets.Assets.Load<ThemeAsset>(themePath);
            }
            Check(!theme.isMissing && theme.assetPath.source == plugin && theme.screens.Single().screen is { isMissing: false } page
                && page.identity.persistentId == pageId && page.assetPath.source == plugin,
                "A package-local reference escaped its importing mount: theme=" + theme.assetPath + ", missing=" + theme.isMissing + ", pages=" + theme.screens.Length
                + ", page=" + theme.screens.FirstOrDefault()?.screen?.assetPath + ", expected=" + pageId + ", actual=" + theme.screens.FirstOrDefault()?.screen?.identity.persistentId);
            Check(assets.GetDependencies(theme, true).Single().persistentId == pageId, "Rebasing a package reference changed its stable identity or dependency closure.");
        }
        Console.WriteLine("Native package-source acceptance project: " + root);
    }
    internal static void RootsAndColdReload(
        string project,
        ModuleHost modules,
        TypeCatalog types,
        SerializationRegistry native,
        IdentityAllocator identities,
        StoryNodeDataCodec codec
    ) {
        string root = Path.Combine(project, "artifacts", "AcceptanceProjects", "NativeCold-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Assets"));
        var options = AssetPipelineOptions.Create(Path.Combine(root, "Assets"), Path.Combine(root, "Library")) with { enableFileSystemWatcher = false, deferUnavailableExtensions = true };
        using var logs = new LogRouter(); Guid themeId, screenId, storyId;
        string deployed = Path.Combine(root, "RuntimeContent");
        using (var assets = new AssetPipeline(modules, types, native, identities, new DiagnosticHub(), logs, options))
        using (AssetExecutionContext.EnterScope(assets))
        {
            Check(EditorAssets.Save(AssetPath.Project("Page.ivnui"), ThemeContracts.Screen()), "Native page could not be committed.");
            var page = Assets.Assets.Load<ScreenAsset>(AssetPath.Project("Page.ivnui")); screenId = page.identity.persistentId;
            var theme = ThemeContracts.Create(); theme.screens = [new() { role = "title", screen = page }];
            Check(EditorAssets.Save(AssetPath.Project("Theme.ivntheme"), theme), "Native theme could not be committed.");
            var imported = Assets.Assets.Load<ThemeAsset>(AssetPath.Project("Theme.ivntheme")); themeId = imported.identity.persistentId;
            var dependencies = assets.GetDependencies(imported, true);
            Check(dependencies.Count == 1 && dependencies.Single().persistentId == screenId,
                "Import artifact serialized its detached root as an unresolvable asset dependency.");
            var outputs = new System.Collections.Generic.List<AssetObject>();
            var copy = (ThemeAsset)Authoring.BuiltInResources.BuiltInResourceCopyService.Copy(imported, outputs.Add);
            Check(copy.identity.persistentId != themeId && copy.resourceId != imported.resourceId && copy.assetPath.localPath.StartsWith("Game/Resources/", StringComparison.Ordinal)
                && copy.screens[0].screen!.identity.persistentId != screenId && !ReferenceEquals(copy.styles, imported.styles),
                "Editable theme copy reused installation identities, page roots or mutable style declarations.");
            Check(outputs.Count == 2 && outputs[0] is ScreenAsset && outputs[1] == copy, "Native preset copy did not report its committed dependent page and root in order.");
            var aliasSource = ThemeAssetCopy.Copy(imported); aliasSource.screens = [new() { role = "title", screen = page }, new() { role = "credits", screen = page }];
            Check(EditorAssets.Save(AssetPath.Project("Aliased.ivntheme"), aliasSource), "Could not prepare a theme sharing one page across roles.");
            outputs.Clear(); var aliasCopy = (ThemeAsset)Authoring.BuiltInResources.BuiltInResourceCopyService.Copy(Assets.Assets.Load<ThemeAsset>(AssetPath.Project("Aliased.ivntheme")), outputs.Add);
            Check(outputs.Count == 2 && aliasCopy.screens[0].screen!.identity.persistentId == aliasCopy.screens[1].screen!.identity.persistentId
                && aliasCopy.screens[0].screen!.identity.persistentId != page.identity.persistentId,
                "Copying a theme duplicated an intentionally shared page or retained its installation identity.");
            var batch = new Authoring.BuiltInResources.BuiltInResourceCopyBatch([imported, page]);
            Check(!batch.Advance() && batch.completed == 1 && batch.created.Count == 2 && batch.roots.Count == 1, "One batch update copied more than one requested native root or omitted theme outputs.");
            batch.Cancel(); Check(batch.cancelled && batch.Advance() && batch.completed == 1 && batch.created.Count == 2, "Cancelling a native copy batch resumed pending writes or discarded committed output provenance.");
            var finish = new Authoring.BuiltInResources.BuiltInResourceCopyBatch([page]);
            Check(finish.Advance() && finish.error.Length == 0 && finish.completed == 1 && finish.roots[0].assetId != page.identity.persistentId,
                "A completed single-root batch did not produce a new independent project identity.");
            var editable = Assets.Assets.Load<ScreenAsset>(finish.roots[0].assetId);
            var stale = new Authoring.BuiltInResources.BuiltInResourceCopyBatch([editable]);
            var edited = Inno.VisualNovel.UI.Documents.UiDefinitionCopy.Copy(editable); edited.title += " changed";
            Check(EditorAssets.Save(editable.assetPath, edited), "Could not prepare a changed native batch source.");
            Check(stale.Advance() && stale.error.Length > 0 && stale.completed == 0 && stale.created.Count == 0
                && stale.failure is Authoring.Diagnostics.AuthoringValidationException { source: "源资源在批量准备后已改变、移动或缺失，请重新选择：{0}" } cause
                && cause.arguments.Single()?.ToString() == editable.assetPath.ToString(), "A changed native preset escaped its frozen batch revision, lost presentation arguments or created a partial root.");
            bool duplicates = false;
            try { _ = new Authoring.BuiltInResources.BuiltInResourceCopyBatch([page, page]); } catch (ArgumentException) { duplicates = true; }
            Check(duplicates, "A batch accepted duplicate native input identities before review.");
            var ownedTheme = (ThemeAsset)Authoring.BuiltInResources.BuiltInResourceCopyService.Copy(imported);
            var themeBatch = new Authoring.BuiltInResources.BuiltInResourceCopyBatch([ownedTheme]);
            var ownedPage = ownedTheme.screens[0].screen!; var updatedPage = Inno.VisualNovel.UI.Documents.UiDefinitionCopy.Copy(ownedPage); updatedPage.title += " batch-owned change";
            Check(EditorAssets.Save(ownedPage.assetPath, updatedPage), "Could not change the owned theme page through native source saving.");
            Check(themeBatch.Advance() && themeBatch.error.Length > 0 && themeBatch.created.Count == 0 && themeBatch.completed == 0,
                "A theme batch mixed page revisions frozen before review or committed outputs after a stale owned page.");
            Check(EditorAssets.Save(AssetPath.Project("Story.ivnstory"), FirstStoryTemplate.Create(codec).ToAsset()), "Native story did not compile during import.");
            var story = Assets.Assets.Load<StoryAsset>(AssetPath.Project("Story.ivnstory")); storyId = story.identity.persistentId;
            Check(story.program?.instructions.Length == 6 && assets.GetDependencies(story, true).Count == 0,
                "Compiled story artifact depends on its temporary detached root instead of the explicit content closure.");
            _ = assets.ExportRuntimeArtifacts(deployed);
        }
        string emptySources = Path.Combine(root, "EmptyRuntimeSources"); Directory.CreateDirectory(emptySources);
        using (var runtime = new AssetPipeline(modules, types, native, identities, new DiagnosticHub(), logs,
            AssetPipelineOptions.Create(emptySources, deployed) with { mode = AssetPipelineMode.RuntimeArtifacts, enableFileSystemWatcher = false, deferUnavailableExtensions = true }))
        using (AssetExecutionContext.EnterScope(runtime))
        {
            var theme = Assets.Assets.Load<ThemeAsset>(themeId); var story = Assets.Assets.Load<StoryAsset>(storyId);
            Check(!theme.isMissing && theme.screens[0].screen is { isMissing: false } page && page.identity.persistentId == screenId
                && !story.isMissing && story.program?.instructions.Length == 6,
                "Source-free runtime artifact loading attempted to recover content from authoring files.");
        }
        using (var assets = new AssetPipeline(modules, types, native, identities, new DiagnosticHub(), logs, options))
        using (AssetExecutionContext.EnterScope(assets))
        {
            var theme = Assets.Assets.Load<ThemeAsset>(AssetPath.Project("Theme.ivntheme"));
            var story = Assets.Assets.Load<StoryAsset>(AssetPath.Project("Story.ivnstory"));
            Check(!theme.isMissing && theme.identity.persistentId == themeId && theme.screens[0].screen is { isMissing: false } page && page.identity.persistentId == screenId
                && !story.isMissing && story.identity.persistentId == storyId && story.program?.instructions.Length == 6,
                "Cold native cache loading lost root identity, nested references or compiled instructions.");
            Check(assets.GetDependencies(theme, true).Count == 1 && assets.GetDependencies(story, true).Count == 0, "Cold catalog reload resurrected phantom root dependencies.");
        }
        Console.WriteLine("Native cold-cache acceptance project: " + root);
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
