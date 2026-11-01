using System.Collections.Generic;
using System.Numerics;
using Inno.VisualNovel.UI.Layout;

namespace Inno.VisualNovel.UI.Input;

/// <summary>Contains one viewport-local input sample, independent of host input APIs.</summary>
public sealed class UiInputSnapshot
{
    /// <summary>Maps a sample into a nested fitted canvas, preserving its single-use button transitions.</summary>
    /// <param name="fit">The canvas-to-current-coordinate transform used by rendering.</param>
    /// <returns>A new logical sample with letterbox pointer regions excluded.</returns>
    public UiInputSnapshot InCanvas(UiCanvasFit fit)
    {
        Vector2 point = fit.Unproject(new(x, y));
        return new() { x = point.X, y = point.Y, pointerInside = pointerInside && fit.bounds.Contains(x, y), pressed = pressed, released = released, held = held,
            scroll = scroll / fit.scaleY, confirm = confirm, space = space, cancel = cancel, next = next, previous = previous, increment = increment, decrement = decrement,
            pageUp = pageUp, pageDown = pageDown, panUp = panUp, panDown = panDown, backspace = backspace, delete = delete, home = home, end = end,
            textLeft = textLeft, textRight = textRight, textConfirm = textConfirm,
            selecting = selecting, wordNavigation = wordNavigation, selectAll = selectAll, composing = composing, skipHeld = skipHeld, readingShortcut = readingShortcut, text = text };
    }
    /// <summary>Gets or sets pointer X in design coordinates.</summary>
    public float x { get; init; }
    /// <summary>Gets or sets pointer Y in design coordinates.</summary>
    public float y { get; init; }
    /// <summary>Gets or sets whether the pointer belongs to this viewport.</summary>
    public bool pointerInside { get; init; }
    /// <summary>Gets or sets primary press this frame.</summary>
    public bool pressed { get; init; }
    /// <summary>Gets or sets primary release this frame.</summary>
    public bool released { get; init; }
    /// <summary>Gets or sets whether primary input remains held.</summary>
    public bool held { get; init; }
    /// <summary>Gets or sets logical vertical scrolling.</summary>
    public float scroll { get; init; }
    /// <summary>Gets or sets an up-arrow pan request, distinguished from Tab focus navigation by the host.</summary>
    public bool panUp { get; init; }
    /// <summary>Gets or sets a down-arrow pan request, distinguished from Tab focus navigation by the host.</summary>
    public bool panDown { get; init; }
    /// <summary>Gets or sets confirm this frame.</summary>
    public bool confirm { get; init; }
    /// <summary>Gets or sets physical Enter submission for a text field, independent of remapped menu confirmation.</summary>
    public bool textConfirm { get; init; }
    /// <summary>Gets or sets physical left-arrow text navigation independently of menu or slider bindings.</summary>
    public bool textLeft { get; init; }
    /// <summary>Gets or sets physical right-arrow text navigation independently of menu or slider bindings.</summary>
    public bool textRight { get; init; }
    /// <summary>Gets or sets a space-key activation, which remains text input inside an input control.</summary>
    public bool space { get; init; }
    /// <summary>Gets or sets cancel this frame.</summary>
    public bool cancel { get; init; }
    /// <summary>Gets or sets next focus movement.</summary>
    public bool next { get; init; }
    /// <summary>Gets or sets previous focus movement.</summary>
    public bool previous { get; init; }
    /// <summary>Gets or sets a keyboard request to review the previous page of the active scroll area.</summary>
    public bool pageUp { get; init; }
    /// <summary>Gets or sets a keyboard request to review the next page of the active scroll area.</summary>
    public bool pageDown { get; init; }
    /// <summary>Gets or sets an increase request for the focused numeric control.</summary>
    public bool increment { get; init; }
    /// <summary>Gets or sets a decrease request for the focused numeric control.</summary>
    public bool decrement { get; init; }
    /// <summary>Gets or sets deletion of one preceding grapheme.</summary>
    public bool backspace { get; init; }
    /// <summary>Gets or sets deletion of the selected range or following grapheme.</summary>
    public bool delete { get; init; }
    /// <summary>Gets or sets movement to the start of a focused single-line input.</summary>
    public bool home { get; init; }
    /// <summary>Gets or sets movement to the end of a focused single-line input.</summary>
    public bool end { get; init; }
    /// <summary>Gets or sets selection extension for text navigation and pointer placement.</summary>
    public bool selecting { get; init; }
    /// <summary>Gets or sets word movement or deletion for the focused text input.</summary>
    public bool wordNavigation { get; init; }
    /// <summary>Gets or sets selection of all committed text in the focused input.</summary>
    public bool selectAll { get; init; }
    /// <summary>Gets or sets whether an input method is composing text.</summary>
    public bool composing { get; init; }
    /// <summary>Gets or sets whether the viewport-local hold-to-skip input remains down; it never confirms choices.</summary>
    public bool skipHeld { get; init; }
    /// <summary>Gets or sets at most one supported reading action resolved from exact viewport key and modifier bindings.</summary>
    public string readingShortcut { get; init; } = string.Empty;
    /// <summary>Gets or sets committed Unicode text, never composition fragments.</summary>
    public IReadOnlyList<string> text { get; init; } = [];
}

/// <summary>Contains one neutral control activation.</summary>
/// <param name="action">Stable registered action identity.</param>
/// <param name="argument">Authored neutral argument.</param>
/// <param name="value">An optional exact typed binding value, absent for a fixed literal.</param>
/// <param name="ownerId">The live source control identity, used by runtime diagnostics.</param>
/// <param name="binding">The optional source field path, retained to diagnose an unavailable typed argument.</param>
public sealed record UiActionRequest(string action, string argument, Inno.VisualNovel.Model.Values.NovelValue? value = null, string ownerId = "", string binding = "");

/// <summary>Reports whether a page consumed this sample and which actions it produced.</summary>
/// <param name="consumed">Whether lower pages and story input must stop.</param>
/// <param name="actions">Ordered control activations.</param>
/// <param name="cancelConsumed">Whether an inner modal handled cancellation before shell page navigation.</param>
public sealed record UiInputResult(bool consumed, IReadOnlyList<UiActionRequest> actions, bool cancelConsumed = false);
