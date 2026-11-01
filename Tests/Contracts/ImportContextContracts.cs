using System;
using System.IO;
using Inno.Assets;
using Inno.Assets.Pipeline;
using Inno.Core.Diagnostics;
using Inno.Core.Identity;
using Inno.Core.Logging;
using Inno.Core.Serialization;
using Inno.Extensibility.Modules;
using Inno.Extensibility.Types;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class ImportContextContracts
{
    internal static void WithoutAmbientAssets(
        string root,
        ModuleHost modules,
        TypeCatalog types,
        SerializationRegistry serialization
    ) {
        var identities = new IdentityAllocator(); using var identityScope = identities.EnterScope();
        using var logs = new LogRouter();
        using var assets = new AssetPipeline(modules, types, serialization, identities, new DiagnosticHub(), logs,
            AssetPipelineOptions.Create(Path.Combine(root, "Assets"), Path.Combine(root, "Library")) with { enableFileSystemWatcher = false, deferUnavailableExtensions = true });
        int count = 0;
        foreach (string source in Directory.EnumerateFiles(Path.Combine(root, "Assets", "Game"), "*.ivnproject", SearchOption.AllDirectories))
        {
            var path = AssetPath.Project(Path.GetRelativePath(Path.Combine(root, "Assets"), source).Replace('\\', '/'));
            if (!assets.Import(path)) throw new InvalidOperationException("Candidate import required an unrelated ambient asset scope or registered duplicate references: " + path);
            count++;
        }
        if (count < 3) throw new InvalidOperationException("The isolated native import fixture lost its blank branching or showcase game.");
        Console.WriteLine("Isolated import without ambient Assets: " + count + " native games; official candidate serialization and dependency collection.");
    }
}
