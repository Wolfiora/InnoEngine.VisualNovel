using System;
using System.IO;
using System.Linq;
using Inno.Adapter.Modules.DotNet;
using Inno.Adapter.Serialization.DotNet;
using Inno.Core.Serialization;
using Inno.Extensibility.Modules;
using Inno.Extensibility.Types;
using Inno.Plugins;
using Inno.Plugins.Authoring;

namespace Inno.VisualNovel.Tools.DistributionCheck;

internal static class Program
{
    private static int Main(string[] args)
    {
        string root = Path.GetFullPath(args.Length == 1 ? args[0] : throw new ArgumentException("Supply the fresh acceptance project."));
        using var modules = new ModuleHost(new ModuleHostOptions { catalogSource = new DotNetAssemblyCatalogSource(typeof(PluginSourceService).Assembly) });
        using var types = new TypeCatalog(modules, new ReflectionTypeCatalogSource());
        using var serialization = new SerializationRegistry(types, new ReflectionSerializationMetadataSource());
        PluginScanResult result = new PluginSourceService(serialization, Path.Combine(root, "Plugins"), Path.Combine(root, "Library")).Scan();
        if (result.diagnostics.Count > 0)
            throw new InvalidDataException(string.Join(Environment.NewLine, result.diagnostics.Select(static item => item.message)));
        PluginCandidate candidate = result.candidates.Single();
        PluginManifest manifest = candidate.manifest;
        if (manifest.pluginId != "inno.visualnovel" || manifest.dependencies.Length != 0 || manifest.overrides.Length != 0)
            throw new InvalidDataException("The plugin identity or independent dependency contract is incorrect.");
        string[] expected = ["Model", "Compiler", "Narrative", "Stage", "UI", "Player", "Rendering", "Integration", "Authoring", "Editor"];
        string[] actual = manifest.assemblyDefinitions.Select(Path.GetFileNameWithoutExtension).Order(StringComparer.Ordinal).ToArray()!;
        if (!actual.SequenceEqual(expected.Select(static item => "Inno.VisualNovel." + item).Order(StringComparer.Ordinal)))
            throw new InvalidDataException("The exported assembly definition set does not match implemented production modules.");
        Console.WriteLine($"Distribution: {manifest.pluginId}; {manifest.assemblyDefinitions.Length} assembly definitions; no plugin dependencies or overrides.");
        Console.WriteLine("Source hash: " + candidate.contentHash);
        return 0;
    }
}
