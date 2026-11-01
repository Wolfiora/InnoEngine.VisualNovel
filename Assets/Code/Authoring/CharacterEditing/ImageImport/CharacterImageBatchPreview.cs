using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Rendering;
using InnoEngine.Assets;
using Inno.VisualNovel.Model.Characters;

namespace Inno.VisualNovel.Authoring.CharacterEditing.ImageImport;

/// <summary>Owns an unforgeable frozen character replacement tied to one source and imported image generations.</summary>
public sealed class CharacterImageBatchPreview
{
    private readonly CharacterAsset m_candidate;
    private readonly CharacterImageAssetStamp[] m_images;
    internal CharacterImageBatchPreview(
        string baseline,
        IEnumerable<CharacterImageBatchRow> rows,
        CharacterAsset candidate,
        IEnumerable<TextureAsset> images,
        bool createVariants,
        bool resize
    ) {
        this.baseline = baseline; this.rows = Array.AsReadOnly(rows.ToArray()); m_candidate = CharacterAssetCopy.Copy(candidate);
        this.createVariants = createVariants; this.resize = resize;
        m_images = images.DistinctBy(static item => item.identity.persistentId).Select(static item =>
            new CharacterImageAssetStamp(item.identity.persistentId, item.contentVersion, item.assetPath.ToString())).ToArray();
    }
    /// <summary>Gets the native authored-property fingerprint required before committing.</summary>
    public string baseline { get; }
    /// <summary>Gets all matched and rejected rows in stable source-path order.</summary>
    public IReadOnlyList<CharacterImageBatchRow> rows { get; }
    /// <summary>Gets whether the review permits creating named variants.</summary>
    public bool createVariants { get; }
    /// <summary>Gets whether pose geometry adopts replacement image dimensions.</summary>
    public bool resize { get; }
    /// <summary>Gets the number of individually ready rows in the reviewed atomic subset.</summary>
    public int readyCount => rows.Count(static item => item.applicable);
    /// <summary>Creates an independent visual preview; this copy is never authority for a later commit.</summary>
    /// <returns>A detached candidate borrowing only canonical image resources.</returns>
    public CharacterAsset CopyForDisplay() => CharacterAssetCopy.Copy(m_candidate);

    internal CharacterAsset Prepare(CharacterAsset current)
    {
        if (readyCount == 0) throw new InvalidOperationException("No reviewed image rows are ready to apply.");
        if (CharacterImageBatchService.Fingerprint(current) != baseline) throw new InvalidOperationException("Character changed after review; rebuild the image preview.");
        foreach (var stamp in m_images)
        {
            TextureAsset image = Assets.Load<TextureAsset>(stamp.id);
            if (image.isMissing || image.identity.persistentId != stamp.id || image.contentVersion != stamp.version || image.assetPath.ToString() != stamp.path)
                throw new InvalidOperationException("Character image changed after review; prepare the batch again: " + stamp.path);
        }
        return CharacterAssetCopy.Copy(m_candidate);
    }
    private sealed record CharacterImageAssetStamp(Guid id, long version, string path);
}
