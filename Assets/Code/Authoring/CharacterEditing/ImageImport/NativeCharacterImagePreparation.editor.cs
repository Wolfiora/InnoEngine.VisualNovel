using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using InnoEngine.Assets;
using InnoEngine.Rendering;

namespace Inno.VisualNovel.Authoring.CharacterEditing.ImageImport;

/// <summary>Prepares a finite explicit project folder through native image loading in caller-budgeted steps.</summary>
public sealed class NativeCharacterImagePreparation : IDisposable
{
    private readonly string m_root, m_directory;
    private readonly string[] m_paths;
    private readonly List<CharacterImageBatchInput> m_inputs = [];
    private bool m_disposed;
    /// <summary>Captures at most 512 image filenames in one nonrecursive source folder without loading them yet.</summary>
    /// <param name="projectDirectory">The active host project directory.</param>
    /// <param name="folder">An explicit Assets-relative folder, excluding hidden and sample subtrees.</param>
    public NativeCharacterImagePreparation(
        string projectDirectory,
        string folder
    ) {
        string local = AssetPath.Project(folder).localPath;
        if (local.Length == 0 || local.Split('/').Any(static segment => segment.StartsWith('~') || segment.StartsWith('.')))
            throw new InvalidOperationException("Choose an ordinary folder inside Assets rather than the root, hidden content or samples.");
        m_root = Path.GetFullPath(Path.Combine(projectDirectory, "Assets")); m_directory = Path.GetFullPath(Path.Combine(m_root, local));
        VerifyDirectory();
        string[] entries = Directory.EnumerateFiles(m_directory, "*", new EnumerationOptions { RecurseSubdirectories = false, AttributesToSkip = FileAttributes.ReparsePoint }).Take(4097).ToArray();
        if (entries.Length > 4096) throw new InvalidOperationException("Split this image folder; one preparation scans at most 4096 direct file entries.");
        m_paths = entries.Where(static path => Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg")
            .Order(StringComparer.Ordinal).ToArray();
        if (m_paths.Length > 512) throw new InvalidOperationException("Prepare at most 512 images per character batch.");
    }
    /// <summary>Gets the number of captured image filenames.</summary>
    public int total => m_paths.Length;
    /// <summary>Gets the number of attempted native preparations.</summary>
    public int processed { get; private set; }
    /// <summary>Gets whether the preparation reached a finite end or a source-folder failure.</summary>
    public bool finished => processed == total || error.Length > 0;
    /// <summary>Gets the preparation-level source failure, separate from individual image failures.</summary>
    public string error { get; private set; } = string.Empty;
    /// <summary>Gets an independent immutable snapshot of all attempted inputs before disposal.</summary>
    public IReadOnlyList<CharacterImageBatchInput> inputs => Array.AsReadOnly(m_inputs.ToArray());

    /// <summary>Loads at most the requested number of images without a background task or hidden asset index.</summary>
    /// <param name="budget">A positive import budget capped at 64 per call.</param>
    public void Step(int budget = 4)
    {
        ObjectDisposedException.ThrowIf(m_disposed, this);
        if (budget is < 1 or > 64) throw new ArgumentOutOfRangeException(nameof(budget));
        if (finished) return;
        try { VerifyDirectory(); }
        catch (Exception failure) when (failure is not OutOfMemoryException) { error = failure.Message; return; }
        while (!finished && budget-- > 0)
        {
            string absolute = m_paths[processed++]; AssetPath path = AssetPath.Project(Path.GetRelativePath(m_root, absolute).Replace('\\', '/'));
            try
            {
                if ((File.GetAttributes(absolute) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("The captured image became a link; choose an ordinary project source.");
                TextureAsset image = Assets.Load<TextureAsset>(path);
                string failure = image.isMissing || image.identity.persistentId == Guid.Empty || image.assetPath.localPath.Length == 0 || image.width <= 0 || image.height <= 0
                    ? "原生图像未就绪；请在 File Browser 完成导入后重新准备。" : "";
                m_inputs.Add(new(path, image, failure));
            }
            catch (Exception failure) when (failure is not OutOfMemoryException) { m_inputs.Add(new(path, null, failure.Message)); }
        }
    }
    /// <summary>Stops future preparation and releases borrowed input references; no owned file streams remain open.</summary>
    public void Dispose() { m_disposed = true; m_inputs.Clear(); }

    private void VerifyDirectory()
    {
        if (!m_directory.StartsWith(m_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(m_directory))
            throw new InvalidOperationException("The character image folder must exist strictly inside the active project's Assets.");
        DirectoryInfo? current = new(m_directory);
        while (current is not null)
        {
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Character image preparation does not follow directory links.");
            if (string.Equals(current.FullName, m_root, StringComparison.OrdinalIgnoreCase)) return;
            current = current.Parent;
        }
        throw new InvalidOperationException("Character image folder escaped its native project source boundary.");
    }
}
