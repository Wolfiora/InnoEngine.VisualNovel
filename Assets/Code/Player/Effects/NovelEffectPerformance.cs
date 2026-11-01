using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Programs;
using Inno.VisualNovel.Model.Story.Nodes.Stage;

namespace Inno.VisualNovel.Player.Effects;

/// <summary>Declares effect-instance timeline targets from authored instructions, without another effect clock.</summary>
public static class NovelEffectPerformance
{
    /// <summary>Collects only channels shared by every preset assigned to the same effect instance.</summary>
    /// <param name="program">The linked source-addressed story program.</param>
    /// <param name="presets">The game's explicitly declared screen-effect library.</param>
    /// <returns>Detached stage performance capabilities keyed by effect instance.</returns>
    public static IReadOnlyDictionary<string, PerformanceProperty[]> Targets(
        StoryProgram program,
        IEnumerable<VisualEffectAsset> presets
    ) {
        var library = presets.Where(static preset => preset.kind == VisualEffectKind.Screen).ToDictionary(static preset => preset.resourceId, StringComparer.Ordinal);
        var result = new Dictionary<string, PerformanceProperty[]>(StringComparer.Ordinal);
        foreach (var instruction in program.instructions)
        {
            if (instruction.screenEffect is not { operation: ScreenEffectOperation.Set or ScreenEffectOperation.Fade } effect) continue;
            if (!library.TryGetValue(effect.resourceId, out var preset)) continue;
            string target = "effect/" + effect.instanceId;
            var channels = ScreenEffectChannels.For(preset.screen.kind);
            result[target] = result.TryGetValue(target, out var prior) ? prior.Intersect(channels).ToArray() : channels;
        }
        return result;
    }
    /// <summary>Checks effect-target bindings before session preparation can publish any state.</summary>
    /// <param name="program">The linked program containing concrete performance requests.</param>
    /// <param name="clips">Declared immutable performance resources.</param>
    /// <param name="targets">The explicit effect capabilities collected for this game.</param>
    public static void ValidateProgram(
        StoryProgram program,
        IEnumerable<PerformanceClipAsset> clips,
        IReadOnlyDictionary<string, PerformanceProperty[]> targets
    ) {
        var library = clips.ToDictionary(static clip => clip.resourceId, StringComparer.Ordinal);
        foreach (var instruction in program.instructions)
        {
            if (instruction.performance is not { operation: PerformanceOperation.Play } request || !library.TryGetValue(request.clipId, out var clip)) continue;
            foreach (var track in clip.tracks)
            {
                string target = request.bindings.SingleOrDefault(item => item.slot == track.target)?.target ?? track.target;
                if (!target.StartsWith("effect/", StringComparison.Ordinal)) continue;
                if (!targets.TryGetValue(target, out var channels) || !channels.Contains(track.property) || track.keys.Any(key => !PerformancePropertyRules.IsValid(track.property, key.value)))
                    throw new InvalidOperationException("Invalid effect timeline target, parameter or value at " + instruction.sourceChapterId + "/" + instruction.sourceNodeId + " → " + target + "/" + track.property);
            }
        }
    }
}
