using System;
using System.Linq;
using InnoEngine.Input;
using InnoEngine.Rendering;
using Inno.VisualNovel.UI.Input;
using Inno.VisualNovel.UI.Layout;
using Inno.VisualNovel.Player.Shell;
using Inno.VisualNovel.Player.Shell.Keyboard;

namespace Inno.VisualNovel.Integration.Services;

/// <summary>Maps engine viewport input to the VN design canvas without global capture.</summary>
public sealed class InnoNovelInput
{
    private bool m_held;
    private bool m_waitForSkipRelease;
    private KeyModifier m_skipModifiers = KeyModifier.Control;

    /// <summary>Creates one sample using the same aspect fit as rendering.</summary>
    /// <param name="input">The host viewport input.</param>
    /// <param name="viewport">The physical viewport.</param>
    /// <param name="width">Logical design width.</param>
    /// <param name="height">Logical design height.</param>
    /// <returns>The design-space input.</returns>
    public UiInputSnapshot Map(
        RenderOutputInput input,
        RenderViewport viewport,
        float width,
        float height
    ) => Map(input, viewport, width, height, null);

    /// <summary>Maps viewport-local reading and menu bindings while retaining independent physical text editing.</summary>
    /// <param name="input">The host's frame-local input.</param>
    /// <param name="viewport">The physical output rectangle.</param>
    /// <param name="width">Logical design width.</param>
    /// <param name="height">Logical design height.</param>
    /// <param name="preferences">Current validated mappings, or null for defaults.</param>
    /// <returns>Neutral input with exact configured chords; suspended outputs never dispatch reading actions.</returns>
    public UiInputSnapshot Map(
        RenderOutputInput input,
        RenderViewport viewport,
        float width,
        float height,
        PlayerPreferences? preferences
    ) {
        if (!input.interactionEnabled)
        {
            bool release = m_held; m_held = false;
            m_waitForSkipRelease = true;
            return new() { released = release };
        }
        KeyModifier skip = preferences?.holdSkipModifiers ?? KeyModifier.Control;
        if (skip != m_skipModifiers) { m_skipModifiers = skip; m_waitForSkipRelease = (input.modifiers & skip) != 0; }
        bool holdingSkip = skip != KeyModifier.None && input.modifiers == skip;
        if ((input.modifiers & skip) == 0) m_waitForSkipRelease = false;
        else if (!holdingSkip || input.keysPressed.Any(static key => key is not (KeyCode.LeftCtrl or KeyCode.RightCtrl or KeyCode.LeftAlt or KeyCode.RightAlt or KeyCode.LeftShift or KeyCode.RightShift))) m_waitForSkipRelease = true;
        bool pressed = input.buttonsPressed.Contains(MouseButton.Left), released = input.buttonsReleased.Contains(MouseButton.Left);
        if (pressed) m_held = true;
        if (released) m_held = false;
        var fit = UiCanvasFit.Create(width, height, new(0, 0, viewport.width, viewport.height), true);
        var point = fit.Unproject(new(input.pointerPosition.x, input.pointerPosition.y));
        float x = point.X, y = point.Y;
        string navigation = preferences?.ResolveNavigationShortcut(input.keysPressed, input.modifiers) ?? NovelNavigationShortcuts.ResolveDefault(input.keysPressed, input.modifiers);
        return new()
        {
            x = x, y = y, pointerInside = input.pointerInside && x >= 0 && y >= 0 && x < width && y < height,
            pressed = pressed, released = released, held = m_held, scroll = input.scrollDelta.y * 48,
            skipHeld = holdingSkip && !m_waitForSkipRelease,
            readingShortcut = preferences?.ResolveKeyboardShortcut(input.keysPressed, input.modifiers) ?? NovelKeyboardShortcuts.ResolveDefault(input.keysPressed, input.modifiers),
            confirm = navigation == "confirm", space = input.keysPressed.Contains(KeyCode.Space) && input.modifiers == KeyModifier.None,
            textConfirm = input.keysPressed.Contains(KeyCode.Enter), textLeft = input.keysPressed.Contains(KeyCode.LeftArrow), textRight = input.keysPressed.Contains(KeyCode.RightArrow),
            cancel = navigation == "cancel", next = input.keysPressed.Contains(KeyCode.Tab) && !input.modifiers.HasFlag(KeyModifier.Shift) || navigation == "next",
            previous = navigation == "previous" || input.keysPressed.Contains(KeyCode.Tab) && input.modifiers.HasFlag(KeyModifier.Shift), increment = navigation == "increase", decrement = navigation == "decrease",
            pageUp = navigation == "pageUp", pageDown = navigation == "pageDown",
            panUp = navigation == "previous", panDown = navigation == "next",
            backspace = input.keysPressed.Contains(KeyCode.Backspace), delete = input.keysPressed.Contains(KeyCode.Delete),
            home = input.keysPressed.Contains(KeyCode.Home), end = input.keysPressed.Contains(KeyCode.End),
            selecting = input.modifiers.HasFlag(KeyModifier.Shift), wordNavigation = input.modifiers == KeyModifier.Control || input.modifiers == (KeyModifier.Control | KeyModifier.Shift),
            selectAll = input.modifiers == KeyModifier.Control && input.keysPressed.Contains(KeyCode.A), text = input.textInput
        };
    }
}
