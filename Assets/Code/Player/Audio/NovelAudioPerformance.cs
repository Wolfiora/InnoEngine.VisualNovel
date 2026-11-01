using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Programs;
using Inno.VisualNovel.Stage.Composition;

namespace Inno.VisualNovel.Player.Audio;

internal static class NovelAudioPerformance
{
    private const string prefix = "audio/";
    internal static IReadOnlyDictionary<string, PerformanceProperty[]> Targets(StoryProgram program) => program.instructions
        .Where(static instruction => instruction.audio is not null).Select(static instruction => instruction.audio!.slot)
        .Concat(["bgm", "music", "ambience", "voice", "sfx", "ui", "title", "gallery"]).Distinct(StringComparer.Ordinal)
        .ToDictionary(static slot => prefix + slot, static _ => new[] { PerformanceProperty.Volume }, StringComparer.Ordinal);
    internal static IReadOnlyDictionary<string, double> Mix(StageController stage) => stage.performances.Evaluate()
        .Where(static pair => pair.Key.property == PerformanceProperty.Volume && pair.Key.target.StartsWith(prefix, StringComparison.Ordinal))
        .ToDictionary(static pair => pair.Key.target[prefix.Length..], static pair => pair.Value, StringComparer.Ordinal);
    internal static void Apply(
        StageController stage,
        NovelAudioDirector? audio
    ) {
        if (audio is not null) audio.SetTimelineMix(Mix(stage));
    }
}
