using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.TimelineEditing;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Model.Performance;

namespace Inno.VisualNovel.Editor.Timeline;

internal sealed class TimelineCanvasView(
    ResourceDocumentsModule documents,
    EditorInteractions interactions,
    StoryInspectorFields fields
)
{
    private const int m_pageSize = 12;
    private readonly HashSet<PerformanceKeyAddress> m_selected = [];
    private PerformanceKeySelection? m_start;
    private EditorEditGesture? m_drag;
    private Guid m_document;
    private long m_revision;
    private double m_zoom = 1, m_viewStart, m_offset, m_dragOffset, m_primaryTime;
    private int m_firstTrack;
    private Vector2 m_pointer, m_boxStart;
    private bool m_box, m_scrub, m_replace, m_removeEmpty;
    private string m_marker = string.Empty;
    private HashSet<PerformanceKeyAddress> m_boxOriginal = [];
    private string m_message = string.Empty;

    internal void Finish()
    {
        m_drag?.Commit(); m_drag = null; m_start = null; m_marker = string.Empty; m_box = m_scrub = false;
    }
    internal void Suspend()
    {
        if (m_drag is not null)
        {
            var result = m_drag.Cancel(); m_drag = null;
            if (!result.succeeded) throw new InvalidOperationException(result.message);
        }
        m_start = null; m_marker = string.Empty; m_box = m_scrub = false;
    }
    internal bool Draw(
        Guid document,
        PerformanceClipAsset clip,
        ref int track,
        ref double keyTime,
        ref double time,
        ref string marker,
        bool snap,
        double step
    ) {
        long revision = documents.GetRevision(document);
        if (document != m_document || revision != m_revision)
        { Finish(); m_selected.Clear(); m_document = document; m_revision = revision; }
        double authoredTime = keyTime;
        if (track >= 0 && track < clip.tracks.Length && clip.tracks[track].keys.Any(key => key.time == authoredTime)
            && !m_selected.Contains(new(track, keyTime))) { m_selected.Clear(); m_selected.Add(new(track, keyTime)); }
        double zoom = m_zoom;
        if (fields.Number("timeline/zoom", fields.Localize("时间缩放（倍）"), ref zoom, 1, 64)) { Finish(); m_zoom = zoom; }
        double span = clip.duration / m_zoom;
        m_viewStart = Math.Clamp(m_viewStart, 0, clip.duration - span);
        if (fields.Number("timeline/start", fields.Localize("可见时间起点"), ref m_viewStart, 0, clip.duration - span)) Finish();
        if (ImGui.SmallButton(fields.Label("完整片段"))) { Finish(); m_zoom = 1; m_viewStart = 0; span = clip.duration; }
        ImGui.SameLine(); if (ImGui.SmallButton(fields.Label("定位播放头"))) { Finish(); m_viewStart = Math.Clamp(time - span / 2, 0, clip.duration - span); }
        if (clip.tracks.Length > m_pageSize)
        {
            if (ImGui.SmallButton(fields.Label("上一组轨道"))) { Finish(); m_firstTrack = Math.Max(0, m_firstTrack - m_pageSize); }
            ImGui.SameLine(); if (ImGui.SmallButton(fields.Label("下一组轨道"))) { Finish(); m_firstTrack = Math.Min(Math.Max(0, clip.tracks.Length - m_pageSize), m_firstTrack + m_pageSize); }
        }
        m_firstTrack = Math.Clamp(m_firstTrack, 0, Math.Max(0, clip.tracks.Length - 1));
        int count = Math.Min(m_pageSize, clip.tracks.Length - m_firstTrack);
        Vector2 origin = ImGui.GetCursorScreenPosition(), size = new(Math.Max(100, ImGui.GetContentRegionAvailable().X), Math.Max(64, 32 + count * 28));
        Vector2 mouse = ImGui.GetMousePos();
        double PointerTime() => m_viewStart + (mouse.X - origin.X) / size.X * span;
        float X(double position) => origin.X + (float)((position - m_viewStart) / span) * size.X;
        float Y(int index) => origin.Y + 46 + (index - m_firstTrack) * 28;
        bool inside = ImGui.IsWindowHovered() && mouse.X >= origin.X && mouse.Y >= origin.Y && mouse.X < origin.X + size.X && mouse.Y < origin.Y + size.Y;
        bool edited = false;
        if (m_drag is not null && (ImGui.IsKeyPressed(ImGuiKey.Escape, false) || !ImGui.IsWindowFocused()))
        { Suspend(); m_selected.Clear(); m_revision = documents.GetRevision(document); ImGui.Dummy(size); return true; }
        if (!ImGui.IsWindowFocused()) { m_box = m_scrub = false; }
        if (inside && !ImGui.IsAnyItemActive() && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && m_drag is null)
        {
            fields.Finish(); m_message = string.Empty; m_pointer = mouse;
            var hitMarker = clip.markers.OrderBy(item => Math.Abs(X(item.time) - mouse.X)).FirstOrDefault();
            int row = m_firstTrack + (int)Math.Floor((mouse.Y - origin.Y - 32) / 28);
            if (mouse.Y < origin.Y + 32 && hitMarker is not null && Math.Abs(X(hitMarker.time) - mouse.X) <= 8)
            { marker = m_marker = hitMarker.id; time = hitMarker.time; m_drag = new(interactions.history, "移动演出标记"); }
            else if (mouse.Y < origin.Y + 32) { m_scrub = true; time = Math.Clamp(PointerTime(), 0, clip.duration); }
            else
            {
                var hit = row >= 0 && row < clip.tracks.Length ? clip.tracks[row].keys.OrderBy(key => Math.Abs(X(key.time) - mouse.X)).FirstOrDefault() : null;
                if (hit is not null && Math.Abs(X(hit.time) - mouse.X) <= 9)
                {
                    var address = new PerformanceKeyAddress(row, hit.time);
                    if (Control()) { if (!m_selected.Remove(address)) m_selected.Add(address); }
                    else
                    {
                        if (!Shift() && !m_selected.Contains(address)) m_selected.Clear();
                        m_selected.Add(address); m_start = PerformanceKeySelection.Capture(clip, m_selected);
                        m_primaryTime = hit.time; m_dragOffset = 0; m_drag = new(interactions.history, "移动所选演出关键帧");
                    }
                    track = row; time = hit.time;
                    keyTime = m_selected.Contains(address) ? hit.time : m_selected.Where(key => key.track == row).Select(static key => key.time).DefaultIfEmpty(-1).First();
                }
                else
                { m_boxOriginal = Shift() || Control() ? [.. m_selected] : []; m_selected.Clear(); m_boxStart = mouse; m_box = true; }
            }
        }
        if (m_scrub) time = Math.Clamp(PointerTime(), 0, clip.duration);
        if (m_box)
        {
            Vector2 low = Vector2.Min(m_boxStart, mouse), high = Vector2.Max(m_boxStart, mouse);
            m_selected.Clear(); m_selected.UnionWith(m_boxOriginal);
            for (int index = m_firstTrack; index < m_firstTrack + count; index++)
                if (Y(index) >= low.Y && Y(index) <= high.Y)
                    foreach (var key in clip.tracks[index].keys)
                        if (X(key.time) >= Math.Max(low.X, origin.X) && X(key.time) <= Math.Min(high.X, origin.X + size.X)) m_selected.Add(new(index, key.time));
            if (m_selected.Count > 0) { var first = m_selected.OrderBy(static key => key.track).ThenBy(static key => key.time).First(); track = first.track; keyTime = first.time; }
            else keyTime = -1;
        }
        if (m_drag is not null && ImGui.IsMouseButtonDown(ImGuiMouseButton.Left))
        {
            try
            {
                if (m_marker.Length > 0)
                {
                    double next = Bounded(PointerTime()); var selected = clip.markers.Single(item => item.id == m_marker);
                    if (selected.time != next)
                    { documents.Edit<PerformanceClipAsset>(document, fields.Localize("移动演出标记"), candidate => PerformanceEditService.SetMarker(candidate, new() { id = selected.id, name = selected.name, time = next }), m_drag.mergeKey); edited = true; time = next; }
                }
                else if (m_start is not null)
                {
                    double next = m_primaryTime + (mouse.X - m_pointer.X) / size.X * span;
                    if (snap && !Alt()) next = PerformanceEditService.Snap(next, step, clip.duration);
                    double offset = Math.Clamp(next - m_primaryTime, m_start.minimumOffset, m_start.maximumOffset);
                    if (offset != m_dragOffset)
                    {
                        var tracks = m_start.Transform(offset);
                        documents.Edit<PerformanceClipAsset>(document, fields.Localize("移动所选演出关键帧"), candidate => candidate.tracks = tracks, m_drag.mergeKey);
                        m_selected.Clear(); m_selected.UnionWith(m_start.keys.Select(key => new PerformanceKeyAddress(key.track, key.time + offset)));
                        m_dragOffset = offset; keyTime = time = m_primaryTime + offset; edited = true; m_message = string.Empty;
                    }
                }
                if (edited) m_revision = documents.GetRevision(document);
            }
            catch (InvalidOperationException failure) { m_message = fields.Error(failure); }
        }
        if (!ImGui.IsMouseButtonDown(ImGuiMouseButton.Left)) Finish();
        var draw = ImGui.GetDrawList(); uint line = ImGui.ToPackedColor(new(.3f, .35f, .4f, 1)), accent = ImGui.ToPackedColor(new(1, .8f, .4f, 1));
        draw.Rectangle(origin, origin + size, line); draw.PushClip(origin, origin + size, true);
        try
        {
            for (int tick = 0; tick <= 8; tick++)
            {
                float x = origin.X + size.X * tick / 8; draw.Line(new(x, origin.Y + 22), new(x, origin.Y + size.Y), line);
                draw.Text(new(x + 3, origin.Y + 1), line, (m_viewStart + span * tick / 8).ToString("0.###", CultureInfo.InvariantCulture));
            }
            for (int index = m_firstTrack; index < m_firstTrack + count; index++)
            {
                float y = Y(index); draw.Text(new(origin.X + 4, y - 12), line, (index + 1) + " · " + clip.tracks[index].target + " · " + clip.tracks[index].property, .8f);
                foreach (var key in clip.tracks[index].keys.Where(key => key.time >= m_viewStart && key.time <= m_viewStart + span))
                    draw.FilledCircle(new(X(key.time), y), 5, m_selected.Contains(new(index, key.time)) ? accent : ImGui.ToPackedColor(new(.6f, .75f, 1, 1)));
            }
            foreach (var item in clip.markers.Where(item => item.time >= m_viewStart && item.time <= m_viewStart + span))
            { draw.Line(new(X(item.time), origin.Y + 24), new(X(item.time), origin.Y + size.Y), accent); draw.FilledCircle(new(X(item.time), origin.Y + 25), item.id == marker ? 5 : 3, accent); }
            draw.Line(new(X(time), origin.Y), new(X(time), origin.Y + size.Y), ImGui.ToPackedColor(new(.5f, 1, .75f, 1)), 2);
            if (m_box) { draw.FilledRectangle(Vector2.Min(m_boxStart, mouse), Vector2.Max(m_boxStart, mouse), ImGui.ToPackedColor(new(.4f, .7f, 1, .16f))); draw.Rectangle(Vector2.Min(m_boxStart, mouse), Vector2.Max(m_boxStart, mouse), accent); }
        }
        finally { draw.PopClip(); }
        ImGui.Dummy(size);
        if (edited) return true;
        if (ImGui.SmallButton(fields.Label("选择全部关键帧"))) { Finish(); m_selected.Clear(); for (int index = 0; index < clip.tracks.Length; index++) foreach (var key in clip.tracks[index].keys) m_selected.Add(new(index, key.time)); }
        ImGui.SameLine(); if (ImGui.SmallButton(fields.Label("清空关键帧选择"))) { Finish(); m_selected.Clear(); keyTime = -1; }
        ImGui.Text(fields.Localize("已选 ") + m_selected.Count + fields.Localize(" 个关键帧。空白拖框，Ctrl 切换，Shift 追加；拖动保留间距，Alt 暂停吸附，Esc 取消。"));
        if (m_message.Length > 0) EditorUiSession.current.widgets.Hint(m_message);
        if (m_selected.Count == 0) return false;
        fields.Number("timeline/offset", fields.Localize("整组偏移（秒）"), ref m_offset, -clip.duration, clip.duration);
        ImGui.Checkbox(fields.Label("明确允许覆盖目标时间的关键帧"), ref m_replace); ImGui.Checkbox(fields.Label("删除时同时移除空轨道"), ref m_removeEmpty);
        bool move = ImGui.Button(fields.Label("偏移所选关键帧")); ImGui.SameLine(); bool copy = ImGui.Button(fields.Label("复制所选关键帧")); ImGui.SameLine(); bool delete = ImGui.Button(fields.Label("删除所选关键帧"));
        if (!move && !copy && !delete) return false;
        Finish(); fields.Finish(); var frozen = PerformanceKeySelection.Capture(clip, m_selected);
        var result = delete ? frozen.Delete(m_removeEmpty) : frozen.Transform(m_offset, copy, m_replace);
        documents.Edit<PerformanceClipAsset>(document, delete ? fields.Localize("删除所选演出关键帧") : copy ? fields.Localize("复制所选演出关键帧") : fields.Localize("偏移所选演出关键帧"), candidate => candidate.tracks = result);
        m_revision = documents.GetRevision(document); m_selected.Clear(); keyTime = -1; return true;

        double Bounded(double value) => snap && !Alt() ? PerformanceEditService.Snap(value, step, clip.duration) : Math.Clamp(value, 0, clip.duration);
    }
    private static bool Control() => ImGui.IsKeyDown(ImGuiKey.LeftCtrl) || ImGui.IsKeyDown(ImGuiKey.RightCtrl) || ImGui.IsKeyDown(ImGuiKey.LeftSuper) || ImGui.IsKeyDown(ImGuiKey.RightSuper);
    private static bool Shift() => ImGui.IsKeyDown(ImGuiKey.LeftShift) || ImGui.IsKeyDown(ImGuiKey.RightShift);
    private static bool Alt() => ImGui.IsKeyDown(ImGuiKey.LeftAlt) || ImGui.IsKeyDown(ImGuiKey.RightAlt);
}
