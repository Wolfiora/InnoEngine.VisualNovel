using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using InnoEngine.Assets;
using InnoEditor.Assets;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Authoring.AssetImporting;

namespace Inno.VisualNovel.Authoring.Publishing;

/// <summary>Hashes explicit content roots and their transitive native asset references during import.</summary>
public static class ProjectContentFingerprint
{
    /// <summary>Derives a conservative exact-build identity and declares source invalidation dependencies.</summary>
    /// <param name="context">The generation-bound import context.</param>
    /// <param name="project">The detached source with its referenced imported assets.</param>
    /// <returns>An uppercase SHA-256 fingerprint of source bytes and imported properties.</returns>
    public static string Compute(
        AssetImportContext context,
        NovelProjectAsset project
    ) {
        var visited = new HashSet<Guid> { context.persistentId };
        var hashes = new SortedDictionary<Guid, byte[]>();
        var pending = new Queue<(AssetDependency dependency, string parent)>();
        // Inspection must not register each transitive reference again in the import context.
        // The controlled resolver below owns dependency declaration and source invalidation.
        foreach (var dependency in ImportAssetInspection.Capture(project, context.services).dependencies) pending.Enqueue((dependency, context.assetPath.ToString()));
        while (pending.TryDequeue(out var item))
        {
            AssetDependency dependency = item.dependency;
            if (!visited.Add(dependency.persistentId)) continue;
            AssetPath path = AssetPath.Parse(dependency.lastKnownPath);
            if (path.localPath.Length == 0) throw new InvalidOperationException("Content reference has no native source path: " + item.parent + " -> " + dependency.persistentId);
            AssetObject asset = context.ResolveDependency<AssetObject>(path);
            if (asset.isMissing || asset.identity.persistentId != dependency.persistentId) throw new InvalidOperationException("A content dependency is missing or has changed identity: " + path);
            var properties = ImportAssetInspection.Capture(asset, context.services);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            hash.AppendData(SHA256.HashData(context.ReadSourceBytes(path).Span));
            hash.AppendData(SHA256.HashData(properties.data));
            hashes.Add(dependency.persistentId, hash.GetHashAndReset());
            foreach (AssetDependency child in properties.dependencies) pending.Enqueue((child, path.ToString()));
            if (visited.Count > 100000) throw new InvalidOperationException("Content closure exceeds the 100000-asset safety budget.");
        }
        using var result = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        result.AppendData(SHA256.HashData(context.sourceBytes.Span));
        foreach (var item in hashes)
        {
            result.AppendData(item.Key.ToByteArray());
            result.AppendData(item.Value);
        }
        foreach (var chapter in project.chapters.OrderBy(static item => item.chapterId, StringComparer.Ordinal))
            result.AppendData(SHA256.HashData(Encoding.UTF8.GetBytes(chapter.program!.fingerprint)));
        return Convert.ToHexString(result.GetHashAndReset());
    }
}
