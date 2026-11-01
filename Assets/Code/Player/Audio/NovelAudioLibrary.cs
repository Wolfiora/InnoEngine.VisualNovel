using System;
using System.Linq;
using Inno.VisualNovel.Model.Audio;
using Inno.VisualNovel.Model.Project;

namespace Inno.VisualNovel.Player.Audio;

/// <summary>Resolves explicit game cues and optional title ambience into one owner-local playback library.</summary>
public static class NovelAudioLibrary
{
    /// <summary>Identifies the private title slot resource, reserved only when title ambience is assigned.</summary>
    public const string titleCueId = "inno.vn.title-ambience";
    /// <summary>Creates cue declarations consumed consistently by policy and the host audio adapter.</summary>
    /// <param name="project">The imported game and explicit audio roots.</param>
    /// <returns>A new library array; imported clip references remain generation-owned engine assets.</returns>
    public static AudioCueAsset[] Create(NovelProjectAsset project)
    {
        if (project.titleAudio is null) return project.audio.ToArray();
        if (project.titleAudio.isMissing || project.audio.Any(static cue => cue.resourceId == titleCueId))
            throw new InvalidOperationException("Title ambience is missing or its reserved cue identity conflicts with the game library.");
        return [.. project.audio, new() { resourceId = titleCueId, title = project.title, clip = project.titleAudio,
            channel = NovelAudioChannel.Bgm, volume = 0.5, loop = true, fadeIn = 0.5, fadeOut = 0.5 }];
    }
}
