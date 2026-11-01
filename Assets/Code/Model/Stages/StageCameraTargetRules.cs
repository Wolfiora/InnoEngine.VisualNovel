using System;

namespace Inno.VisualNovel.Model.Stages;

/// <summary>Defines the qualified stage owners available to declarative camera following.</summary>
public static class StageCameraTargetRules
{
    /// <summary>Recognizes an explicit character or scenery-layer owner, excluding UI, audio and camera recursion.</summary>
    /// <param name="target">A qualified owner such as character/actor or stage/background.</param>
    /// <returns>Whether the owner has an unambiguous nonempty stage-local suffix.</returns>
    public static bool IsValid(string? target) => target is not null && target.Length <= 1024
        && (target.StartsWith("character/", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(target[10..])
            || target.StartsWith("stage/", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(target[6..]));
}
