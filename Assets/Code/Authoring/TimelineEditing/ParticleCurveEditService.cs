using System;
using System.Linq;
using Inno.VisualNovel.Model.Performance;

namespace Inno.VisualNovel.Authoring.TimelineEditing;

/// <summary>Edits bounded particle lifetime keys without partially changing rejected candidates.</summary>
public static class ParticleCurveEditService
{
    /// <summary>Inserts or replaces a size key and sorts it by normalized age.</summary>
    /// <param name="preset">The transaction's emitter candidate.</param>
    /// <param name="index">The existing key index, or minus one to insert.</param>
    /// <param name="key">The independently copied new declaration.</param>
    public static void SetSize(
        ParticlePreset preset,
        int index,
        PerformanceKeyframe key
    ) {
        ValidateIndex(index, preset.sizeKeys.Length);
        var keys = preset.sizeKeys.Where((_, position) => position != index).Append(new PerformanceKeyframe
            { time = key.time, value = key.value, interpolation = key.interpolation, curve = key.curve.Copy() }).OrderBy(static item => item.time).ToArray();
        ParticleCurveRules.Validate(new() { sizeKeys = keys, colorKeys = preset.colorKeys }); preset.sizeKeys = keys;
    }
    /// <summary>Inserts or replaces a color stop and sorts it by normalized age.</summary>
    /// <param name="preset">The transaction's emitter candidate.</param>
    /// <param name="index">The existing stop index, or minus one to insert.</param>
    /// <param name="key">The independently copied new declaration.</param>
    public static void SetColor(
        ParticlePreset preset,
        int index,
        ParticleColorKeyframe key
    ) {
        ValidateIndex(index, preset.colorKeys.Length);
        var keys = preset.colorKeys.Where((_, position) => position != index).Append(key.Copy()).OrderBy(static item => item.progress).ToArray();
        ParticleCurveRules.Validate(new() { sizeKeys = preset.sizeKeys, colorKeys = keys }); preset.colorKeys = keys;
    }
    /// <summary>Removes a selected channel key; an empty channel restores its endpoint settings.</summary>
    /// <param name="preset">The transaction's emitter candidate.</param>
    /// <param name="color">True for color stops, false for size keys.</param>
    /// <param name="index">The existing stop index.</param>
    public static void Remove(
        ParticlePreset preset,
        bool color,
        int index
    ) {
        int length = color ? preset.colorKeys.Length : preset.sizeKeys.Length;
        if (index < 0 || index >= length) throw new ArgumentOutOfRangeException(nameof(index));
        if (color) preset.colorKeys = preset.colorKeys.Where((_, position) => position != index).ToArray();
        else preset.sizeKeys = preset.sizeKeys.Where((_, position) => position != index).ToArray();
    }
    private static void ValidateIndex(
        int index,
        int length
    ) { if (index < -1 || index >= length) throw new ArgumentOutOfRangeException(nameof(index)); }
}
