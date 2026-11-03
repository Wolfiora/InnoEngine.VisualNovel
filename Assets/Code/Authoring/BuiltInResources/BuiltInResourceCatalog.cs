using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Performance;

namespace Inno.VisualNovel.Authoring.BuiltInResources;

/// <summary>Lists known shipped presets; native Assets remains the only asset identity and loading authority.</summary>
public static class BuiltInResourceCatalog
{
    private static readonly IReadOnlyList<BuiltInResourceEntry> m_entries = Array.AsReadOnly(Create());
    /// <summary>Gets immutable metadata for available presets, without scanning or caching imported objects.</summary>
    public static IReadOnlyList<BuiltInResourceEntry> entries => m_entries;
    private static BuiltInResourceEntry[] Create()
    {
        var entries = new List<BuiltInResourceEntry>();
        foreach (var widget in Templates.UiWidgetTemplates.entries) entries.Add(new(BuiltInResourceKind.Widget, "BuiltIn/Widgets/" + widget.id + ".ivnui", widget.title, widget.description));
        foreach (string name in Templates.ShellThemeTemplates.names) entries.Add(new(BuiltInResourceKind.Theme, "BuiltIn/Themes/" + name + "/" + name + ".ivntheme", name, "18 editable system screens, inherited styles, control states and font-size tokens."));
        Add(BuiltInResourceKind.Stage, "BuiltIn/Backgrounds/", ".ivnstage", "2D stage background; original 1920×1080 vector illustration.", "Classroom|Afternoon Classroom", "Station|Twilight Station", "Seafront|Seafront Walk", "Woodland|Woodland Path", "Bedroom|Quiet Bedroom", "Rooftop|Rooftop View");
        foreach (string name in new[] { "Cheng", "Shu" }) entries.Add(new(BuiltInResourceKind.Character, "BuiltIn/Characters/" + name + "/" + name + ".ivncharacter", name == "Cheng" ? "Cheng · Layered Example Character" : "Shu · Layered Example Character", "2 poses, 6 expressions, 5 independent layers and editable breathing."));
        Add(BuiltInResourceKind.Motion, "BuiltIn/Motions/", ".ivnclip", "Character or camera target slot; copy to edit keyframes in the timeline.",
            "enter-left|Enter from Left", "enter-right|Enter from Right", "enter-bottom|Enter from Below", "fade-in|Fade In", "fade-out|Fade Out", "exit-left|Exit Left", "exit-right|Exit Right", "exit-bottom|Exit Down",
            "bounce|Bounce", "emphasis|Scale Emphasis", "shake|Short Shake", "nod|Nod", "float|Gentle Float", "breathe|Subtle Breathing", "sway|Gentle Sway", "camera-push|Camera Push", "camera-pull|Camera Pull", "camera-pan|Camera Pan", "camera-shake|Camera Shake", "dim|Dim Character");
        Add(BuiltInResourceKind.Particle, "BuiltIn/Particles/", ".ivneffect", "Stage emitter with a fixed random seed, save support and logical-time preview.", "Rain|Rain", "Snow|Snow", "Dust|Dust", "Petals|Petals", "Fireflies|Fireflies", "LightPoints|Light Points", "Bubbles|Bubbles", "Confetti|Confetti");
        Add(BuiltInResourceKind.Transition, "BuiltIn/Transitions/", ".ivneffect", "Stage, layer-group or full-output boundary; editable duration, softness and center.", "fade-black|Fade to Black", "fade-white|Fade to White", "dissolve|Cross Dissolve", "wipe-right|Wipe Right", "wipe-down|Wipe Down", "wipe-diagonal|Diagonal Wipe", "mask-noise|Noise Dissolve", "mask-checker|Checker Reveal", "mask-blinds|Blinds", "slide-left|Slide Left", "iris-open|Iris Open", "iris-close|Iris Close");
        string[] effectNames = ["Soft Blur", "Soft Vignette", "Grayscale", "Warm Tint", "Pixelation", "White Flash", "Letterbox", "Sepia", "Invert", "Chromatic Aberration"];
        foreach (var kind in Enum.GetValues<ScreenEffectKind>()) entries.Add(new(BuiltInResourceKind.ScreenEffect, "BuiltIn/ScreenEffects/" + kind.ToString().ToLowerInvariant() + ".ivneffect", effectNames[(int)kind], "Post-processing for the stage or output including UI; copy to edit strength and parameters."));
        Add(BuiltInResourceKind.Audio, "BuiltIn/Audio/UI/", ".ivnaudio", "One-shot UI cue; edit volume and fades in the audio panel.",
            "Hover|Soft Hover", "Confirm|Soft Confirm", "Cancel|Back / Cancel", "Open|Open Screen", "Close|Close Screen", "PageForward|Page Forward", "PageBack|Page Back", "Notice|Gentle Notice", "Error|Action Unavailable", "Save|Save Complete", "Load|Load Complete", "Unlock|Collection Unlock", "Focus|Focus Move", "Select|Select Item", "ToggleOn|Toggle On", "ToggleOff|Toggle Off", "Slider|Slider Adjust", "Typing|Typing", "Start|Story Start", "End|Segment End");
        Add(BuiltInResourceKind.Audio, "BuiltIn/Audio/Music/", ".ivnaudio", "An 8-second synthesized ambience for tutorials, with looping and fades.", "Dawn|Dawn", "PaperRoom|Paper Room", "QuietCinema|Quiet Cinema");
        foreach (string name in ("play pause stop next previous forward rewind skip auto history save load quick-save quick-load settings menu close check plus minus chevron-left chevron-right chevron-up chevron-down arrow-left arrow-right arrow-up arrow-down home search filter sort grid list folder file image music voice volume volume-mute headphones language font eye eye-off lock unlock star heart bookmark info warning error help refresh undo redo copy trash edit link external fullscreen window controller keyboard mouse").Split(' '))
            entries.Add(new(BuiltInResourceKind.Icon, "BuiltIn/Icons/" + name + ".png", name, "128×128 white icons on transparent backgrounds; tint through UI image styling."));
        if (entries.Select(static entry => entry.path).Distinct(StringComparer.Ordinal).Count() != entries.Count) throw new InvalidOperationException("Built-in preset paths must be unique.");
        return entries.ToArray();
        void Add(
            BuiltInResourceKind kind,
            string folder,
            string extension,
            string usage,
            params string[] values
        )
        {
            foreach (string value in values) { string[] fields = value.Split('|'); entries.Add(new(kind, folder + fields[0] + extension, fields[1], usage)); }
        }
    }
}
