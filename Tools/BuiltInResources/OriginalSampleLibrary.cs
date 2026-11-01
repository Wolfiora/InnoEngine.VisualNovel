using System;
using System.IO;
using Inno.Assets;
using Inno.Assets.Pipeline;
using Inno.Core.Identity;
using Inno.Core.Diagnostics;
using Inno.Core.Logging;
using Inno.Core.Serialization;
using Inno.Extensibility.Modules;
using Inno.Extensibility.Types;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Story;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal static class OriginalSampleLibrary
{
    internal static void Write(
        string root,
        StoryNodeDataCodec codec,
        bool refresh,
        ModuleHost modules,
        TypeCatalog types,
        SerializationRegistry serialization,
        IdentityAllocator identities
    ) {
        string sampleRoot = Path.Combine(root, "Assets", "~Samples"); Directory.CreateDirectory(sampleRoot);
        string backupRoot = Path.Combine(root, "artifacts", "SourceBackups", "Tutorials-" + Guid.NewGuid().ToString("N"));
        using var logs = new LogRouter(); using var assets = new AssetPipeline(modules, types, serialization, identities, new DiagnosticHub(), logs,
            AssetPipelineOptions.Create(sampleRoot, Path.Combine(root, "Library", "TutorialResourceWriter")) with { enableFileSystemWatcher = false, deferUnavailableExtensions = true,
                sourcePolicy = new(null, null, null, [".md", ".cs.txt"]) });
        using var scope = AssetExecutionContext.EnterScope(assets);
        foreach (var sample in TutorialSampleCatalog.entries)
        {
            string path = sample.id + "/" + sample.id + ".ivnstory";
            string absolute = Path.GetFullPath(Path.Combine(sampleRoot, path));
            if (!absolute.StartsWith(Path.GetFullPath(sampleRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Tutorial source escaped its owned sample directory.");
            if (refresh || !File.Exists(absolute))
            {
                if (File.Exists(absolute))
                {
                    Directory.CreateDirectory(backupRoot); File.Copy(absolute, Path.Combine(backupRoot, sample.id + ".ivnstory"));
                }
                if (!EditorAssets.Save(AssetPath.Project(path), TutorialSampleCatalog.Create(sample.id, codec).ToAsset())) throw new InvalidOperationException("Tutorial native source commit failed: " + sample.id);
                if (!assets.Import(AssetPath.Project(path))) throw new InvalidOperationException("Tutorial native program refresh failed: " + sample.id);
            }
            var story = Assets.Assets.Load<StoryAsset>(AssetPath.Project(path));
            if (story.isMissing || story.program is null || story.program.instructions.Length < 3)
                throw new InvalidOperationException("Tutorial graph did not compile natively: " + sample.id + " [path=" + story.assetPath + ", missing=" + story.isMissing + ", payload=" + story.runtimePayload.Length + "]");
        }
        Console.WriteLine("Native tutorials: 5 independently compiled authoritative graphs; scripting sample requires explicit documented command registration.");
    }
}
