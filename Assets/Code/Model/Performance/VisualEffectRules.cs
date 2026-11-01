using System;

namespace Inno.VisualNovel.Model.Performance;

/// <summary>Checks declarative screen and transition parameters without creating host resources.</summary>
public static class VisualEffectRules
{
    /// <summary>Validates a concrete screen-operation payload.</summary>
    /// <param name="definition">The authored payload.</param>
    public static void Validate(ScreenEffectDefinition definition)
    {
        if (!Enum.IsDefined(definition.kind) || !Range(definition.blurRadius, 0, 32) || !Range(definition.vignetteRadius, 0, 1)
            || !Range(definition.vignetteSoftness, 0.001, 1) || !Range(definition.saturation, 0, 4) || !Range(definition.brightness, -1, 1)
            || !Range(definition.contrast, 0, 4) || !Range(definition.pixelSize, 1, 512) || !Range(definition.letterboxRatio, 0, 0.45)
            || !Range(definition.aberrationPixels, 0, 32)) throw new InvalidOperationException("Screen effect parameters exceed their declared finite bounds.");
    }
    /// <summary>Validates a concrete image-transition payload.</summary>
    /// <param name="definition">The authored payload.</param>
    public static void Validate(TransitionDefinition definition)
    {
        if (!Enum.IsDefined(definition.kind) || !Enum.IsDefined(definition.mask) || definition.seed == 0 || !Range(definition.duration, 0.001, 3600)
            || !Range(definition.angle, -3600, 3600) || !Range(definition.softness, 0, 0.5) || !Range(definition.centerX, 0, 1) || !Range(definition.centerY, 0, 1))
            throw new InvalidOperationException("Transition parameters exceed their declared finite bounds.");
    }
    private static bool Range(double value, double minimum, double maximum) => double.IsFinite(value) && value >= minimum && value <= maximum;
}
