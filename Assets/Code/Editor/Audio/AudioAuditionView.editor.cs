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
        try { if (ImGui.Button(fields.Label("试听 / Play preview"))) m_player.Play(cue); }
        finally { ImGui.EndDisabled(); }
        ImGui.SameLine(); if (ImGui.Button(fields.Label("停止试听"))) m_player.Stop();
        if (!m_player.active) return;
        ImGui.SameLine(); if (ImGui.Button(fields.Caption(m_player.paused ? "继续试听" : "暂停试听", m_player.paused ? fields.Localize("继续试听") : fields.Localize("暂停试听")))) m_player.TogglePause();
        double time = m_player.position;
        if (fields.Number("audition/position", fields.Localize("试听位置（秒）"), ref time, 0, m_player.duration)) m_player.Seek(time);
    }
    internal void Stop() => m_player.Stop();
    public void Dispose() => m_player.Dispose();
}
