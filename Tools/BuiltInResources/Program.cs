using System;
using System.IO;
using System.Linq;
using Inno.Adapter.Modules.DotNet;
using Inno.Adapter.Serialization.DotNet;
using Inno.Assets;
using Inno.Assets.Pipeline;
using Inno.Audio;
using Inno.Audio.Assets;
using Inno.Core.Diagnostics;
using Inno.Core.Identity;
using Inno.Core.Logging;
using Inno.Core.Serialization;
using Inno.Extensibility.Modules;
using Inno.Extensibility.Types;
using Inno.VisualNovel.Authoring.AssetImporting;
using Inno.VisualNovel.Model.Audio;
using Inno.VisualNovel.Model.Performance;
using Inno.Rendering.Assets;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal static class Program
{
    private static int Main(string[] args)
    {
        string root = Path.GetFullPath(args.Length is >= 1 and <= 7 ? args[0] : throw new ArgumentException("Supply the VN root and optional owned resource refresh flags."));
        if (args.Length == 2 && args[1] == "--inventory-editor-text") { EditorTextInventory.Write(root); return 0; }
        if (args.Length == 2 && args[1] == "--review-editor-english") { EditorEnglishText.Review(root); return 0; }
        if (args.Length == 2 && args[1] == "--wire-editor-text") { EditorTextSourceWiring.Apply(root); return 0; }
        if (args.Length == 2 && args[1] == "--wire-authoring-diagnostics") { AuthoringDiagnosticWiring.Apply(root); return 0; }
        if (args.Skip(1).Any(static option => option is not ("--refresh-sample" or "--refresh-motions" or "--refresh-themes" or "--refresh-characters" or "--refresh-stages" or "--refresh-effects" or "--refresh-editor-text"))) throw new ArgumentException("Unknown resource writer option.");
        bool editorTextOnly = args.Length == 2 && args[1] == "--refresh-editor-text";
        bool refreshMotions = args.Contains("--refresh-motions", StringComparer.Ordinal);
        string motionBackup = Path.Combine(root, "artifacts", "SourceBackups", "MotionCurves-" + Guid.NewGuid().ToString("N"));
        if (!editorTextOnly) { OriginalAudioWriter.WriteSources(root); OriginalParticleSprites.Write(root); OriginalVisualLibrary.WriteSources(root); }
        var identities = new IdentityAllocator();
        using var identityScope = identities.EnterScope();
        using var modules = new ModuleHost(new ModuleHostOptions { catalogSource = new DotNetAssemblyCatalogSource(typeof(Program).Assembly,
            typeof(AudioClipImporter).Assembly, typeof(AudioCueImporter).Assembly, typeof(AudioCueAsset).Assembly,
            typeof(Inno.Rendering.Assets.Authoring.ShaderGraphSourceStore).Assembly, typeof(Inno.Text.Assets.FontImporter).Assembly) });
        _ = modules.Register("visualnovel.resource-runtime", [typeof(AudioCueAsset).Assembly, typeof(Inno.VisualNovel.Stage.Timeline.PerformancePlayer).Assembly,
            typeof(Inno.VisualNovel.Narrative.Execution.StoryMachine).Assembly, typeof(Inno.VisualNovel.Player.Sessions.NovelSession).Assembly,
            typeof(Inno.VisualNovel.UI.Documents.UiDocumentInstance).Assembly]);
        _ = modules.Register("visualnovel.resource-authoring", [typeof(AudioCueImporter).Assembly,
            typeof(Inno.VisualNovel.Compiler.StoryCompiler).Assembly]);
        _ = modules.Register("visualnovel.resource-font-import", [typeof(Inno.Text.Assets.FontImporter).Assembly]);
        using var types = new TypeCatalog(modules, new ReflectionTypeCatalogSource());
        using var serialization = new SerializationRegistry(types, new ReflectionSerializationMetadataSource());
        using var logs = new LogRouter();
        var diagnostics = new DiagnosticHub();
        diagnostics.RegisterSink(new ResourceDiagnostics());
        var codec = new Inno.VisualNovel.Compiler.StoryNodeDataCodec(new Inno.VisualNovel.Compiler.EditorStorySerialization(serialization));
        using (var assets = new AssetPipeline(modules, types, serialization, identities, diagnostics, logs,
            AssetPipelineOptions.Create(Path.Combine(root, "Assets"), Path.Combine(root, "Library", "BuiltInResourceWriter")) with
            { enableFileSystemWatcher = false, deferUnavailableExtensions = true, sourcePolicy = new(null, ["Code", "Documentation", "~Samples"], null, null) })) {
            using var scope = AssetExecutionContext.EnterScope(assets);
            if (args.Contains("--refresh-editor-text", StringComparer.Ordinal))
            {
                var writer = new OwnedSourceRefresh(root, "BuiltIn/Editor", true);
                var table = EditorEnglishText.Create(root);
                const string path = "BuiltIn/Editor/EditorText.ivnlocale";
                writer.Save(path, table);
                var loaded = Assets.Assets.Load<Inno.VisualNovel.Model.Localization.LocalizationTableAsset>(AssetPath.Project(path));
                if (loaded.isMissing || loaded.entries.Length != table.entries.Length) throw new InvalidOperationException("Native editor language resource did not reload completely.");
                _ = new Inno.VisualNovel.Editor.Localization.EditorTextCatalog(loaded, "en");
                Console.WriteLine("Native editor text imported and reloaded: " + loaded.entries.Length + " reviewed source/English pairs.");
                if (editorTextOnly) return 0;
            }
            int preparedLeaves = 0;
            foreach (string leaf in Directory.EnumerateFiles(Path.Combine(root, "Assets", "BuiltIn"), "*", SearchOption.AllDirectories)
                .Where(static path => Path.GetExtension(path).ToLowerInvariant() is ".png" or ".wav" or ".otf").OrderBy(static path => path, StringComparer.Ordinal))
            {
                var path = AssetPath.Project(Path.GetRelativePath(Path.Combine(root, "Assets"), leaf).Replace('\\', '/'));
                var loaded = Assets.Assets.Load<AssetObject>(path);
                if (loaded.assetPath.localPath.Length > 0 && !loaded.isMissing) continue;
                if (!assets.Import(path)) throw new InvalidOperationException("The native resource writer could not prepare its leaf dependency: " + path);
                preparedLeaves++;
            }
            Console.WriteLine("Native resource writer explicitly prepared " + preparedLeaves + " leaf dependencies through the public host importer.");
            bool refreshEffects = args.Contains("--refresh-effects", StringComparer.Ordinal);
            var particleSources = new OwnedSourceRefresh(root, "BuiltIn/Particles", refreshEffects);
            var transitionSources = new OwnedSourceRefresh(root, "BuiltIn/Transitions", refreshEffects);
            var screenSources = new OwnedSourceRefresh(root, "BuiltIn/ScreenEffects", refreshEffects);
            var fontPath = AssetPath.Project("BuiltIn/Fonts/NotoSansCJKsc-Regular.otf");
            var font = Assets.Assets.Load<Inno.Text.FontAsset>(fontPath);
            if (font.metadata is null || font.assetPath.localPath.Length == 0)
            {
                Console.WriteLine("Preparing the full resource-corpus font through the public native importer before resource authoring.");
                if (!assets.Import(fontPath)) throw new InvalidOperationException("The selected native font importer could not prepare the resource writer font.");
            }
            foreach (var sound in OriginalAudioWriter.sounds)
            {
                string path = "BuiltIn/Audio/" + sound.directory + "/" + sound.name;
                AudioClipAsset clip = Assets.Assets.Load<AudioClipAsset>(AssetPath.Project(path + ".wav"));
                Write(path + ".ivnaudio", new AudioCueAsset { resourceId = "builtin.audio." + sound.name.ToLowerInvariant(), title = sound.title,
                    author = "InnoEngine.VisualNovel · 原创合成", channel = sound.loop ? NovelAudioChannel.Bgm : NovelAudioChannel.Ui,
                    clip = clip, loop = sound.loop, volume = sound.loop ? 0.45 : 0.35, fadeIn = sound.loop ? 0.5 : 0, fadeOut = sound.loop ? 0.5 : 0.02 });
            }
            foreach (PerformanceClipAsset clip in OriginalMotionLibrary.Create())
            {
                string path = "BuiltIn/Motions/" + clip.resourceId["builtin.motion.".Length..] + ".ivnclip";
                if (refreshMotions)
                {
                    string source = Path.GetFullPath(Path.Combine(root, "Assets", path));
                    string boundary = Path.GetFullPath(Path.Combine(root, "Assets", "BuiltIn", "Motions")) + Path.DirectorySeparatorChar;
                    if (!source.StartsWith(boundary, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Motion regeneration escaped its exact built-in source boundary.");
                    if (File.Exists(source)) { Directory.CreateDirectory(motionBackup); File.Copy(source, Path.Combine(motionBackup, Path.GetFileName(source))); }
                    if (!EditorAssets.Save(AssetPath.Project(path), clip)) throw new InvalidOperationException("Fresh native motion declaration did not import: " + path);
                }
                else if (File.Exists(Path.Combine(root, "Assets", path)) && clip.resourceId is "builtin.motion.shake" or "builtin.motion.camera-shake")
                {
                    if (!EditorAssets.Save(AssetPath.Project(path), clip)) throw new InvalidOperationException("Motion update failed: " + path);
                }
                else Write(path, clip);
                var imported = Assets.Assets.Load<PerformanceClipAsset>(AssetPath.Project(path));
                if (imported.isMissing || imported.resourceId != clip.resourceId) throw new InvalidOperationException("Motion identity verification failed: " + path);
                _ = new Inno.VisualNovel.Stage.Timeline.PerformancePlayer(imported);
            }
            foreach (var sound in OriginalAudioWriter.sounds)
            {
                string path = "BuiltIn/Audio/" + sound.directory + "/" + sound.name;
                var cue = Assets.Assets.Load<AudioCueAsset>(AssetPath.Project(path + ".ivnaudio"));
                if (cue.isMissing || cue.clip is null || cue.clip.isMissing || cue.clip.duration.TotalSeconds <= 0)
                    throw new InvalidOperationException("Audio cue dependency verification failed: " + path);
            }
            var particlePresets = OriginalParticleLibrary.Create();
            for (int index = 0; index < particlePresets.Count; index++)
            {
                VisualEffectAsset preset = particlePresets[index];
                string name = OriginalParticleSprites.names[index];
                preset.particles.texture = Assets.Assets.Load<TextureAsset>(AssetPath.Project("BuiltIn/Particles/Sprites/" + name + ".png"));
                string path = "BuiltIn/Particles/" + name + ".ivneffect";
                particleSources.Save(path, preset);
                var imported = Assets.Assets.Load<VisualEffectAsset>(AssetPath.Project(path));
                if (imported.isMissing || imported.resourceId != preset.resourceId || imported.particles.texture is null || imported.particles.texture.isMissing)
                    throw new InvalidOperationException("Particle preset or sprite dependency verification failed: " + path);
                _ = new Inno.VisualNovel.Stage.Effects.ParticleSimulator(imported);
            }
            foreach (var preset in OriginalImageEffects.Transitions()) VerifyEffect("BuiltIn/Transitions/" + preset.resourceId["builtin.transition.".Length..] + ".ivneffect", preset);
            foreach (var preset in OriginalImageEffects.Screens()) VerifyEffect("BuiltIn/ScreenEffects/" + preset.resourceId["builtin.screen.".Length..] + ".ivneffect", preset);
            OriginalThemeLibrary.Write(root, args.Contains("--refresh-themes", StringComparer.Ordinal));
            OriginalVisualLibrary.Import(root, args.Contains("--refresh-characters", StringComparer.Ordinal), args.Contains("--refresh-stages", StringComparer.Ordinal));
            OriginalCatalogValidation.Run();
            Console.WriteLine("Built-ins: 20 UI sounds, 3 music beds, 20 motions, 8 particles, 12 transitions and 10 screen effects.");

            void VerifyEffect(string path, VisualEffectAsset preset)
            {
                (preset.kind == VisualEffectKind.Screen ? screenSources : transitionSources).Save(path, preset);
                var imported = Assets.Assets.Load<VisualEffectAsset>(AssetPath.Project(path));
                if (imported.isMissing || imported.kind != preset.kind || imported.resourceId != preset.resourceId) throw new InvalidOperationException("Image preset import verification failed: " + path);
                if (imported.kind == VisualEffectKind.Screen) VisualEffectRules.Validate(imported.screen); else VisualEffectRules.Validate(imported.transition);
            }

            void Write<T>(string path, T source) where T : AssetObject
            {
                if (File.Exists(Path.Combine(root, "Assets", path))) return;
                if (!EditorAssets.Save(AssetPath.Project(path), source)) throw new InvalidOperationException("Native built-in import failed: " + path);
            }
        }
        OriginalSampleLibrary.Write(root, codec, args.Contains("--refresh-sample", StringComparer.Ordinal), modules, types, serialization, identities);
        return 0;
    }
}
