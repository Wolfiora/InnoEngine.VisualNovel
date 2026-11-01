using System.Linq;
using Inno.VisualNovel.Model.Performance;

namespace Inno.VisualNovel.Authoring.Templates;

/// <summary>Provides explicit native preset roots without scanning installation directories.</summary>
public static class BuiltInImageEffectLibrary
{
    /// <summary>Loads the twelve editable-by-copy transition presets.</summary>
    /// <returns>Canonical read-only installation assets.</returns>
    public static VisualEffectAsset[] Transitions() => new[] { "fade-black", "fade-white", "dissolve", "wipe-right", "wipe-down", "wipe-diagonal", "mask-noise", "mask-checker", "mask-blinds", "slide-left", "iris-open", "iris-close" }
        .Select(static name => BuiltInNovelAssets.Load<VisualEffectAsset>("BuiltIn/Transitions/" + name + ".ivneffect")).ToArray();
    /// <summary>Loads one editable-by-copy preset for each supported screen operation.</summary>
    /// <returns>Canonical read-only installation assets.</returns>
    public static VisualEffectAsset[] Screens() => System.Enum.GetValues<ScreenEffectKind>()
        .Select(static kind => BuiltInNovelAssets.Load<VisualEffectAsset>("BuiltIn/ScreenEffects/" + kind.ToString().ToLowerInvariant() + ".ivneffect")).ToArray();
}
