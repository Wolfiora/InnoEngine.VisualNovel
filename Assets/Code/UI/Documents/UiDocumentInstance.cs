using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InnoEngine.Mathematics;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.UI.Binding;
using Inno.VisualNovel.UI.Input;
using Inno.VisualNovel.UI.Layout;
using Inno.VisualNovel.UI.Controls.Inputs;
using Inno.VisualNovel.UI.Text;
using Inno.VisualNovel.Model.Text;
using Inno.VisualNovel.Model.Interface.Themes;
using Inno.VisualNovel.UI.Theming;
using Inno.VisualNovel.UI.Animation;

namespace Inno.VisualNovel.UI.Documents;

/// <summary>Owns layout, page-local scrolling, focus and input consumption for one screen.</summary>
public sealed class UiDocumentInstance
{
    private readonly ScreenAsset m_screen;
    private readonly UiElementInstance m_root;
    private readonly List<UiElementInstance> m_order = [];
    private readonly HashSet<string> m_definitionIds;
    private readonly Func<UiElementDefinition, string, (float width, float height)> m_measure;
    private readonly UiTextMeasurement? m_measureRich;
    private readonly UiRichTextResources m_textResources;
    private UiRichTextResources m_frameTextResources;
    private readonly UiFocusController m_focus = new();
    private readonly UiSelectorController m_selector = new();
    private readonly UiScrollBarController m_scrollBars = new();
    private readonly UiTooltipController m_tooltip = new();
    private float m_fontScale = 1;
    private string m_pressed = string.Empty;
    private string m_hover = string.Empty;
    private bool m_initialFocus = true;
    private double m_time;
    private bool m_reducedMotion;
    private bool m_highContrast;
    private string m_previewElement = string.Empty;
    private Model.Interface.Animation.UiMotionPose? m_previewPose;
    private UiFrame? m_lastFrame;
    private IReadOnlyDictionary<string, Model.Interface.Animation.UiMotionPose> m_performance = new Dictionary<string, Model.Interface.Animation.UiMotionPose>();
    private readonly List<(UiStyleDefinition style, float size)> m_fontSizes = [];
    /// <summary>Gets whether a selector currently owns modal input ahead of shell navigation.</summary>
    public bool popupOpen => m_selector.active;
    /// <summary>Gets the current selector binding rejection, empty when healthy.</summary>
    public string bindingError => m_selector.error;
    /// <summary>Changes only the paint palette, preserving focus, selection, geometry and author declarations.</summary>
    /// <param name="enabled">Whether readable opaque text surfaces override authored colors.</param>
    public void SetHighContrast(bool enabled) { if (m_highContrast != enabled) m_lastFrame = null; m_highContrast = enabled; }

