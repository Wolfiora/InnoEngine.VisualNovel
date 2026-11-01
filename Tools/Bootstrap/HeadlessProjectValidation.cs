using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Inno.Adapter.Modules.DotNet;
using Inno.Adapter.Serialization.DotNet;
using Inno.Assets;
using Inno.Assets.Pipeline;
using Inno.Build;
using Inno.Build.Cli;
using Inno.Build.Distribution.Standard;
using Inno.Build.Toolchains;
using Inno.Build.Toolchains.Bgfx.Tools;
using Inno.Core.IO;
using Inno.Core.Identity;
using Inno.Core.Settings;
using Inno.Extensibility.Modules;
using Inno.Plugins.Authoring;
using Inno.Runtime;
using Inno.Scripting.Compiler;
using Inno.Scene;
using Inno.Build.Managed;

namespace Inno.VisualNovel.Tools.Bootstrap;

internal static class HeadlessProjectValidation
{
    internal static void Run(
        string repository,
        string operation,
        string project,
        string output,
        string dotnet
    ) {
        project = Owned(repository, project); output = Owned(repository, output);
        string temporary = Path.Combine(repository, "artifacts", "HeadlessValidation", "Temp");
        Directory.CreateDirectory(temporary);
        Environment.SetEnvironmentVariable("TEMP", temporary); Environment.SetEnvironmentVariable("TMP", temporary);
        Directory.CreateDirectory(Path.Combine(project, "Plugins"));
        string library = Path.Combine(project, "Library"); Directory.CreateDirectory(library);
        using var engine = new EngineHostBuilder().UseMetadataSources(new DotNetAssemblyCatalogSource(typeof(HeadlessProjectValidation).Assembly,
            typeof(BuildApplication).Assembly, typeof(Inno.Text.Assets.FontImporter).Assembly, typeof(Inno.Audio.Assets.AudioClipImporter).Assembly,
            System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(Path.GetDirectoryName(typeof(BuildApplication).Assembly.Location)!, "Inno.Scene.Assets.dll"))),
            new ReflectionTypeCatalogSource(), new ReflectionSerializationMetadataSource()).Build();
        var sources = new PluginSourceService(engine.serialization, Path.Combine(project, "Plugins"), library);
        var scan = sources.Scan();
        var options = AssetPipelineOptions.Create(Path.Combine(project, "Assets"), library);
        using var assets = new AssetPipeline(engine.modules, engine.types, engine.serialization, new IdentityAllocator(), engine.diagnostics, engine.logs,
            options with { enableFileSystemWatcher = false, deferUnavailableExtensions = true,
                sourceMounts = [.. options.sourceMounts!, .. PluginSourceService.GetActivatableMounts(scan)] });
        using var settings = new ProjectSettingsStore(new FileByteDocumentStore(Path.Combine(project, SettingsFileNames.project)),
            engine.types, engine.serialization, ProjectId.FromName(new DirectoryInfo(project).Name), AssetSerializationContext.Create(assets));
        using var plugins = new PluginEnvironment(assets, settings, engine.serialization, Path.Combine(project, "Plugins"), library, scan, engine.modules.generations);
        if (plugins.discovery.diagnostics.Count != 0 || plugins.activePlugins.Count != scan.candidates.Count)
            throw new InvalidOperationException("Native plugin activation failed: " + string.Join(Environment.NewLine, plugins.discovery.diagnostics.Select(static item => item.message)));
        var compiler = new ScriptCompiler(new ScriptCompilerOptions { projectRootDirectory = project,
            compilationEnvironment = new SdkScriptCompilationEnvironment(dotnet, project) }, assets, plugins);
        var result = Compile(compiler);
        if (operation == "--candidate-scripts")
        {
            ExportScripts(result, output); Console.WriteLine("Candidate formal scripts only; native source activation remains unverified."); return;
        }
        using (var reload = engine.modules.BeginReload(result.moduleDeployments.Select(deployment => (IModuleSource)new DotNetModuleSource
        {
            artifactRootDirectory = Path.Combine(library, "Build", "Metadata"), moduleName = deployment.moduleName,
            mainAssemblyPath = deployment.mainAssemblyPath, domain = deployment.domain, scope = deployment.scope,
            preloadAssemblyPaths = deployment.preloadAssemblyPaths, upstreamModuleNames = deployment.upstreamModuleNames,
            assemblyScopes = deployment.assemblyScopes, collectible = true
        }).ToArray())) _ = engine.generations.Execute("VN headless authoring validation", reload, [plugins.CreateReloadChange()]);
        engine.generations.Wait(); assets.CompleteExtensionDiscovery(); assets.Rescan();
        if (operation == "--scripts")
        {
            result = Compile(compiler); compiler.GenerateProjectFiles();
            ExportScripts(result, output); return;
        }
        if (operation is not ("--plugin" or "--game")) throw new ArgumentException("Unknown headless validation operation.");
        string native = Path.Combine(repository, "artifacts", "EngineEditorSnapshot", "native", "bgfx");
        var tools = new ToolRunner(new Dictionary<BgfxTool, string> { [BgfxTool.Shaderc] = Path.Combine(native, "windows-x64", "tools", "shaderc.exe") }, Path.Combine(native, "includes"));
        var context = StandardBuildEnvironment.Capture(AppContext.BaseDirectory, new BuildTargetId("windows-x64"), dotnet);
        var distribution = StandardBuildDistribution.Create(context, tools).build;
        var pipeline = new BuildPipeline(assets, plugins, settings, engine.serialization, engine.generations, compiler,
            Path.Combine(repository, "artifacts", "SupportPacks"), distribution.CreateBindings(assets, engine.serialization, engine.types), distribution.managedDeployments);
        var progress = new Progress<BuildProgress>(static item => Console.WriteLine(item.message));
        BuildResult built;
        if (operation == "--plugin") built = pipeline.BuildPluginAsync(new() { pluginId = settings.projectId.value, displayName = "InnoEngine.VisualNovel", outputPath = output }, progress).AsTask().GetAwaiter().GetResult();
        else
        {
            var startup = assets.GetFileSystemEntries(includeDirectories: false).Where(static entry => entry.source == AssetSourceId.project
                    && !AssetSample.IsRuntimeExcluded(entry.assetPath, isDirectory: false))
                .OrderBy(static entry => entry.assetPath.localPath, StringComparer.Ordinal)
                .FirstOrDefault(entry => assets.TryGetAssetType(entry.assetPath, out Type? type) && type == typeof(SceneAsset))
                ?? throw new InvalidOperationException("The headless build fixture has no native startup scene.");
            var profile = new BuildProfile { applicationId = settings.projectId.value, productName = "VisualNovelAcceptance", target = new("windows-x64"),
                managedDeployment = ManagedDeploymentId.coreClr, startupScene = startup.assetPath.ToString(), windowWidth = 1280, windowHeight = 720 };
            using var pack = pipeline.EnsurePlayerSupportPackAsync(profile.target, profile.managedDeployment).AsTask().GetAwaiter().GetResult();
            built = pipeline.BuildGameAsync(new() { profile = profile, outputDirectory = output }, progress).AsTask().GetAwaiter().GetResult();
        }
        foreach (var diagnostic in built.diagnostics) Console.WriteLine(diagnostic.severity + " " + diagnostic.code + ": " + diagnostic.message);
        if (!built.succeeded) throw new InvalidOperationException("Native publication failed: " + operation);
        Console.WriteLine(built.outputPath);
    }
    private static ScriptCompilationResult Compile(ScriptCompiler compiler)
    {
        var result = compiler.CompileAuthoringGenerationAsync().GetAwaiter().GetResult();
        if (!result.success) throw new InvalidOperationException("Formal scripting API compilation failed: " + Environment.NewLine
            + string.Join(Environment.NewLine, result.diagnostics.Select(static item => item.message)));
        return result;
    }
    private static void ExportScripts(
        ScriptCompilationResult result,
        string output
    ) {
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        using var staging = ProcessOwnedDirectory.CreateAsync(Path.GetDirectoryName(output)!).AsTask().GetAwaiter().GetResult();
        foreach (string assembly in result.moduleDeployments.Where(static item => item.domain == AssemblyDomain.InnoScripting)
            .SelectMany(static item => new[] { item.mainAssemblyPath }.Concat(item.preloadAssemblyPaths)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            File.Copy(assembly, Path.Combine(staging.directory, Path.GetFileName(assembly)));
            string symbols = Path.ChangeExtension(assembly, ".pdb");
            if (File.Exists(symbols)) File.Copy(symbols, Path.Combine(staging.directory, Path.GetFileName(symbols)));
        }
        AtomicDirectory.Install(staging.directory, output); Console.WriteLine(output);
    }
    private static string Owned(
        string repository,
        string path
    ) {
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repository)), result = Path.GetFullPath(path);
        if (result != root && !result.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Headless validation outputs and projects must remain inside the VN repository.");
        return result;
    }
}
