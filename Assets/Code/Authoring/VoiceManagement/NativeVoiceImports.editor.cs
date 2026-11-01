using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using InnoEngine.Assets;
using InnoEngine.Audio;
using InnoEditor.Assets;
using Inno.VisualNovel.Model.Audio;
using Inno.VisualNovel.Model.Project;

namespace Inno.VisualNovel.Authoring.VoiceManagement;

/// <summary>Uses native imports for a bounded explicit recording folder and reusable Voice cue creation.</summary>
public static class NativeVoiceImports
{
    /// <summary>Loads recordings from an explicitly chosen project-relative asset folder without creating an index.</summary>
    /// <param name="projectDirectory">The active host project root.</param>
    /// <param name="folder">A source-relative folder below Assets, excluding the Assets prefix.</param>
    /// <returns>Canonical clips accepted by the engine's current WAV, MP3 or FLAC importer.</returns>
    public static IReadOnlyList<AudioClipAsset> ReadFolder(
        string projectDirectory,
        string folder
    ) {
        string local = AssetPath.Project(folder).localPath;
        if (local.Length == 0) throw new InvalidOperationException("Choose a recording folder instead of scanning the entire asset project.");
        string root = Path.GetFullPath(Path.Combine(projectDirectory, "Assets")) + Path.DirectorySeparatorChar;
        string directory = Path.GetFullPath(Path.Combine(root, local));
        if (!directory.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(directory)) throw new InvalidOperationException("The recording folder must exist inside this project's Assets.");
        var paths = Directory.EnumerateFiles(directory, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, MaxRecursionDepth = 32 })
            .Where(static path => Path.GetExtension(path).ToLowerInvariant() is ".wav" or ".mp3" or ".flac").Take(10001).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        if (paths.Length > 10000) throw new InvalidOperationException("Recording import is limited to 10000 files per batch.");
        return Array.AsReadOnly(paths.Select(path => Assets.Load<AudioClipAsset>(AssetPath.Project(Path.GetRelativePath(root, path)))).ToArray());
    }
    /// <summary>Validates all reviewed clip generations before creating any new native cue source.</summary>
    /// <param name="project">The current explicit game library.</param>
    /// <param name="preview">The reviewed clip matches.</param>
    /// <returns>Canonical non-looping Voice cues for ready rows; existing imported cues are never overwritten.</returns>
    public static IReadOnlyList<AudioCueAsset> PrepareCues(
        NovelProjectAsset project,
        VoiceBatchPreview preview
    ) {
        var pending = new List<(VoiceBatchRow row, AudioClipAsset clip, AudioCueAsset? existing)>();
        foreach (var row in preview.rows.Where(static item => item.applicable).DistinctBy(static item => item.cue))
        {
            AudioClipAsset clip = Assets.Load<AudioClipAsset>(row.clipId);
            if (clip.isMissing || clip.contentVersion != row.clipVersion || clip.assetPath.ToString() != row.clipPath || clip.duration <= TimeSpan.Zero)
                throw new InvalidOperationException("Recording changed after review; rebuild the preview: " + row.clipPath);
            AudioCueAsset? existing = project.audio.SingleOrDefault(item => item.resourceId == row.cue);
            if (existing is not null && (existing.isMissing || existing.channel != NovelAudioChannel.Voice || existing.loop || existing.clip?.identity.persistentId != row.clipId))
                throw new InvalidOperationException("The game library changed to an incompatible Voice cue: " + row.cue);
            pending.Add((row, clip, existing));
        }
        var result = new List<AudioCueAsset>();
        foreach (var item in pending)
        {
            if (item.existing is not null) { result.Add(item.existing); continue; }
            AssetPath path = AssetPath.Project("Game/Audio/Voice-" + Guid.NewGuid().ToString("N") + ".ivnaudio");
            if (!EditorAssets.Save(path, new AudioCueAsset { resourceId = item.row.cue, title = item.clip.name, clip = item.clip, channel = NovelAudioChannel.Voice }))
                throw new InvalidOperationException("Native Voice cue import failed: " + path);
            result.Add(Assets.Load<AudioCueAsset>(path));
        }
        return result.AsReadOnly();
    }
    /// <summary>Writes a recording script or reviewed match manifest to project-owned artifacts.</summary>
    /// <param name="projectDirectory">The active host project.</param>
    /// <param name="text">The quoted CSV/TSV exchange text.</param>
    /// <param name="separator">Comma or tab, selecting the extension.</param>
    /// <returns>The absolute exported path.</returns>
    public static string Export(
        string projectDirectory,
        string text,
        char separator
    ) {
        if (separator is not (',' or '\t')) throw new ArgumentException("Use CSV or TSV.", nameof(separator));
        string directory = Path.GetFullPath(Path.Combine(projectDirectory, "artifacts", "Exports")); Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "Voice-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + (separator == ',' ? ".csv" : ".tsv"));
        File.WriteAllText(path, text, new System.Text.UTF8Encoding(false)); return path;
    }
}
