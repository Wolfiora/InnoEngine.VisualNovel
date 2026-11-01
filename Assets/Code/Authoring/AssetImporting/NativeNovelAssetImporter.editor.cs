using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InnoEngine.Assets;
using InnoEditor.Assets;

namespace Inno.VisualNovel.Authoring.AssetImporting;

/// <summary>Uses the engine's native source and artifact contracts for VN resource assets.</summary>
/// <typeparam name="TAsset">The closed native VN asset contract.</typeparam>
public abstract class NativeNovelAssetImporter<TAsset> : AssetImporter<TAsset> where TAsset : AssetObject
{
    /// <summary>Checks domain invariants before publishing a runtime artifact.</summary>
    /// <param name="asset">The detached imported source.</param>
    protected abstract void Validate(TAsset asset);
    /// <summary>Derives runtime-only content fields from the import candidate.</summary>
    /// <param name="context">The isolated import candidate and dependency services.</param>
    /// <param name="asset">The detached native source.</param>
    /// <returns>The asset to validate and publish.</returns>
    protected virtual TAsset Prepare(
        AssetImportContext context,
        TAsset asset
    ) => asset;
    /// <inheritdoc />
    protected override async ValueTask ImportAsync(
        AssetImportContext context,
        AssetImportWriter<TAsset> output,
        CancellationToken cancellationToken
    ) {
        TAsset asset = NativeAssetSourceSerialization.Import<TAsset>(context.sourceBytes.Span, context.services, out IReadOnlyList<AssetDependency> dependencies);
        foreach (AssetDependency dependency in dependencies)
        {
            AssetPath path = AssetPath.Parse(dependency.lastKnownPath);
            if (path.localPath.Length == 0) throw new InvalidOperationException("A native VN resource reference has no source path: " + context.assetPath + " -> " + dependency.persistentId);
            // Project-relative declarations in an exported package belong to the importing source mount.
            if (path.source == AssetSourceId.project && context.assetPath.source != AssetSourceId.project)
                path = new AssetPath(context.assetPath.source, path.localPath);
            AssetObject resolved = context.ResolveDependency<AssetObject>(path);
            if (resolved.isMissing || resolved.identity.persistentId != dependency.persistentId)
                throw new InvalidOperationException("A native VN resource dependency is missing or has changed identity: " + context.assetPath + " -> " + path);
        }
        asset = Prepare(context, asset);
        Validate(asset);
        output.SetAsset(asset);
        foreach (AssetDependency dependency in dependencies) output.DependsOnAsset(dependency);
        await output.WriteArtifactAsync("runtime", NativeAssetSourceSerialization.Export(asset, context.services), cancellationToken);
    }
    /// <inheritdoc />
    protected override ValueTask<ReadOnlyMemory<byte>?> ExportAsync(
        AssetExportContext context,
        TAsset asset,
        CancellationToken cancellationToken
    ) {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<ReadOnlyMemory<byte>?>(NativeAssetSourceSerialization.Export(asset, context.services));
    }
}
