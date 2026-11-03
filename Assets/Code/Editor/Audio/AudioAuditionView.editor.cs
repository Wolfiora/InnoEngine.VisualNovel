using System;
using InnoEditor.ImGui;
using Inno.VisualNovel.Authoring.Preview;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Integration.Services;
using Inno.VisualNovel.Model.Audio;

namespace Inno.VisualNovel.Editor.Audio;

internal sealed class AudioAuditionView(StoryInspectorFields fields) : IDisposable
{
    private readonly AudioAudition m_player = new(static cue => new InnoNovelAudio([cue]));
    private string m_source = string.Empty;
    internal void Draw(AudioCueAsset cue)
    {
        string source = cue.resourceId + "/" + cue.clip?.identity.persistentId + "/" + cue.clip?.contentVersion;
        if (m_source != source) { m_player.Stop(); m_source = source; }
        m_player.Tick(Math.Clamp(ImGui.GetInput().deltaTime, 0, .25));
        ImGui.BeginDisabled(cue.clip is not { isMissing: false });
        try { if (ImGui.Button("Play preview")) m_player.Play(cue); }
        finally { ImGui.EndDisabled(); }
        ImGui.SameLine(); if (ImGui.Button("Stop preview")) m_player.Stop();
        if (!m_player.active) return;
        ImGui.SameLine(); if (ImGui.Button(m_player.paused ? "Resume preview" : "Pause preview")) m_player.TogglePause();
        double time = m_player.position;
        if (fields.Number("audition/position", "Preview position (seconds)", ref time, 0, m_player.duration)) m_player.Seek(time);
    }
    internal void Stop() => m_player.Stop();
    public void Dispose() => m_player.Dispose();
}
