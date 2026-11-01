using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Interface.Animation;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Programs;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.Player.Interface.Performance;

/// <summary>Maps the existing performance clock to explicit editable page elements, independent of UI event animation time.</summary>
public sealed class NovelUiPerformance
{
    private readonly Dictionary<string, (string role, string element)> m_targets = new(StringComparer.Ordinal);
    private static readonly PerformanceProperty[] m_channels = [PerformanceProperty.X, PerformanceProperty.Y, PerformanceProperty.ScaleX, PerformanceProperty.Opacity];

    /// <summary>Freezes target identities shared by every theme variant of each authored role.</summary>
    /// <param name="project">The current game's editable pages and declared theme variants.</param>
    public NovelUiPerformance(NovelProjectAsset project)
    {
        foreach (var binding in project.screens)
        {
            if (binding.screen is not { } screen) continue;
            var ids = UiElementTree.Enumerate(screen.root).Select(static row => row.element.id).ToHashSet(StringComparer.Ordinal);
            foreach (var theme in project.themes)
            {
                var variant = theme.screens.SingleOrDefault(item => item.role == binding.role)?.screen;
                if (variant is not null) ids.IntersectWith(UiElementTree.Enumerate(variant.root).Select(static row => row.element.id));
            }
            foreach (string id in ids) m_targets.Add("ui/" + binding.role + "/" + id, (binding.role, id));
        }
    }
    /// <summary>Gets stable target labels for no-code binding menus.</summary>
    public IReadOnlyList<(string id, string label)> targets => Array.AsReadOnly(m_targets.OrderBy(static pair => pair.Key, StringComparer.Ordinal)
        .Select(static pair => (pair.Key, pair.Value.role + " · " + pair.Value.element)).ToArray());

    /// <summary>Creates detached explicit scalar channel capabilities for the existing stage performance owner.</summary>
    /// <returns>A target dictionary without sharing mutable channel arrays.</returns>
    public IReadOnlyDictionary<string, PerformanceProperty[]> GetChannels() => m_targets.ToDictionary(static pair => pair.Key, static _ => m_channels.ToArray(), StringComparer.Ordinal);

    /// <summary>Rejects missing page targets, incompatible channels and unsafe values at their story instruction.</summary>
    /// <param name="program">The exact linked program used by the session.</param>
    /// <param name="clips">The session's performance declarations.</param>
    public void ValidateProgram(
        StoryProgram program,
        IEnumerable<PerformanceClipAsset> clips
    ) {
        var library = clips.ToDictionary(static clip => clip.resourceId, StringComparer.Ordinal);
        foreach (var instruction in program.instructions)
        {
            if (instruction.performance is not { operation: PerformanceOperation.Play } request || !library.TryGetValue(request.clipId, out var clip)) continue;
            ValidateBinding(clip, request.bindings, instruction.sourceChapterId + "/" + instruction.sourceNodeId);
        }
    }
    /// <summary>Validates a concrete clip binding before playback or pure designer seeking.</summary>
    /// <param name="clip">The validated scalar performance declaration.</param>
    /// <param name="bindings">The explicit target substitution for this instance.</param>
    /// <param name="location">The source location included in diagnostics.</param>
    public void ValidateBinding(
        PerformanceClipAsset clip,
        PerformanceBinding[] bindings,
        string location
    ) {
        foreach (var track in clip.tracks)
        {
            string target = bindings.SingleOrDefault(item => item.slot == track.target)?.target ?? track.target;
            if (!target.StartsWith("ui/", StringComparison.Ordinal)) continue;
            if (!m_targets.ContainsKey(target) || !m_channels.Contains(track.property) || track.keys.Any(key => !Valid(track.property, key.value)))
                throw new InvalidOperationException("Invalid UI performance target, channel or value at " + location + " → " + target + "/" + track.property);
        }
    }
    /// <summary>Projects already evaluated shared-clock channels into one page's relative presentation values.</summary>
    /// <param name="role">The current shell role.</param>
    /// <param name="samples">Values from the existing stage performance controller.</param>
    /// <returns>Detached poses keyed by authored element identity, including repeated templates.</returns>
    public IReadOnlyDictionary<string, UiMotionPose> GetValues(
        string role,
        IReadOnlyDictionary<(string target, PerformanceProperty property), double> samples
    ) {
        var result = new Dictionary<string, UiMotionPose>(StringComparer.Ordinal);
        foreach (var pair in samples)
        {
            if (!pair.Key.target.StartsWith("ui/", StringComparison.Ordinal)) continue;
            if (!m_targets.TryGetValue(pair.Key.target, out var target) || !Valid(pair.Key.property, pair.Value))
                throw new InvalidOperationException("The UI performance sample is invalid: " + pair.Key.target + "/" + pair.Key.property);
            if (target.role != role) continue;
            if (!result.TryGetValue(target.element, out var pose)) result.Add(target.element, pose = new());
            switch (pair.Key.property)
            {
                case PerformanceProperty.X: pose.x = (float)pair.Value; break;
                case PerformanceProperty.Y: pose.y = (float)pair.Value; break;
                case PerformanceProperty.ScaleX: pose.scale = (float)pair.Value; break;
                case PerformanceProperty.Opacity: pose.opacity = (float)pair.Value; break;
                default: throw new InvalidOperationException("The UI performance channel is unsupported: " + pair.Key.property);
            }
        }
        return result;
    }
    private static bool Valid(
        PerformanceProperty property,
        double value
    ) => double.IsFinite(value) && (property switch
    {
        PerformanceProperty.X or PerformanceProperty.Y => Math.Abs(value) <= 10000,
        PerformanceProperty.ScaleX => value is >= .1 and <= 4,
        PerformanceProperty.Opacity => value is >= 0 and <= 1,
        _ => false
    });
}
