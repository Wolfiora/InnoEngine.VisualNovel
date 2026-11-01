using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using InnoEngine.Graphs;
using InnoEditor.Graph;
using InnoEditor.ImGui;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Editor.Localization;
using Inno.VisualNovel.Narrative.Debugging;

namespace Inno.VisualNovel.Editor.StoryGraph;

internal sealed class StoryGraphView
{
    private readonly GraphCanvasState m_navigation = new();
    private readonly GraphCanvasLayout m_layout = new();
    private readonly StoryGraphOverview m_overview = new();
    private bool m_showOverview = true;
    private readonly GraphCanvasPointerController m_pointer;
    private string m_primary = string.Empty;
    private Guid m_document;
    private long m_revision = -1;
    private string m_language = string.Empty;
    private readonly EditorTextView m_text;
    private readonly StoryGraphDebugView m_debugView;
    internal GraphCanvasState navigation => m_navigation;
    internal void Suspend() { m_pointer.Cancel(); m_overview.Cancel(); }

    internal StoryGraphView(EditorTextView text)
    {
        m_text = text;
        m_debugView = new(text);
        m_navigation.SetViewport(new(35, 160), 0.65f);
        m_pointer = new(m_navigation);
    }

    internal void Draw(
        StoryDocumentsModule documents,
        StoryDraftState draft,
        string selected,
        Action<string> select,
        Func<bool>? clipboardKeys = null,
        StoryDebugger? debugger = null,
        StoryDebugLocation? execution = null,
        bool executionPaused = false
    ) {
        if (clipboardKeys?.Invoke() == true) { Suspend(); return; }
        if (selected != m_primary)
        {
            if (selected.Length == 0 || !m_navigation.selectedNodes.Contains(new GraphNodeId(selected)))
                m_navigation.SelectNodes(selected.Length > 0 ? [new GraphNodeId(selected)] : []);
            m_primary = selected;
        }
        long revision = documents.GetRevision(documents.activeDocumentId);
        if (m_document != documents.activeDocumentId || m_revision != revision || m_language != m_text.language)
        {
            m_pointer.Cancel();
            var catalog = new StoryNodeCatalog(documents.codec);
            m_layout.SetNodes(draft.graph.document.nodes.Select(node => Describe(node, catalog, m_text)));
            m_overview.SetNodes(m_layout.nodes);
            m_document = documents.activeDocumentId; m_revision = revision; m_language = m_text.language;
        }
        bool centerAll = ImGui.SmallButton(m_text.Label("居中全部节点"));
        ImGui.SameLine(); bool centerSelected = ImGui.SmallButton(m_text.Label("居中所选节点"));
        ImGui.SameLine(); if (ImGui.Checkbox(m_text.Label("导航缩略图"), ref m_showOverview)) m_overview.Cancel();
        string active = execution?.chapter == draft.chapterId ? execution.node : string.Empty;
        bool centerExecution = false;
        if (active.Length > 0) { ImGui.SameLine(); centerExecution = ImGui.SmallButton(m_text.Label("居中执行位置")); }
        var breakpoints = (debugger?.breakpoints ?? []).Where(point => point.chapter == draft.chapterId)
            .ToDictionary(static point => point.node, static point => point.conditional, StringComparer.Ordinal);
        if (breakpoints.Count > 0) ImGui.Text(m_text.Localize("圆点：运行会话断点；方框：条件断点。"));
        Vector2 origin = ImGui.GetCursorScreenPosition();
        Vector2 size = Vector2.Max(Vector2.Zero, ImGui.GetContentRegionAvailable());
        if (centerAll || centerSelected) { m_pointer.Cancel(); m_overview.Cancel(); m_overview.CenterSelection(m_navigation, m_layout.nodes, size, centerSelected); }
        if (centerExecution && m_layout.TryGetNode(new(active), out var activeNode))
        { m_pointer.Cancel(); m_overview.Cancel(); m_overview.CenterSelection(m_navigation, [activeNode], size, false); }
        Vector2 mouse = ImGui.GetMousePos();
        bool overviewOwnsInput = m_showOverview && m_overview.HandleInput(m_navigation, origin, size,
            !m_pointer.isMoving && !m_pointer.isBoxSelecting && m_navigation.pendingConnection is null);
        bool hovered = !overviewOwnsInput && ImGui.IsWindowHovered() && mouse.X >= origin.X && mouse.Y >= origin.Y && mouse.X < origin.X + size.X && mouse.Y < origin.Y + size.Y;
        var input = ImGui.GetInput();
        if (hovered && input.wheel != 0)
        {
            if (m_pointer.isMoving || m_pointer.isBoxSelecting) m_pointer.Cancel();
            m_navigation.ZoomAt(MathF.Pow(1.12f, input.wheel), mouse.X - origin.X, mouse.Y - origin.Y);
        }
        if (hovered && ImGui.IsMouseButtonDown(ImGuiMouseButton.Middle))
        {
            if (m_pointer.isMoving || m_pointer.isBoxSelecting) m_pointer.Cancel();
            m_navigation.PanBy(input.mouseDelta.X, input.mouseDelta.Y);
        }
        var view = new GraphCanvasView(m_navigation, origin, size);
        GraphCanvasPointerResult result = m_pointer.Step(view, m_layout, new()
        {
            position = mouse, isHovered = hovered,
            isPressed = ImGui.IsMouseClicked(ImGuiMouseButton.Left),
            isDown = ImGui.IsMouseButtonDown(ImGuiMouseButton.Left),
            isReleased = ImGui.IsMouseReleased(ImGuiMouseButton.Left),
            shift = input.shift || ImGui.IsKeyDown(ImGuiKey.LeftCtrl) || ImGui.IsKeyDown(ImGuiKey.RightCtrl)
                || ImGui.IsKeyDown(ImGuiKey.LeftSuper) || ImGui.IsKeyDown(ImGuiKey.RightSuper),
            cancel = !ImGui.IsWindowFocused() || ImGui.IsKeyPressed(ImGuiKey.Escape, false)
                || overviewOwnsInput && ImGui.IsMouseReleased(ImGuiMouseButton.Left) && m_navigation.pendingConnection is not null
        });
        if (result.nodePressed is GraphNodeId pressed)
        {
            m_primary = m_navigation.selectedNodes.Contains(pressed) ? pressed.value : m_navigation.selectedNodes.FirstOrDefault().value ?? string.Empty;
            select(m_primary);
        }
        else if (m_primary.Length > 0 && !m_navigation.selectedNodes.Contains(new(m_primary)) || m_primary.Length == 0 && m_navigation.selectedNodes.Count > 0)
        {
            m_primary = draft.graph.document.nodes.FirstOrDefault(node => m_navigation.selectedNodes.Contains(node.id))?.id.value ?? string.Empty;
            select(m_primary);
        }
        if (result.movedNodes is { } positions)
            documents.Edit(documents.activeDocumentId, m_text.Localize("移动剧情节点"), state => documents.edits.Move(state, positions));
        if (result.output is GraphEndpoint output && result.input is GraphEndpoint destination)
            documents.Edit(documents.activeDocumentId, m_text.Localize("连接剧情节点"), state => documents.edits.Connect(state, output, destination.nodeId));
        var draw = ImGui.GetDrawList();
        draw.PushClip(origin, origin + size, true);
        try
        {
            view.DrawGrid(ImGui.ToPackedColor(new(0.20f, 0.21f, 0.25f, 1)));
            foreach (GraphEdgeRecord edge in draft.graph.document.edges)
            {
                Vector2? from = Port(edge.output);
                Vector2? to = Port(edge.input);
                if (from is { } a && to is { } b)
                    view.DrawConnection(draw, a, b, ImGui.ToPackedColor(new(0.56f, 0.72f, 0.88f, 1)));
            }
            foreach (GraphCanvasNode node in m_layout.nodes)
            {
                GraphPosition? previewPosition = m_pointer.previewPositions.TryGetValue(node.id, out GraphPosition position) ? position : null;
                view.DrawNode(draw, node, ImGui.ToPackedColor(new(0.22f, 0.32f, 0.39f, 1)), m_navigation.selectedNodes.Contains(node.id),
                    previewPosition: previewPosition);
                bool hasBreakpoint = breakpoints.TryGetValue(node.id.value, out bool conditional);
                m_debugView.DrawNode(view, node, previewPosition, origin, size, hasBreakpoint, conditional, node.id.value == active, executionPaused);
            }
            if (m_navigation.pendingConnection is { } pending && Port(pending) is { } start)
                view.DrawConnection(draw, start, mouse, ImGui.ToPackedColor(new(0.8f, 0.8f, 0.9f, 1)));
            if (m_pointer.isBoxSelecting)
            {
                Vector2 a = Vector2.Min(m_pointer.boxStart, mouse), b = Vector2.Max(m_pointer.boxStart, mouse);
                draw.FilledRectangle(a, b, ImGui.ToPackedColor(new(.4f, .7f, 1, .12f)));
                draw.Rectangle(a, b, ImGui.ToPackedColor(new(.4f, .7f, 1, .8f)), 0, 1);
            }
            if (m_showOverview) m_overview.Draw(m_navigation, m_layout, origin, size);
        }
        finally { draw.PopClip(); }

        Vector2? Port(GraphEndpoint endpoint)
        {
            if (!m_layout.TryGetNode(endpoint.nodeId, out GraphCanvasNode? node))
                return null;
            GraphCanvasPort? port = node.ports.Where(item => item.endpoint == endpoint).Cast<GraphCanvasPort?>().FirstOrDefault();
            GraphPosition position = m_pointer.previewPositions.TryGetValue(node.id, out GraphPosition preview) ? preview : node.position;
            return port is { } value ? view.ToScreen(position) + value.position * view.zoom : null;
        }
    }

    private static GraphCanvasNode Describe(
        GraphNodeRecord node,
        StoryNodeCatalog catalog,
        EditorTextView text
    ) {
        IReadOnlyList<GraphPortDefinition> definitions = catalog.TryResolve(node.definitionId, out GraphNodeDefinition? definition) && definition is not null
            ? definition.GetPorts(node) : [];
        var ports = new List<GraphCanvasPort>();
        int outputs = 0;
        foreach (GraphPortDefinition port in definitions)
        {
            bool input = port.direction == GraphPortDirection.Input;
            float y = 58 + (input ? 0 : outputs++) * 26;
            ports.Add(new(new(node.id, port.id), port.direction, new(input ? 0 : 230, y), text.Localize(port.displayName),
                labelBounds: (new(input ? 0 : 105, y - 12), new(input ? 105 : 230, y + 12))));
        }
        return new(node.id, node.position, new(230, MathF.Max(100, 72 + outputs * 26)), 34,
            text.Localize(definition?.displayName ?? node.definitionId) + " · " + node.id.value[..Math.Min(8, node.id.value.Length)], ports);
    }
}
