using System;
using InnoEditor.ImGui;

namespace Inno.VisualNovel.Editor.Workspace;

internal static class StoryClipboardShortcuts
{
    internal static void Handle(
        Action copy,
        Action paste,
        Action delete,
        Action selectAll
    ) {
        if (!ImGui.IsWindowFocused(ImGuiFocusedFlags.None) || ImGui.GetInput().wantsText || ImGui.IsAnyItemActive()
            || ImGui.IsMouseButtonDown(ImGuiMouseButton.Left) || ImGui.IsMouseButtonDown(ImGuiMouseButton.Middle)) return;
        bool control = ImGui.IsKeyDown(ImGuiKey.LeftCtrl) || ImGui.IsKeyDown(ImGuiKey.RightCtrl) || ImGui.IsKeyDown(ImGuiKey.LeftSuper) || ImGui.IsKeyDown(ImGuiKey.RightSuper);
        if (ImGui.IsKeyDown(ImGuiKey.LeftAlt) || ImGui.IsKeyDown(ImGuiKey.RightAlt) || ImGui.GetInput().shift) return;
        if (!control) { if (ImGui.IsKeyPressed(ImGuiKey.Delete, false)) delete(); return; }
        if (ImGui.IsKeyPressed(ImGuiKey.C, false)) copy();
        else if (ImGui.IsKeyPressed(ImGuiKey.V, false)) paste();
        else if (ImGui.IsKeyPressed(ImGuiKey.A, false)) selectAll();
    }
}
