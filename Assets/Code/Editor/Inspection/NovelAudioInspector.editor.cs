using System;
using InnoEditor.ImGui;
using InnoEditor.Inspection;
using Inno.VisualNovel.Integration.Components;

namespace Inno.VisualNovel.Editor.Inspection;

/// <summary>Explains scene audio ownership without starting a voice from editor drawing.</summary>
[InspectionDrawer(typeof(NovelAudioComponent))]
public sealed class NovelAudioInspector : InspectionDrawer<NovelAudioComponent>
{
    /// <inheritdoc />
    public override string icon => ImGuiIcon.File;
    /// <inheritdoc />
    protected override (string, Action<string>?) BindName(
        InspectionDrawContext context,
        NovelAudioComponent target
    ) => ("VN Audio", null);
    /// <inheritdoc />
    protected override void Draw(
        InspectionDrawContext context,
        NovelAudioComponent target
    ) {
        context.DrawProperties();
        var widgets = EditorUiSession.current.widgets;
        widgets.Hint("Assign an Audio Cue. Play On Enable starts it during native runtime activation; editing or rendering this object is silent. Disabling the object or a parent releases its voices.");
        widgets.Hint("Cue fades and looping are reused. Gain multiplies cue and player volume; a native timeline Volume track can animate it. Paused preserves the current playback position.");
        if (target.cue is null) widgets.Hint("Assign a cue before playing this scene audio object.");
        else if (target.cue.isMissing || target.cue.clip is null || target.cue.clip.isMissing) widgets.Hint("The cue or its imported clip is missing.");
    }
}
