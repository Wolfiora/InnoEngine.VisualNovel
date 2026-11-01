using System;
using System.IO;
using InnoEngine.Assets;
using InnoEngine.Audio;

namespace Inno.VisualNovel.Authoring.VoiceManagement;

/// <summary>Reads native audio artifacts and writes only new project-owned WAVE resources.</summary>
public static class NativeWaveformSource
{
    /// <summary>Reads a WAV artifact through the engine lease without guessing library or cache paths.</summary>
    /// <param name="clip">A canonical imported WAVE clip.</param>
    /// <returns>Immutable offline editing data.</returns>
    public static WaveAudioContent Read(AudioClipAsset clip)
    {
        if (clip.isMissing || clip.metadata is not { } metadata || metadata.codec != AudioCodecId.wav) throw new InvalidOperationException("Select an imported WAV clip for waveform editing.");
        using var lease = Assets.AcquireArtifact(clip.identity.persistentId, "audio-data");
        if (lease.info.length > 64 * 1024 * 1024) throw new InvalidOperationException("Waveform editing is limited to 64 MiB per clip.");
        using Stream stream = lease.OpenRead();
        var content = WaveAudioContent.Read(stream);
        if (content.channels != metadata.channels || content.sampleRate != metadata.sampleRate || content.frameCount != metadata.frameCount)
            throw new InvalidOperationException("Audio metadata differs from its immutable artifact; reimport the source before editing.");
        return content;
    }
    /// <summary>Creates an independent trimmed clip under Assets/Game/Audio without overwriting the source.</summary>
    /// <param name="projectDirectory">The active host project directory.</param>
    /// <param name="content">The immutable source to crop.</param>
    /// <param name="start">The inclusive start time.</param>
    /// <param name="end">The exclusive end time.</param>
    /// <returns>The newly imported native clip.</returns>
    public static AudioClipAsset CreateTrimmedClip(
        string projectDirectory,
        WaveAudioContent content,
        double start,
        double end
    ) {
        byte[] bytes = content.Trim(start, end);
        AssetPath path = AssetPath.Project("Game/Audio/Trimmed-" + Guid.NewGuid().ToString("N") + ".wav");
        string directory = Path.GetFullPath(Path.Combine(projectDirectory, "Assets", "Game", "Audio"));
        Directory.CreateDirectory(directory);
        string destination = Path.Combine(directory, Path.GetFileName(path.localPath));
        using (var file = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { file.Write(bytes); file.Flush(true); }
        AudioClipAsset imported = Assets.Load<AudioClipAsset>(path);
        if (imported.isMissing) throw new InvalidOperationException("The trimmed WAV was created but native import failed: " + path);
        return imported;
    }
}
