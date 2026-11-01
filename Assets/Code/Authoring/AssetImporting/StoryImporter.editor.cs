using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InnoEngine.Assets;
using InnoEngine.Serialization;
using InnoEditor.Assets;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Story;

namespace Inno.VisualNovel.Authoring.AssetImporting;

/// <summary>Imports native story sources and publishes only successfully compiled runtime content.</summary>
[AssetImporter("inno.vn.story")]
public sealed class StoryImporter : AssetImporter<StoryAsset>
{
    /// <inheritdoc />
    public override IReadOnlyList<string> supportedExtensions { get; } = [".ivnstory"];
    /// <inheritdoc />
    protected override async ValueTask ImportAsync(
        AssetImportContext context,
        AssetImportWriter<StoryAsset> output,
        CancellationToken cancellationToken
    ) {
        StoryAsset source = NativeAssetSourceSerialization.Import<StoryAsset>(context.sourceBytes.Span, context.services, out IReadOnlyList<AssetDependency> dependencies);
        if (source.variableTable is { } table)
        {
            AssetDependency dependency = dependencies.Single(item => item.persistentId == table.identity.persistentId);
            AssetPath path = AssetPath.Parse(dependency.lastKnownPath);
            if (path.source == AssetSourceId.project && context.assetPath.source != AssetSourceId.project) path = new(context.assetPath.source, path.localPath);
            source.variableTable = context.ResolveDependency<Model.Values.VariableTableAsset>(path);
            if (source.variableTable.isMissing || source.variableTable.identity.persistentId != dependency.persistentId)
                throw new InvalidOperationException("The shared variable library is missing or has changed native identity: " + path);
            context.DependsOnSource(path);
            context.DependsOnArtifact(dependency.persistentId);
        }
        StoryCompilationResult compiled = new StoryCompiler(new ImportStorySerialization(context.services)).Compile(source);
        if (!compiled.success)
            throw new InvalidOperationException(string.Join(Environment.NewLine, compiled.diagnostics.Select(static item => $"{item.code} [{item.nodeId}]: {item.message}")));
        foreach (AssetDependency dependency in dependencies)
            output.DependsOnAsset(dependency);
        var runtime = new StoryAsset { chapterId = source.chapterId, title = source.title, program = compiled.program, variableTable = source.variableTable };
        output.SetAsset(runtime);
        await output.WriteArtifactAsync("runtime", NativeAssetSourceSerialization.Export(runtime, context.services), cancellationToken);
    }

    /// <inheritdoc />
    protected override ValueTask<ReadOnlyMemory<byte>?> ExportAsync(
        AssetExportContext context,
        StoryAsset asset,
        CancellationToken cancellationToken
    ) {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<ReadOnlyMemory<byte>?>(NativeAssetSourceSerialization.Export(asset, context.services));
    }
}