    /// <summary>Freezes a screen definition and validates its ownership and geometry.</summary>
    /// <param name="screen">The imported screen, copied by the supplied native generation owner.</param>
    /// <param name="measureText">The host adapter to engine text shaping.</param>
    /// <param name="measureRichText">Optional width-aware adapter sharing the rendering layout.</param>
    /// <param name="theme">An optional theme; absent themes use the page's explicit literal design values.</param>
    public UiDocumentInstance(
        ScreenAsset screen,
        Func<UiElementDefinition, string, (float width, float height)> measureText,
        UiTextMeasurement? measureRichText = null,
        ThemeAsset? theme = null
    ) {
        ArgumentNullException.ThrowIfNull(screen);
        UiStateRules.Validate(screen.stateVariables);
        m_measure = measureText ?? throw new ArgumentNullException(nameof(measureText));
        if (!float.IsFinite(screen.width) || !float.IsFinite(screen.height) || screen.width <= 0 || screen.height <= 0)
            throw new ArgumentException("Screen dimensions must be finite and positive.", nameof(screen));
        m_screen = screen = theme is null ? UiDefinitionCopy.Copy(screen) : new UiThemeResolver(theme).Apply(screen);
        m_measureRich = measureRichText; m_textResources = new(screen.textFonts, screen.textIcons); m_frameTextResources = m_textResources;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var fields = screen.stateVariables.ToDictionary(static field => field.id, StringComparer.Ordinal);
        m_root = Build(screen.root, 0);
        m_definitionIds = ids;
        var imageTargets = UiFocusTargets.Enumerate(screen.root).Where(static element => element.kind == UiElementKind.ImageViewport).Select(static element => element.id).ToHashSet(StringComparer.Ordinal);
        foreach (var element in m_order.Select(static item => item.definition).Where(static element => element.action is "image.zoomIn" or "image.zoomOut" or "image.reset"))
            if (element.argumentBinding.Length == 0 && !imageTargets.Contains(element.argument))
                throw new ArgumentException("Image actions require an existing static image viewport target: " + element.id + "/" + element.argument, nameof(screen));
        if (screen.initialFocus.Length > 0 && !UiFocusTargets.Enumerate(screen.root).Any(item => item.id == screen.initialFocus))
            throw new ArgumentException("Initial focus must name an existing static interactive control: " + screen.initialFocus, nameof(screen));

        UiElementInstance Build(
            UiElementDefinition definition,
            int depth
        ) {
            UiStateRules.ValidateElement(definition, fields);
            definition.animation.Validate();
            definition.font ??= screen.font;
            if (definition.options.Length > 1000 || definition.options.Any(static option => string.IsNullOrWhiteSpace(option.id))
                || definition.options.Select(static option => option.id).Distinct(StringComparer.Ordinal).Count() != definition.options.Length)
                throw new ArgumentException("Selector options require unique stable identities within the 1000-option budget: " + definition.id);
            m_fontSizes.Add((definition.style, definition.style.fontSize));
            if (depth > 64 || m_order.Count >= 10000 || string.IsNullOrWhiteSpace(definition.id) || !ids.Add(definition.id))
                throw new ArgumentException("A screen needs unique IDs and an acyclic, bounded element tree.", nameof(screen));
            UiLayoutDefinition layout = definition.layout;
            if (definition.kind == UiElementKind.ImageViewport && definition.imageFit == UiImageFit.NineSlice)
                throw new ArgumentException("Image viewports support Stretch, Contain or Cover; NineSlice belongs to ordinary UI imagery: " + definition.id);
            definition.imageSlice.Validate(definition.imageFit == UiImageFit.NineSlice ? definition.image?.width ?? 0 : 0, definition.imageFit == UiImageFit.NineSlice ? definition.image?.height ?? 0 : 0);
            float[] geometry = [layout.x, layout.y, layout.width, layout.height, layout.minWidth, layout.minHeight, layout.maxWidth, layout.maxHeight,
                layout.anchorX, layout.anchorY, layout.pivotX, layout.pivotY, layout.padding, layout.gap,
                layout.marginLeft, layout.marginTop, layout.marginRight, layout.marginBottom, definition.style.fontSize, definition.style.textPadding, definition.style.opacity];
            if (geometry.Any(static value => !float.IsFinite(value)) || layout.width < 0 || layout.height < 0 || layout.minWidth < 0 || layout.minHeight < 0
                || layout.maxWidth < layout.minWidth || layout.maxHeight < layout.minHeight || layout.padding < 0 || layout.gap < 0 || layout.columns < 1
                || definition.style.fontSize <= 0 || definition.style.textPadding < 0 || definition.style.opacity is < 0 or > 1 || definition.maxLength < 0
                || (definition.styleOverrides & ~UiStyleProperty.All) != 0
                || !double.IsFinite(definition.minimum) || !double.IsFinite(definition.maximum) || !double.IsFinite(definition.step) || definition.step < 0 || definition.minimum > definition.maximum
                || !float.IsFinite(definition.style.lineSpacing) || definition.style.lineSpacing is < 0.5f or > 5
                || !Enum.IsDefined(definition.style.alignment) || !Enum.IsDefined(definition.kind) || !Enum.IsDefined(layout.widthMode) || !Enum.IsDefined(layout.heightMode) || !Enum.IsDefined(definition.imageFit))
                throw new ArgumentException($"Screen element '{definition.id}' has invalid layout or control values.", nameof(screen));
            if (layout.marginLeft is < 0 or > 100000 || layout.marginTop is < 0 or > 100000 || layout.marginRight is < 0 or > 100000 || layout.marginBottom is < 0 or > 100000
                || !Enum.IsDefined(layout.horizontalAlignment) || !Enum.IsDefined(layout.verticalAlignment) || !Enum.IsDefined(layout.flowAlignment))
                throw new ArgumentException("Screen margins or container alignment are invalid: " + definition.id, nameof(screen));
            var instance = new UiElementInstance(definition);
            if (definition.tooltip.Length > 4096) throw new ArgumentException("UI help text exceeds 4096 UTF-16 characters: " + definition.id);
            if (definition.richText) _ = NovelTextMarkup.Parse(definition.text);
            m_order.Add(instance);
            instance.children = definition.children.Select(child => Build(child, depth + 1)).ToArray();
            return instance;
        }
    }

    /// <summary>Gets the stable focus identity, or an empty value.</summary>
    public string focusedElement => m_focus.id;
    /// <summary>Gets whether the resolved focus owns committed text, suppressing narrative reading shortcuts.</summary>
    public bool textInputFocused => m_order.Any(item => item.id == m_focus.id && item.inputVisible && item.enabled && item.definition.kind == UiElementKind.Input);
    /// <summary>Reads the active modal search query or full last-resolved input value without exposing its clipped paint window.</summary>
    /// <param name="text">Receives the complete literal value, or empty when no eligible input owns focus.</param>
    /// <returns>Whether a modal selector query or visible enabled text field owns text entry.</returns>
    public bool TryGetFocusedInputText(out string text)
    {
        if (m_selector.active) { text = m_selector.query; return true; }
        var input = m_order.FirstOrDefault(item => item.id == m_focus.id && item.inputVisible && item.enabled && item.definition.kind == UiElementKind.Input);
        text = input?.text ?? string.Empty; return input is not null;
    }
    /// <summary>Requests the authored starting control the next time layout and bindings have resolved.</summary>
    public void RequestInitialFocus() => m_initialFocus = true;
    /// <summary>Restarts end following for scroll areas explicitly bound to a follow-end condition.</summary>
    public void FollowScrollEnds()
    {
        foreach (var item in m_order.Where(static item => item.definition.kind == UiElementKind.Scroll && item.definition.followEndBinding.Length > 0))
        { item.followTail = true; item.previousContentHeight = -1; }
    }

