using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Inno.VisualNovel.Model.Extensions;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Programs;

namespace Inno.VisualNovel.Player.Performance;

/// <summary>Freezes pure extension-channel capabilities; timeline playback and restoration remain owned by the normal VN stage.</summary>
public sealed class NovelPerformanceTargets
{
    /// <summary>Creates immutable declarations independently of the caller's dictionaries and arrays.</summary>
    /// <param name="targets">Up to 4096 extension/ identities, each with distinct supported scalar channels; an empty declaration is valid.</param>
    public NovelPerformanceTargets(IReadOnlyDictionary<string, PerformanceProperty[]> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        if (targets.Count > 4096) throw new ArgumentOutOfRangeException(nameof(targets));
        var copy = new Dictionary<string, IReadOnlyList<PerformanceProperty>>(StringComparer.Ordinal);
        foreach (var pair in targets)
        {
            if (pair.Key is null || !pair.Key.StartsWith("extension/", StringComparison.Ordinal) || pair.Key.Length is <= 10 or > 256
                || pair.Key.Any(static character => char.IsWhiteSpace(character) || char.IsControl(character))
                || pair.Value is null || pair.Value.Length == 0 || pair.Value.Any(static property => !Enum.IsDefined(property))
                || pair.Value.Distinct().Count() != pair.Value.Length)
                throw new ArgumentException("Extension performance targets require bounded extension/ identities and distinct supported scalar channels.", nameof(targets));
            copy.Add(pair.Key, Array.AsReadOnly(pair.Value.Order().ToArray()));
        }
        this.targets = new ReadOnlyDictionary<string, IReadOnlyList<PerformanceProperty>>(copy);
        fingerprint = NovelImplementationFingerprint.Combine(copy.Select(pair => "target/" + pair.Key.Length.ToString(CultureInfo.InvariantCulture) + ":" + pair.Key + "/"
            + string.Join(",", pair.Value.Select(static property => ((int)property).ToString(CultureInfo.InvariantCulture)))));
    }
    /// <summary>Gets immutable target and channel declarations; these never contain scene objects, callbacks or GPU state.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<PerformanceProperty>> targets { get; }
    /// <summary>Gets an order-independent contract contribution for exact-build saves; bridge implementation code remains an explicit host contribution.</summary>
    public string fingerprint { get; }
    internal void ValidateBindings(
        StoryProgram program,
        IEnumerable<PerformanceClipAsset> clips
    ) {
        var library = clips.ToDictionary(static clip => clip.resourceId, StringComparer.Ordinal);
        foreach (var instruction in program.instructions)
        {
            if (instruction.performance is not { operation: PerformanceOperation.Play } performance) continue;
            if (!library.TryGetValue(performance.clipId, out var clip))
            {
                if (performance.bindings.Any(static binding => binding.target.StartsWith("extension/", StringComparison.Ordinal)))
                    throw new InvalidOperationException("Extension performance clip is unavailable at " + instruction.sourceChapterId + "/" + instruction.sourceNodeId + ": " + performance.clipId);
                continue;
            }
            foreach (var binding in performance.bindings.Where(static binding => binding.target.StartsWith("extension/", StringComparison.Ordinal)))
                if (!clip.tracks.Any(track => track.target == binding.slot))
                    throw new InvalidOperationException("Extension performance slot is unavailable at " + instruction.sourceChapterId + "/" + instruction.sourceNodeId + ": " + binding.slot);
            foreach (var track in clip.tracks)
            {
                string target = performance.bindings.SingleOrDefault(binding => binding.slot == track.target)?.target ?? track.target;
                if (!target.StartsWith("extension/", StringComparison.Ordinal)) continue;
                if (!targets.TryGetValue(target, out var channels))
                    throw new InvalidOperationException("Undeclared extension performance target at " + instruction.sourceChapterId + "/" + instruction.sourceNodeId + ": " + target);
                if (!channels.Contains(track.property))
                    throw new InvalidOperationException("Unsupported extension performance channel at " + instruction.sourceChapterId + "/" + instruction.sourceNodeId + ": " + target + "/" + track.property);
            }
        }
    }
}
