using System;
using System.IO;
using Inno.Adapter.Modules.DotNet;
using Inno.Adapter.Serialization.DotNet;
using Inno.Assets;
using Inno.Assets.Pipeline;
using Inno.Core.Diagnostics;
using Inno.Core.Identity;
using Inno.Core.IO;
using Inno.Core.Logging;
using Inno.Core.Serialization;
using Inno.Core.Settings;
using Inno.Extensibility.Modules;
using Inno.Extensibility.Types;
using Inno.Scripting.Compiler;
using Inno.Rendering.Assets.Authoring;
using Inno.Build.Toolchains.Bgfx.Tools;

namespace Inno.VisualNovel.Tools.Bootstrap;

internal static class Program
{
    private static int Main(string[] args)
    {
        string root = Path.GetFullPath(args.Length is 1 or 2 or 5 ? args[0] : throw new ArgumentException("Supply the plugin project root."));
        if (args.Length == 5) { HeadlessProjectValidation.Run(root, args[1], args[2], args[3], args[4]); return 0; }
        if (args.Length == 2)
        {
            if (args[1] != "--validate-shaders") throw new ArgumentException("Unknown bootstrap operation.");
            ShaderTargetValidation.Run(root); return 0;
        }
        Directory.CreateDirectory(Path.Combine(root, "Assets"));
        Directory.CreateDirectory(Path.Combine(root, "Plugins"));
        var identities = new IdentityAllocator();
        using IDisposable identityScope = identities.EnterScope();
        using var modules = new ModuleHost(new ModuleHostOptions
        {
            catalogSource = new DotNetAssemblyCatalogSource(typeof(ScriptAssemblyDefinitionAsset).Assembly,
                typeof(ShaderGraphSourceStore).Assembly, typeof(BgfxShaderSourceFrontend).Assembly)
        });
        using var types = new TypeCatalog(modules, new ReflectionTypeCatalogSource());
        using var serialization = new SerializationRegistry(types, new ReflectionSerializationMetadataSource());
        using var logs = new LogRouter();
        var diagnostics = new DiagnosticHub();
        diagnostics.RegisterSink(new BootstrapDiagnostics());
        using var assets = new AssetPipeline(modules, types, serialization, identities, diagnostics, logs,
            AssetPipelineOptions.Create(Path.Combine(root, "Assets"), Path.Combine(root, "Library", "Bootstrap")) with
            {
                enableFileSystemWatcher = false,
                deferUnavailableExtensions = true
            });
        AssetSourceStore sources = assets.CreateSourceStore();
        Write("Model", ScriptAssemblyScope.Runtime, []);
        Write("Compiler", ScriptAssemblyScope.Editor, ["Model"]);
        Write("Narrative", ScriptAssemblyScope.Runtime, ["Model"]);
        Write("Stage", ScriptAssemblyScope.Runtime, ["Model"]);
        Write("UI", ScriptAssemblyScope.Runtime, ["Model"]);
        Write("Player", ScriptAssemblyScope.Runtime, ["Model", "Narrative", "Stage", "UI"]);
        Write("Rendering", ScriptAssemblyScope.Runtime, ["Model", "Stage", "UI"]);
        Write("Integration", ScriptAssemblyScope.Runtime, ["Model", "Player", "Rendering", "Narrative", "Stage", "UI"]);
        Write("Authoring", ScriptAssemblyScope.Editor, ["Model", "Compiler", "Narrative", "Stage", "Player", "UI"]);
        Write("Editor", ScriptAssemblyScope.Editor, ["Model", "Compiler", "Narrative", "Authoring", "Integration", "UI", "Stage", "Player"]);
        using (AssetExecutionContext.EnterScope(assets)) ShaderBootstrap.Ensure(assets, serialization);
        string projectSettings = Path.Combine(root, "Settings.Project.inno");
        if (!File.Exists(projectSettings))
        {
            using var settings = new ProjectSettingsStore(new FileByteDocumentStore(projectSettings), types, serialization,
                new ProjectId("inno.project"), SerializationContext.empty);
            settings.SetProjectOverride(ProjectIdentitySettings.settingId,
                new ProjectIdentitySettings { projectId = "inno.visualnovel" }, []);
        }
        Console.WriteLine("Native assembly definitions prepared through the engine AssetSourceStore.");
        return 0;

        void Write(
            string name,
            ScriptAssemblyScope scope,
            string[] references
        ) {
            string relative = $"Code/{name}/Inno.VisualNovel.{name}.iasmdef";
            string destination = Path.Combine(root, "Assets", relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            string[] qualified = Array.ConvertAll(references, static item => "Inno.VisualNovel." + item);
            byte[] data = sources.Encode(new ScriptAssemblyDefinitionAsset("Inno.VisualNovel." + name, scope, qualified));
            if (!File.Exists(destination) || !File.ReadAllBytes(destination).AsSpan().SequenceEqual(data))
                File.WriteAllBytes(destination, data);
        }
    }
}