    /// <summary>Changes text scale relative to the frozen authored sizes.</summary>
    /// <param name="scale">A finite scale between 0.5 and 4.</param>
    public void SetFontScale(float scale)
    {
        if (!float.IsFinite(scale) || scale is < 0.5f or > 4) throw new ArgumentOutOfRangeException(nameof(scale));
        if (scale != m_fontScale) { m_focus.RequestReveal(); m_fontScale = scale; }
        foreach (var item in m_fontSizes) item.style.fontSize = item.size * scale;
    }

    /// <summary>Sets the session accessibility policy; reduced motion snaps ordinary states and suppresses loops.</summary>
    /// <param name="reduced">Whether the player requests reduced motion.</param>
    public void SetReducedMotion(bool reduced) => m_reducedMotion = reduced;

    /// <summary>Installs frozen shared-story-clock presentation without changing transient interaction time.</summary>
    /// <param name="values">Relative poses keyed by existing authored elements; empty values remove prior track ownership.</param>
    public void SetPerformanceValues(IReadOnlyDictionary<string, Model.Interface.Animation.UiMotionPose> values)
    {
        var candidate = new Dictionary<string, Model.Interface.Animation.UiMotionPose>(StringComparer.Ordinal);
        foreach (var pair in values)
        {
            if (!m_definitionIds.Contains(pair.Key)) throw new InvalidOperationException("The animated UI element is missing: " + pair.Key);
            pair.Value.Validate(); candidate.Add(pair.Key, pair.Value.Copy());
        }
        m_performance = candidate;
    }

    /// <summary>Restarts transient entry presentation without changing bound data, scroll, or persistent page state.</summary>
    public void Enter()
    {
        CancelInput(); m_time = 0; m_lastFrame = null;
        foreach (var element in m_order) element.animation = new();
    }

    /// <summary>Freezes the outgoing page without retaining its data source or any input overlays.</summary>
    /// <returns>An input-free presentation owner; reduced motion completes immediately.</returns>
    public UiPageExit BeginExit()
    {
        UiFrame frame = m_lastFrame ?? new(m_screen.width, m_screen.height, []);
        var draws = frame.items.ToDictionary(static item => item.id, StringComparer.Ordinal);
        var elements = m_order.Where(item => item.visible && draws.ContainsKey(item.id)).Select(item => new UiPageExit.Element(draws[item.id], item.parent?.id ?? string.Empty,
            item.definition.layout.clip || item.definition.kind == UiElementKind.Scroll, new(item.definition.layout.pivotX, item.definition.layout.pivotY),
            m_reducedMotion ? new() : item.definition.animation.exit.Copy())).ToArray();
        CancelInput();
        return new(m_screen.width, m_screen.height, elements);
    }

    /// <summary>Seeks one authored event without advancing live time or executing any UI action.</summary>
    /// <param name="source">The explicit preview data.</param>
    /// <param name="elementId">The authored element identity, including all instances in a repeated template.</param>
    /// <param name="kind">The event to sample.</param>
    /// <param name="seconds">The finite nonnegative event-local time.</param>
    /// <returns>A pure preview using the ordinary runtime layout and presentation path.</returns>
    public UiFrame PreviewAnimation(
        UiDataSource source,
        string elementId,
        Model.Interface.Animation.UiAnimationEvent kind,
        double seconds
    ) {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        var element = m_order.FirstOrDefault(item => item.definition.id == elementId) ?? throw new ArgumentException("The preview element is missing: " + elementId, nameof(elementId));
        var motion = element.definition.animation.GetMotion(kind);
        double time = seconds;
        if (kind == Model.Interface.Animation.UiAnimationEvent.Waiting && motion.duration > 0 && time >= motion.delay)
        {
            double phase = (time - motion.delay) % (motion.duration * 2);
            time = motion.delay + (phase <= motion.duration ? phase : motion.duration * 2 - phase);
        }
        m_previewElement = elementId; m_previewPose = m_reducedMotion ? new() : UiMotionSampling.Sample(motion, time, kind == Model.Interface.Animation.UiAnimationEvent.Enter);
        try { return BuildFrame(source, false); }
        finally { m_previewElement = string.Empty; m_previewPose = null; }
    }

