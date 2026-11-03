using System;
using System.Collections.Generic;
using System.Numerics;
using InnoEngine.Audio;
using InnoEditor.ImGui;
using Inno.VisualNovel.Authoring.VoiceManagement;
using Inno.VisualNovel.Editor.Inspection;

namespace Inno.VisualNovel.Editor.Audio;

internal sealed class WaveformView(StoryInspectorFields fields)
{
    private Guid m_clip;
    private long m_version;
    private WaveAudioContent? m_content;
    private IReadOnlyList<WaveformPeak> m_peaks = [];
    private double m_start, m_end;
    private double? m_drag;
    internal AudioClipAsset? Draw(
        string projectDirectory,
        AudioClipAsset? clip
    ) {
        if (clip is null || clip.metadata?.codec != AudioCodecId.wav) return null;
        if (m_clip != clip.identity.persistentId || m_version != clip.contentVersion)
        { m_clip = clip.identity.persistentId; m_version = clip.contentVersion; m_content = null; m_peaks = []; m_drag = null; }
        if (!ImGui.CollapsingHeader("Waveform and trim")) return null;
        if (ImGui.Button(m_content is null ? "Read waveform (without playback)" : "Reload waveform"))
        { var content = NativeWaveformSource.Read(clip); var peaks = content.BuildPeaks(1024); m_content = content; m_peaks = peaks; m_start = 0; m_end = content.duration; }
        if (m_content is null) return null;
        float width = Math.Max(100, ImGui.GetContentRegionAvailable().X), height = 170;
        Vector2 origin = ImGui.GetCursorScreenPosition(), size = new(width, height); var draw = ImGui.GetDrawList();
        uint line = ImGui.ToPackedColor(new(.55f, .78f, 1, 1)), selected = ImGui.ToPackedColor(new(.65f, .8f, 1, 1));
        draw.Rectangle(origin, origin + size, line); draw.Line(origin + new Vector2(0, height / 2), origin + new Vector2(width, height / 2), line);
        for (int index = 0; index < m_peaks.Count; index++)
        {
            float x = width * index / m_peaks.Count;
            draw.Line(origin + new Vector2(x, height * (0.5f - m_peaks[index].minimum * 0.45f)), origin + new Vector2(x, height * (0.5f - m_peaks[index].maximum * 0.45f)), line);
        }
        float left = (float)(m_start / m_content.duration) * width, right = (float)(m_end / m_content.duration) * width;
        draw.Rectangle(origin + new Vector2(left, 0), origin + new Vector2(right, height), selected);
        Vector2 mouse = ImGui.GetMousePos(); bool inside = ImGui.IsWindowHovered() && mouse.X >= origin.X && mouse.X <= origin.X + width && mouse.Y >= origin.Y && mouse.Y <= origin.Y + height;
        double time = Math.Clamp((mouse.X - origin.X) / width, 0, 1) * m_content.duration;
        if (inside && ImGui.IsMouseClicked(ImGuiMouseButton.Left)) m_drag = time;
        if (m_drag is { } start)
        {
            if (!ImGui.IsWindowFocused() || ImGui.IsKeyPressed(ImGuiKey.Escape, false)) m_drag = null;
            else if (!ImGui.IsMouseButtonDown(ImGuiMouseButton.Left)) m_drag = null;
            else { m_start = Math.Min(start, time); m_end = Math.Max(start, time); }
        }
        ImGui.Dummy(size);
        fields.Number("waveform/start", "Trim start (seconds)", ref m_start, 0, m_content.duration);
        fields.Number("waveform/end", "Trim end (seconds, exclusive)", ref m_end, 0, m_content.duration);
        EditorUiSession.current.widgets.Hint(string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0} channels · {1} Hz · {2} frames. Drag the waveform or enter a range; boundaries snap to the nearest sample frame. Creating a WAV keeps the original recording. The cue's new reference can be undone.", m_content.channels, m_content.sampleRate, m_content.frameCount));
        bool valid = double.IsFinite(m_start) && double.IsFinite(m_end) && m_start >= 0 && m_end <= m_content.duration && m_end > m_start;
        ImGui.BeginDisabled(!valid);
        try { if (ImGui.Button("Create trimmed copy and link this cue")) return NativeWaveformSource.CreateTrimmedClip(projectDirectory, m_content, m_start, m_end); }
        finally { ImGui.EndDisabled(); }
        return null;
    }
}
