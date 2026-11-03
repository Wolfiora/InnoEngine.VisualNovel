using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InnoEngine.Input;
using InnoEngine.Scene;
using InnoEngine.UI;
using Inno.VisualNovel.Integration.Components.Interface;

namespace Inno.VisualNovel.Integration.Interface.Engine;

/// <summary>Projects engine interaction events into component colors; it never performs hit testing or activates controls.</summary>
internal sealed class NovelEngineUiControlFeedback
{
    private readonly HashSet<GameObject> m_hovered = [];
    private GameObject? m_focus;
    private GameObject? m_pointerPress;
    private GameObject? m_keyboardPress;

    internal void Observe(
        UiEvent message,
        GameObject owner
    ) {
        if (message.type == UiEventType.MouseEnter) m_hovered.Add(owner);
        else if (message.type == UiEventType.MouseLeave) m_hovered.Remove(owner);
        else if (message.type == UiEventType.Focus) m_focus = Control(owner);
        else if (message.type == UiEventType.Blur && ReferenceEquals(Control(owner), m_focus)) m_focus = null;
    }

    internal void Input(
        UiInputSnapshot input,
        GameObject? hover
    ) {
        if (input.buttonsPressed.Contains(MouseButton.Left)) m_pointerPress = Control(hover);
        if (input.buttonsReleased.Contains(MouseButton.Left)) m_pointerPress = null;
        if (input.keysPressed.Contains(KeyCode.Enter) || input.keysPressed.Contains(KeyCode.Space)) m_keyboardPress = m_focus;
        if (input.keysReleased.Contains(KeyCode.Enter) || input.keysReleased.Contains(KeyCode.Space)) m_keyboardPress = null;
    }

    internal string Style(
        GameObject owner,
        string normal,
        bool disabled
    ) {
        if (!owner.TryGetComponent<NovelUiControlStyleComponent>(out var palette) || palette is null
            || !ReferenceEquals(Control(owner), owner)) return normal;
        m_hovered.RemoveWhere(static item => item.isDestroyed || !item.activeInHierarchy);
        bool hovered = m_hovered.Any(item => ReferenceEquals(Control(item), owner));
        bool pressed = ReferenceEquals(m_keyboardPress, owner) && ReferenceEquals(m_focus, owner)
            || ReferenceEquals(m_pointerPress, owner) && hovered;
        uint? fill = disabled ? palette.disabled : pressed ? palette.pressed : hovered ? palette.hover : null;
        if (fill.HasValue) normal += "background-color:#" + fill.Value.ToString("X8", CultureInfo.InvariantCulture) + ";";
        return normal + "border-width:2px;border-color:#"
            + (!disabled && ReferenceEquals(m_focus, owner) ? palette.focus : 0u).ToString("X8", CultureInfo.InvariantCulture) + ";";
    }

    internal void Clear()
    { m_hovered.Clear(); m_focus = null; m_pointerPress = null; m_keyboardPress = null; }

    private static GameObject? Control(GameObject? owner)
    {
        while (owner is { isDestroyed: false, activeInHierarchy: true })
        {
            if (owner.TryGetComponent<NovelUiButtonComponent>(out _) || owner.TryGetComponent<NovelUiToggleComponent>(out _)) return owner;
            if (owner.TryGetComponent<NovelUiRootComponent>(out _)) return null;
            owner = owner.transform.parent?.gameObject;
        }
        return null;
    }
}