    /// <summary>Advances transient presentation and pointer dwell without executing UI actions.</summary>
    /// <param name="seconds">The finite nonnegative interval while this page owns input.</param>
    public void Tick(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0 || !double.IsFinite(m_time + seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
        m_time += seconds; m_tooltip.Tick(seconds);
    }

    /// <summary>Resolves bindings and layout into a frame without executing actions.</summary>
    /// <param name="source">The current typed game data.</param>
    /// <returns>Detached immutable painting inputs.</returns>
    public UiFrame BuildFrame(UiDataSource source) => BuildFrame(source, true);

    /// <summary>Resolves a page with an explicit policy for transient selector and help overlays.</summary>
    /// <param name="source">The current typed game data.</param>
    /// <param name="includeTransient">Whether to paint nonpersistent input overlays; save capture passes false.</param>
    /// <returns>Detached painting inputs without executing any action.</returns>
    public UiFrame BuildFrame(
        UiDataSource source,
        bool includeTransient
    )
    {
        m_frameTextResources = source.ResolveTextResources(m_textResources);
        RefreshRows(m_root, source);
        m_order.Clear();
        Flatten(m_root);
        if (m_order.Count > 10000 || m_order.Select(static item => item.id).Distinct(StringComparer.Ordinal).Count() != m_order.Count)
            throw new InvalidOperationException("Repeated UI rows exceeded the page budget or produced duplicate identities.");
        Resolve(m_root, true, source, false, includeTransient, true);
        UiLayoutPass.Measure(m_root, Measure, m_screen.width);
        UiLayoutPass.Arrange(m_root, new(0, 0, m_screen.width, m_screen.height), new(0, 0, m_screen.width, m_screen.height));
        if (m_initialFocus) { m_initialFocus = false; if (m_screen.initialFocus.Length > 0) m_focus.Focus(m_screen.initialFocus, m_order); }
        m_focus.EnsureValid(m_order);
        m_focus.Reveal(m_order, m_root, m_screen.width, m_screen.height);
        Present(m_root, Matrix.identity, new(0, 0, m_screen.width, m_screen.height), 1, includeTransient);
        m_selector.Validate(m_order);
        var draws = m_order.Where(static item => item.visible).Select(item =>
        {
            UiElementDefinition definition = item.definition;
            UiStyleDefinition style = definition.style;
            Rect? imageBounds = null;
            if (item.imageViewport is { } viewport)
            {
                viewport.Synchronize((object?)item.image ?? item.bitmap, item.image?.width ?? item.bitmap?.width ?? 0,
                    item.image?.height ?? item.bitmap?.height ?? 0, item.bounds, definition.imageFit);
                if (viewport.available) imageBounds = viewport.bounds;
            }
            bool selected = definition.action.Length > 0 && source.IsActionSelected(ActionRequest(item, source));
            uint color = !item.enabled ? style.disabled : m_pressed == item.id ? style.pressed : m_hover == item.id ? style.hover : selected ? style.pressed : style.background;
            if (definition.kind is UiElementKind.Text or UiElementKind.Spacer or UiElementKind.Horizontal or UiElementKind.Vertical or UiElementKind.Grid or UiElementKind.Scroll) color = 0;
            string paintedText = item.text; UiRichText? paintedRich = item.richText; UiTextInputPresentation? inputPaint = null;
            if (item.textInput is { } editor)
            {
                editor.Synchronize(item.text);
                var painted = editor.Paint(item.bounds, style.textPadding, Math.Max(1, MeasureInput(item, "M").height), style.alignment,
                    text => MeasureInput(item, text), includeTransient && m_focus.id == item.id && item.enabled && item.inputVisible, includeTransient);
                paintedText = painted.text; paintedRich = new(NovelTextDocument.Literal(paintedText));
                if (includeTransient) inputPaint = painted.presentation;
            }
            return new UiDrawItem(item.id, definition.kind, item.bounds, item.visualClip, Opacity(color, style.opacity), Opacity(item.enabled ? style.foreground : style.disabledForeground, style.opacity), paintedText,
                definition.font ?? m_screen.font, style.fontSize, style.textPadding, item.image, m_focus.id == item.id, style.focus,
                definition.maximum > definition.minimum && double.TryParse(item.text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double value)
                    ? Math.Clamp((value - definition.minimum) / (definition.maximum - definition.minimum), 0, 1) : 0, style.lineSpacing, definition.imageFit,
                paintedRich, m_frameTextResources, style.alignment, m_fontScale, item.behindStage, item.bitmap, Opacity(style.imageTint, style.opacity), definition.imageSlice.Copy(), imageBounds, item.presentation, item.visualOpacity, inputPaint);
        }).ToArray();
        if (m_highContrast) draws = draws.Select(UiHighContrastPalette.Apply).ToArray();
        if (includeTransient) m_lastFrame = new(m_screen.width, m_screen.height, Array.AsReadOnly(draws));
        var overlays = (includeTransient ? m_selector.Build(m_screen.width, m_screen.height, source) : [])
            .Concat(!includeTransient || m_selector.active ? [] : m_tooltip.Build(m_order.FirstOrDefault(item => item.id == m_hover),
                m_order.FirstOrDefault(item => item.id == m_focus.id), m_screen.width, m_screen.height, m_measure, m_measureRich, m_frameTextResources, m_fontScale)).ToArray();
        if (m_highContrast) overlays = overlays.Select(UiHighContrastPalette.Apply).ToArray();
        var completeDraws = (includeTransient ? m_scrollBars.Paint(draws, m_order, m_highContrast) : draws).Concat(overlays).ToArray();
        return new(m_screen.width, m_screen.height, Array.AsReadOnly(completeDraws));

        (float width, float height) Measure(UiElementInstance element, float width)
        {
            UiRichText content = element.richText ?? new(NovelTextDocument.Literal(element.text));
            if (m_measureRich is null) { var measured = m_measure(element.definition, content.document.text); element.revealHeight = measured.height; return measured; }
            var rich = m_measureRich(element.definition, content, m_frameTextResources, Math.Max(0, width - element.definition.style.textPadding * 2), m_fontScale);
            element.revealHeight = rich.visibleHeight; return (rich.width, rich.height);
        }
    }

    private (float width, float height) MeasureInput(
        UiElementInstance element,
        string text
    ) {
        UiElementDefinition definition = element.textInput!.Measurement(element.definition); definition.font ??= m_screen.font;
        if (m_measureRich is null) return m_measure(definition, text);
        var measured = m_measureRich(definition, new(NovelTextDocument.Literal(text)), m_frameTextResources, 1_000_000_000, m_fontScale);
        return (measured.width, measured.height);
    }

    /// <summary>Routes one input sample to the frontmost eligible control.</summary>
    /// <param name="input">The current design-space sample.</param>
    /// <param name="source">The bound data owner.</param>
    /// <returns>Consumption and ordered neutral actions.</returns>
    public UiInputResult HandleInput(
        UiInputSnapshot input,
        UiDataSource source
    ) {
        _ = BuildFrame(source);
        m_tooltip.Observe(input);
        if (m_selector.active) { m_tooltip.Cancel(); return m_selector.Handle(input, source); }
        bool typing = textInputFocused && input.text.Count > 0;
        if (m_scrollBars.Handle(input, m_order))
        {
            m_pressed = m_hover = string.Empty; m_tooltip.Cancel();
            foreach (var item in m_order) item.imageViewport?.EndDrag();
            return new(true, []);
        }
        var imageFocus = m_order.FirstOrDefault(item => item.id == m_focus.id && UiFocusController.Eligible(item) && item.imageViewport is not null);
        if (!input.composing && imageFocus is not null && input.pageUp != input.pageDown)
        { ZoomImage(imageFocus.id, input.pageUp ? 1 : -1); if (input.released) CancelInput(); return new(true, []); }
        if (!input.composing && imageFocus is not null && input.panUp != input.panDown)
        { imageFocus.imageViewport!.Pan(0, (input.panUp ? 1 : -1) * imageFocus.bounds.height * .1f); if (input.released) CancelInput(); return new(true, []); }
        if (!input.composing && !typing && input.pageUp != input.pageDown && UiScrollController.Page(m_order, m_focus.id, input.pageUp))
        { if (input.released) m_pressed = string.Empty; return new(true, []); }
        var actions = new List<UiActionRequest>();
        bool activated = false;
        UiElementInstance? hit = input.pointerInside ? m_order.LastOrDefault(item => item.inputVisible && item.visualOpacity > .001f && !item.behindStage && item.definition.blocksPointer && item.visualBounds.Contains(input.x, input.y) && item.visualClip.Contains(input.x, input.y)) : null;
        m_hover = hit?.id ?? string.Empty;
        bool consumed = m_screen.modal;
        if (input.scroll != 0 && hit is { enabled: true, imageViewport: { } wheelImage })
        { Vector2 point = Pointer(hit); wheelImage.Zoom(input.scroll / 48, point.x, point.y); if (input.released) CancelInput(); return new(true, []); }
        if (input.scroll != 0 && hit is not null)
        {
            UiElementInstance? scroll = m_order.LastOrDefault(item => item.inputVisible && item.definition.kind == UiElementKind.Scroll && item.visualClip.Contains(input.x, input.y));
            if (scroll is not null)
            {
                UiScrollController.Move(scroll, -input.scroll); consumed = true;
            }
        }
        if (input.pressed)
        {
            consumed |= hit is not null;
            m_pressed = hit is { enabled: true } && UiFocusController.Eligible(hit) ? hit.id : string.Empty;
            if (m_pressed.Length > 0) m_focus.Focus(m_pressed, m_order);
            if (hit is { enabled: true, imageViewport: { } pressedImage }) { Vector2 point = Pointer(hit); pressedImage.BeginDrag(point.x, point.y); }
            if (hit is { enabled: true, textInput: { } pressedText })
            { Vector2 point = Pointer(hit); pressedText.Point(point.x - hit.bounds.x - hit.definition.style.textPadding, Math.Max(0, hit.bounds.width - hit.definition.style.textPadding * 2), text => MeasureInput(hit, text), input.selecting, false); }
        }
        UiElementInstance? active = m_order.FirstOrDefault(item => item.id == m_pressed);
        if (active is { enabled: true, inputVisible: true, imageViewport: { } imageView } && (input.held || input.released))
        { Vector2 point = Pointer(active); imageView.Drag(point.x, point.y); consumed = true; if (input.released) imageView.EndDrag(); }
        if (active is { enabled: true, inputVisible: true, textInput: { } draggedText } && input.pointerInside && !input.pressed && (input.held || input.released))
        { Vector2 point = Pointer(active); draggedText.Point(point.x - active.bounds.x - active.definition.style.textPadding, Math.Max(0, active.bounds.width - active.definition.style.textPadding * 2), text => MeasureInput(active, text), true, input.held); consumed = true; }
        if (active is { enabled: true } && active.definition.kind == UiElementKind.Slider && (input.pressed || input.held))
        {
            double amount = Math.Clamp((input.x - active.visualBounds.x) / Math.Max(1, active.visualBounds.width), 0, 1);
            SliderValueWriter.Apply(source, active.definition, active.definition.minimum * (1 - amount) + active.definition.maximum * amount, active.row);
            consumed = true;
        }
        if (input.released)
        {
            consumed |= hit is not null || m_pressed.Length > 0;
            if (active is not null && hit == active && active.definition.kind is not (UiElementKind.Input or UiElementKind.ImageViewport)) Activate(active);
            m_pressed = string.Empty;
        }
        if (!input.composing && !typing && (input.next || input.previous))
        {
            consumed |= m_focus.Navigate(input.previous, m_order);
        }
        UiElementInstance? focused = m_order.FirstOrDefault(item => item.id == m_focus.id && UiFocusController.Eligible(item));
        if (!input.composing && focused?.definition.kind == UiElementKind.Slider && (input.increment || input.decrement))
        { SliderValueWriter.Move(source, focused.definition, (input.increment ? 1 : 0) - (input.decrement ? 1 : 0), focused.row); consumed = true; }
        if (!input.composing && focused?.imageViewport is { } focusedImage && (input.increment || input.decrement))
        { focusedImage.Pan((input.increment ? -1 : 1) * focused.bounds.width * .1f, 0); consumed = true; }
        if (focused?.definition.kind == UiElementKind.Input)
        {
            int maximum = Math.Clamp(source.GetTextLimit(focused.definition.binding, focused.definition.maxLength), 0, focused.definition.maxLength);
            focused.textInput!.Synchronize(focused.text);
            consumed |= focused.textInput.Edit(input, maximum, text => source.TryWrite(focused.definition.binding, NovelValue.String(text), focused.row));
        }
        if (!input.composing && focused is not null && (focused.definition.kind == UiElementKind.Input ? input.textConfirm : input.confirm || input.space))
        { m_focus.RequestReveal(); m_focus.Reveal(m_order, m_root, m_screen.width, m_screen.height); if (focused.imageViewport is { } confirmedImage) confirmedImage.Reset(); else Activate(focused); consumed = true; }
        Present(m_root, Matrix.identity, new(0, 0, m_screen.width, m_screen.height), 1, true);
        return new(consumed, actions.AsReadOnly(), typing && input.cancel);

        Vector2 Pointer(UiElementInstance item) => new(item.bounds.x + (input.x - item.visualBounds.x) * item.bounds.width / Math.Max(.001f, item.visualBounds.width),
            item.bounds.y + (input.y - item.visualBounds.y) * item.bounds.height / Math.Max(.001f, item.visualBounds.height));

        void Activate(UiElementInstance element)
        {
            if (!element.enabled || activated) return;
            activated = true;
            UiElementDefinition definition = element.definition;
            if (definition.action.Length > 0 && !source.CanInvoke(ActionRequest(element, source))) return;
            if (definition.kind == UiElementKind.Selector) { m_selector.Open(element, source); return; }
            if (definition.kind == UiElementKind.Toggle && Read(element, definition.binding, source, out NovelValue value) && value.kind == NovelValueKind.Boolean)
                source.TryWrite(definition.binding, NovelValue.Boolean(!value.boolean), element.row);
            if (definition.action.Length > 0)
            {
                var request = ActionRequest(element, source);
                if (source.CanInvoke(request)) actions.Add(request);
            }
        }
    }

    /// <summary>Restores focus to an existing eligible control after closing a modal page.</summary>
    /// <param name="id">The page-local stable element identity.</param>
    /// <returns>Whether the target accepted focus.</returns>
    public bool Focus(string id) => m_focus.Focus(id, m_order);

    /// <summary>Releases pressed controls and pointer drags when the page loses input ownership.</summary>
    /// <param name="preserveTextEntry">Whether an explicit native text bridge retains the active selector query; ordinary focus loss closes it.</param>
    public void CancelInput(bool preserveTextEntry = false)
    {
        m_pressed = m_hover = string.Empty; if (!preserveTextEntry) m_selector.Close();
        m_scrollBars.Cancel();
        m_tooltip.Cancel();
        foreach (var element in m_order) element.imageViewport?.EndDrag();
    }

    /// <summary>Zooms a resolved image viewport around its center, retaining clipped input ownership.</summary>
    /// <param name="id">The live page-local viewport identity.</param>
    /// <param name="steps">Positive or negative exponential zoom steps, clamped to 100–1600 percent.</param>
    /// <returns>Whether a visible available image accepted the request.</returns>
    public bool ZoomImage(
        string id,
        float steps
    ) {
        var element = ImageView(id);
        if (!element.visible || !element.enabled || !element.imageViewport!.available) return false;
        element.imageViewport.Zoom(steps, element.bounds.x + element.bounds.width / 2, element.bounds.y + element.bounds.height / 2); return true;
    }

    /// <summary>Restores one image to its authored fit without changing collection or story state.</summary>
    /// <param name="id">The live page-local viewport identity.</param>
    public void ResetImage(string id) => ImageView(id).imageViewport!.Reset();
    /// <summary>Resets transient image interaction when entering a new gallery detail.</summary>
    public void ResetImageViews() { CancelInput(); foreach (var element in m_order) element.imageViewport?.Reset(); }
    /// <summary>Reads the transient zoom for debugging and explicit host integrations.</summary>
    /// <param name="id">The live page-local viewport identity.</param>
    /// <returns>The current zoom multiplier.</returns>
    public float GetImageZoom(string id) => ImageView(id).imageViewport!.zoom;

    private UiElementInstance ImageView(string id) => m_order.SingleOrDefault(item => item.id == id && item.imageViewport is not null)
        ?? throw new InvalidOperationException("The requested image viewport is unavailable: " + id);

    private static uint Opacity(uint color, float opacity) => color & 0xFFFFFF00 | (uint)Math.Round((color & 255) * opacity);

    private void Resolve(
        UiElementInstance element,
        bool parentVisible,
        UiDataSource source,
        bool parentBehindStage,
        bool animate,
        bool parentPainting
    ) {
        UiElementDefinition definition = element.definition;
        element.behindStage = parentBehindStage || definition.behindStage;
        bool ownVisible = Boolean(definition.visibleBinding, true);
        element.inputVisible = parentVisible && ownVisible;
        element.animation.ResolveVisibility(definition.animation, element.inputVisible, m_time, m_reducedMotion);
        element.visible = animate ? element.animation.visible || parentPainting && !parentVisible && ownVisible : element.inputVisible;
        element.waiting = definition.animation.waitingBinding.Length == 0;
        if (definition.animation.waitingBinding.Length > 0 && Read(element, definition.animation.waitingBinding, source, out var waiting))
        {
            if (waiting.kind != NovelValueKind.Boolean) throw new InvalidOperationException("UI waiting motion requires Boolean data: " + element.id + "/" + definition.animation.waitingBinding);
            element.waiting = waiting.boolean;
        }
        element.enabled = definition.interactable && Boolean(definition.enabledBinding, true);
        if (element.enabled && definition.action.Length > 0) element.enabled = source.CanInvoke(ActionRequest(element, source));
        element.followEnd = definition.followEndBinding.Length > 0 && Boolean(definition.followEndBinding, false);
        definition.layout.x = Geometry(definition.geometryBinding.x, element.authoredGeometry.x, false, m_screen.width);
        definition.layout.y = Geometry(definition.geometryBinding.y, element.authoredGeometry.y, false, m_screen.height);
        definition.layout.width = Geometry(definition.geometryBinding.width, element.authoredGeometry.width, true, m_screen.width);
        definition.layout.height = Geometry(definition.geometryBinding.height, element.authoredGeometry.height, true, m_screen.height);
        element.image = source.ResolveImage(definition.imageBinding.Length > 0 ? source.ReadImage(definition.imageBinding, element.row) : definition.image);
        element.bitmap = definition.imageBinding.Length > 0 && element.image is null ? source.ReadBitmap(definition.imageBinding, element.row) : null;
        element.text = definition.binding.Length > 0 && Read(element, definition.binding, source, out NovelValue value) ? value.ToString() : source.Localize(definition.textId, definition.text);
        element.tooltip = source.Localize(definition.tooltipTextId, definition.tooltip);
        if (definition.tooltipBinding.Length > 0 && Read(element, definition.tooltipBinding, source, out NovelValue help))
        {
            if (help.kind != NovelValueKind.String) throw new InvalidOperationException("Tooltip bindings require String data: " + element.id + "/" + definition.tooltipBinding);
            element.tooltip = help.text;
        }
        if (element.tooltip.Length > 4096) throw new InvalidOperationException("Resolved UI help text exceeds 4096 UTF-16 characters: " + element.id);
        if (definition.kind == UiElementKind.Selector) element.text = UiSelectorController.Label(definition, source, Read(element, definition.binding, source, out NovelValue selected) ? selected : null);
        element.richText = (definition.binding.Length > 0 ? source.ReadRichText(definition.binding, element.row) : null)
            ?? new(definition.richText ? NovelTextMarkup.Parse(element.text) : NovelTextDocument.Literal(element.text));
        foreach (UiElementInstance child in element.children) Resolve(child, element.inputVisible, source, element.behindStage, animate, element.visible);
        if (animate && element.children.Any(static child => child.visible)) element.visible = true;

        bool Boolean(string path, bool fallback) => path.Length == 0 ? fallback : Read(element, path, source, out NovelValue value) && value.kind == NovelValueKind.Boolean && value.boolean;
        float Geometry(string path, float fallback, bool positive, float canvasSize)
        {
            if (path.Length == 0 || !Read(element, path, source, out NovelValue value)) return fallback;
            if (value.kind is not (NovelValueKind.Number or NovelValueKind.Integer) || !double.IsFinite(value.number) || Math.Abs(value.number) > 100000 || positive && value.number < 0)
                throw new InvalidOperationException("UI geometry requires a finite numeric field: " + element.id + "/" + path);
            return (float)value.number * (definition.geometryBinding.normalized ? canvasSize : 1);
        }
    }

    private void Present(
        UiElementInstance element,
        Matrix parent,
        Rect clip,
        float opacity,
        bool animate
    ) {
        var definition = element.definition;
        var pose = animate ? element.animation.Sample(definition.animation, m_time, element.enabled && element.inputVisible && m_hover == element.id,
            element.enabled && element.inputVisible && m_focus.id == element.id, element.enabled && element.inputVisible && m_pressed == element.id,
            element.inputVisible && element.waiting, m_reducedMotion) : new Model.Interface.Animation.UiMotionPose();
        if (definition.id == m_previewElement && m_previewPose is not null) pose = m_previewPose;
        if (m_performance.TryGetValue(definition.id, out var performance)) pose = UiMotionSampling.Combine(pose, performance);
        element.presentation = parent * UiMotionSampling.Transform(element.bounds, new(definition.layout.pivotX, definition.layout.pivotY), pose);
        element.visualBounds = UiMotionSampling.Bounds(element.bounds, element.presentation);
        Rect.TryIntersect(element.visualBounds, clip, out element.visualClip);
        element.visualOpacity = opacity * pose.opacity;
        element.childClip = definition.layout.clip || definition.kind == UiElementKind.Scroll ? element.visualClip : clip;
        foreach (var child in element.children) Present(child, element.presentation, element.childClip, element.visualOpacity, animate);
    }

    private static UiActionRequest ActionRequest(
        UiElementInstance element,
        UiDataSource source
    ) {
        var definition = element.definition; NovelValue? value = null;
        if (definition.argumentBinding.Length > 0 && Read(element, definition.argumentBinding, source, out var bound)) value = bound.Copy();
        return new(definition.action, value?.ToString() ?? definition.argument, value, element.id, definition.argumentBinding);
    }

    private static bool Read(
        UiElementInstance element,
        string path,
        UiDataSource source,
        out NovelValue value
    ) => source.TryRead(path, element.row, out value);

    private void Flatten(
        UiElementInstance item,
        UiElementInstance? parent = null
    ) {
        item.parent = parent;
        m_order.Add(item);
        foreach (UiElementInstance child in item.children) Flatten(child, item);
    }

    private static void RefreshRows(
        UiElementInstance element,
        UiDataSource source
    ) {
        if (element.definition.repeatBinding.Length > 0)
        {
            var rows = source.ReadRows(element.definition.repeatBinding);
            if (rows.Count > 2000 || rows.Select(static row => row.key).Distinct(StringComparer.Ordinal).Count() != rows.Count)
                throw new InvalidOperationException("UI rows require unique keys within the bounded page collection.");
            string keys = string.Join("", rows.Select(static row => row.key.Length + ":" + row.key));
            if (keys != element.repeatedKeys)
            {
                var previous = element.children.ToDictionary(static child => child.id, StringComparer.Ordinal);
                element.children = rows.SelectMany(row => element.definition.children.Select(template =>
                {
                    string prefix = element.id + "/" + row.key.Length + ":" + row.key + "/";
                    return previous.TryGetValue(prefix + template.id, out var existing) ? existing : Build(UiDefinitionCopy.Copy(new ScreenAsset { root = template }).root, prefix, row);
                })).ToArray();
                element.repeatedKeys = keys;
            }
            int templates = element.definition.children.Length;
            for (int index = 0; index < element.children.Length; index++) SetRow(element.children[index], rows[index / templates]);
        }
        foreach (UiElementInstance child in element.children) RefreshRows(child, source);

        static UiElementInstance Build(UiElementDefinition definition, string prefix, UiDataRow row) => new(definition)
        {
            id = prefix + definition.id, row = row,
            children = definition.children.Select(child => Build(child, prefix, row)).ToArray()
        };
        static void SetRow(UiElementInstance instance, UiDataRow row)
        {
            instance.row = row;
            foreach (UiElementInstance child in instance.children) SetRow(child, row);
        }
    }
}
