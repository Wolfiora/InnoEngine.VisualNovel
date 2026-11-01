using System;
using System.Linq;
using System.Numerics;
using InnoEditor.ImGui;
using InnoEngine.Input;
using InnoEngine.Rendering;
using Inno.VisualNovel.Integration.Services;
using Inno.VisualNovel.Player.Shell;
using Inno.VisualNovel.Player.Shell.Keyboard;
using Inno.VisualNovel.UI.Input;
using Inno.VisualNovel.UI.Layout;

namespace Inno.VisualNovel.Editor.Debugging;

internal sealed class PreviewCanvasInput
{
    private InnoNovelInput m_mapper = new();
    private static readonly KeyCode[] m_keys = NovelNavigationShortcuts.keys.Concat(new[] { KeyCode.Space, KeyCode.Tab, KeyCode.Home, KeyCode.End, KeyCode.Backspace, KeyCode.Delete })
        .Where(static key => key != KeyCode.Unknown).Distinct().ToArray();

    internal UiInputSnapshot Map(
        Vector2 local,
        bool inside,
        bool focused,
        float width,
        float height,
        PlayerPreferences preferences
    ) {
        var ui = ImGui.GetInput(); var viewport = new RenderViewport(0, 0, Math.Max(1, (int)Math.Ceiling(width)), Math.Max(1, (int)Math.Ceiling(height)));
        if (!focused || ui.wantsText) return m_mapper.Map(RenderOutputInput.suspended, viewport, width, height, preferences);
        Vector2 pointer = UiCanvasFit.Create(width, height, new(0, 0, viewport.width, viewport.height), true).Project(local);
        KeyModifier modifiers = (ui.control ? KeyModifier.Control : KeyModifier.None) | (ui.shift ? KeyModifier.Shift : KeyModifier.None)
            | (ui.alt ? KeyModifier.Alt : KeyModifier.None) | (ui.super ? KeyModifier.Super : KeyModifier.None);
        var pressed = m_keys.Where(static key => Enum.TryParse<ImGuiKey>(key.ToString(), out var native)
            && ImGui.IsKeyPressed(native, key is KeyCode.LeftArrow or KeyCode.RightArrow or KeyCode.UpArrow or KeyCode.DownArrow or KeyCode.Backspace or KeyCode.Delete)).ToArray();
        var sample = new RenderOutputInput(new(pointer.X, pointer.Y), inside, new(0, inside ? ui.wheel : 0), modifiers, pressed, [],
            inside && ImGui.IsMouseClicked(ImGuiMouseButton.Left) ? [MouseButton.Left] : [], ImGui.IsMouseReleased(ImGuiMouseButton.Left) ? [MouseButton.Left] : [], []);
        return m_mapper.Map(sample, viewport, width, height, preferences);
    }
    internal void Reset() => m_mapper = new();
}
