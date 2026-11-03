using System;
using System.Collections.Generic;
using InnoEngine.Mathematics;
using InnoEngine.Scene;
using InnoEngine.UI;
using Inno.VisualNovel.Integration.Components.Interface;
using Inno.VisualNovel.UI.Binding;
using Inno.VisualNovel.UI.Input;
using EngineInput = InnoEngine.UI.UiInputSnapshot;

namespace Inno.VisualNovel.Integration.Interface.Engine;

/// <summary>Owns one engine UI context projected from a native canvas, without an independent input or layout engine.</summary>
public sealed class NovelEngineUiSurface : IDisposable
{
    private readonly NovelUiRootComponent m_root;
    private readonly IUiService m_ui;
    private readonly UiContextHandle m_context;
    private UiDocumentHandle m_document;
    private UiDocumentAsset? m_template;
    private long m_templateVersion;
    private string m_slot = string.Empty;
    private NovelEngineUiProjection? m_projection;
    private int m_width, m_height;
    private bool m_visible, m_disposed;
    private GameObject? m_hover;
    private readonly NovelEngineUiControlFeedback m_feedback = new();
    private readonly NovelEngineUiFocusMemory m_focus = new();
    private readonly Dictionary<string, string> m_appliedStyles = new(StringComparer.Ordinal);
    private Vector2 m_lastPointer = new(-100000, -100000);
    internal bool modal => m_visible && m_projection?.modal == true;
    internal bool hasControls => m_visible && m_projection?.hasControls == true;
    internal void CancelInput()
    {
        if (m_visible) m_ui.HideDocument(m_context, m_document);
        m_visible = false; m_hover = null; m_lastPointer = new(-100000, -100000);
        m_feedback.Clear(); m_appliedStyles.Clear(); m_ui.DrainEvents(m_context);
    }

    /// <summary>Creates an owned engine context; it never starts or advances story execution.</summary>
    /// <param name="root">The real scene-owned canvas.</param>
    /// <param name="service">The host's public UI service; its lifetime must enclose this surface.</param>
    public NovelEngineUiSurface(
        NovelUiRootComponent root,
        IUiService service
    ) {
        m_root = root ?? throw new ArgumentNullException(nameof(root));
        m_ui = service ?? throw new ArgumentNullException(nameof(service));
        m_width = Dimension(root.width); m_height = Dimension(root.height);
        m_context = service.CreateContext(new UiContextOptions("VN " + root.gameObject.name, m_width, m_height));
    }

    /// <summary>Reconciles native objects and bindings, then obtains the engine's immutable mesh/resource frame.</summary>
    /// <param name="source">Optional game data; null uses author-provided text without advancing a session.</param>
    /// <returns>The engine UI frame in canvas-local pixels, including incremental resource retirements.</returns>
    public UiRenderFrame Capture(UiDataSource? source = null)
    {
        if (Synchronize(source)) RealizeLayout();
        ApplyFeedback();
        return m_ui.Render(m_context);
    }

    /// <summary>Routes input through engine UI and resolves its actions back to current native objects.</summary>
    /// <param name="input">Single-use viewport-local native input.</param>
    /// <param name="source">The active game's typed data.</param>
    /// <returns>Actions emitted by live controls, in engine event order; story dispatch remains with the session owner.</returns>
    public IReadOnlyList<UiActionRequest> Handle(
        EngineInput input,
        UiDataSource? source = null
    ) {
        ArgumentNullException.ThrowIfNull(input);
        m_lastPointer = input.mousePosition;
        if (Synchronize(source)) RealizeLayout();
        if (!m_visible) return [];
        m_ui.Update(m_context, input);
        var actions = new List<UiActionRequest>();
        var activated = new HashSet<Guid>();
        foreach (var message in m_ui.DrainEvents(m_context))
        {
            ObserveHover(message);
            if (message.document != m_document || message.type != UiEventType.Click || m_projection is null
                || !m_projection.targets.TryGetValue(message.targetId, out var owner) || !IsCurrent(owner)) continue;
            while (!owner.TryGetComponent<NovelUiButtonComponent>(out _) && !owner.TryGetComponent<NovelUiToggleComponent>(out _) && owner.transform.parent is { } parent
                && !ReferenceEquals(parent, m_root.transform)) owner = parent.gameObject;
            string id = "vn-" + owner.identity.persistentId.ToString("N");
            owner.TryGetComponent<NovelUiButtonComponent>(out var button); owner.TryGetComponent<NovelUiToggleComponent>(out var toggle);
            if (button is null && toggle is null || !(button?.interactable ?? toggle?.interactable ?? false)
                || !m_projection.disabled.TryGetValue(id, out bool disabled) || disabled || !activated.Add(owner.identity.persistentId)) continue;
            owner.TryGetComponent<NovelUiBindingComponent>(out var binding);
            var row = NovelUiRowComponent.Find(owner);
            if (!NovelUiActionReader.Condition(source, binding?.visible ?? string.Empty, row)
                || !NovelUiActionReader.Condition(source, binding?.interactable ?? string.Empty, row)) continue;
            var action = NovelUiActionReader.Read(owner, source);
            if (action is not null && source is not null && !source.CanInvoke(action)) continue;
            if (action is { binding.Length: > 0 } && source is null)
                throw new InvalidOperationException("A bound UI action requires an active data source.");
            if (!NovelEngineUiToggle.Activate(owner, source)) continue;
            if (action is not null) actions.Add(action);
        }
        m_feedback.Input(input, m_hover);
        ApplyFeedback();
        return actions;
    }

    /// <summary>Asks the engine whether this visible surface consumes a canvas-local pointer position.</summary>
    /// <param name="position">The pointer in engine UI pixels.</param>
    /// <returns>Whether a visible engine element receives pointer input.</returns>
    public bool HasElementAtPoint(Vector2 position)
    {
        ObjectDisposedException.ThrowIf(m_disposed, this);
        return m_visible && !m_root.isDestroyed && m_root.gameObject.activeInHierarchy && m_ui.HasElementAtPoint(m_context, position);
    }

    /// <summary>Resolves an editor selection through engine pointer events without activating controls or advancing story.</summary>
    /// <param name="position">The pointer in canvas-local pixels.</param>
    /// <param name="source">Optional already-running session data; selection never advances it.</param>
    /// <returns>The actual native object hit by the engine, or null outside the document.</returns>
    public GameObject? Pick(
        Vector2 position,
        UiDataSource? source = null
    ) {
        if (Synchronize(source)) RealizeLayout();
        if (!m_visible) return null;
        m_lastPointer = position;
        m_ui.Update(m_context, new EngineInput(position, Vector2.ZERO, 0, [], [], [], [], []));
        foreach (var message in m_ui.DrainEvents(m_context)) ObserveHover(message);
        ApplyFeedback();
        return m_hover is not null && IsCurrent(m_hover) && m_ui.HasElementAtPoint(m_context, position) ? m_hover : null;
    }

    private void ObserveHover(UiEvent message)
    {
        if (message.document != m_document || m_projection is null || !m_projection.targets.TryGetValue(message.targetId, out var owner)) return;
        m_feedback.Observe(message, owner);
        m_focus.Observe(message, owner);
        if (message.type == UiEventType.MouseEnter) m_hover = owner;
        else if (message.type == UiEventType.MouseLeave && ReferenceEquals(m_hover, owner)) m_hover = null;
    }

    private bool Synchronize(UiDataSource? source)
    {
        ObjectDisposedException.ThrowIf(m_disposed, this);
        if (m_root.isDestroyed || !m_root.gameObject.activeInHierarchy)
        {
            if (m_visible) m_ui.HideDocument(m_context, m_document);
            m_visible = false; m_projection = null; m_hover = null;
            m_feedback.Clear(); m_appliedStyles.Clear();
            m_ui.DrainEvents(m_context);
            return false;
        }
        int width = Dimension(m_root.width), height = Dimension(m_root.height);
        var candidate = NovelEngineUiProjection.Capture(m_root, source, m_focus);
        string slot = m_root.contentElement;
        if (string.IsNullOrWhiteSpace(slot)) throw new InvalidOperationException("The UI template needs a content element ID.");
        var template = m_root.documentTemplate;
        if (template is not null && (template.isMissing || template.source?.language.value != "inno.ui-language.rml"))
            throw new InvalidOperationException("The scene UI projection requires an imported RML template.");
        if ((candidate.text.Count > 0 || candidate.richText.Count > 0) && (template is null || template.fonts.Count == 0))
            throw new InvalidOperationException("Text controls require an engine UI template with imported font faces.");
        bool replace = !m_document.isValid || !ReferenceEquals(template, m_template)
            || (template?.contentVersion ?? 0) != m_templateVersion || slot != m_slot;
        bool changed = replace || !m_visible || width != m_width || height != m_height;
        if (replace)
        {
            var document = template is not null ? m_ui.LoadDocument(m_context, template)
                : m_ui.LoadDocument(m_context, new UiDocumentSource(new("inno.ui-language.rml"),
                    "<rml><head><style>body { margin:0; width:100%; height:100%; } #vn-scene { position:absolute; width:100%; height:100%; }</style></head><body><div id=\"vn-scene\"></div></body></rml>"));
            try
            {
                Apply(document, candidate, null, slot);
                m_ui.ShowDocument(m_context, document);
            }
            catch { m_ui.CloseDocument(m_context, document); throw; }
            if (m_document.isValid) m_ui.CloseDocument(m_context, m_document);
            m_document = document; m_template = template; m_templateVersion = template?.contentVersion ?? 0; m_slot = slot;
            m_feedback.Clear();
        }
        else
        {
            bool replacedContent = candidate.NeedsContent(m_projection);
            changed |= Apply(m_document, candidate, m_projection, slot);
            // The public document lifecycle applies RML autofocus. A value/style-only update must not steal keyboard focus.
            if (replacedContent && m_visible && candidate.hasInitialFocus) m_ui.ShowDocument(m_context, m_document);
        }
        if (width != m_width || height != m_height)
        {
            m_ui.SetViewport(m_context, width, height); m_width = width; m_height = height;
        }
        if (!m_visible && !replace) m_ui.ShowDocument(m_context, m_document);
        m_projection = candidate; m_visible = true;
        return changed;
    }

    private bool Apply(
        UiDocumentHandle document,
        NovelEngineUiProjection candidate,
        NovelEngineUiProjection? previous,
        string slot
    ) {
        bool changed = candidate.NeedsContent(previous);
        if (changed && !m_ui.SetContent(m_context, document, slot, new UiDocumentFragment(new("inno.ui-language.rml"), candidate.markup)))
            throw new InvalidOperationException("The engine UI template is missing its scene content element: " + slot);
        if (changed) { m_feedback.Clear(); m_appliedStyles.Clear(); }
        bool dirty = changed;
        foreach (var pair in candidate.styles)
            if (changed || !previous!.styles.TryGetValue(pair.Key, out var old) || old != pair.Value)
            {
                if (!m_ui.SetAttribute(m_context, document, pair.Key, "style", pair.Value))
                    throw new InvalidOperationException("Engine UI lost a projected scene element: " + pair.Key);
                m_appliedStyles[pair.Key] = pair.Value;
                dirty = true;
            }
        foreach (var pair in candidate.text)
            if (changed || !previous!.text.TryGetValue(pair.Key, out var old) || old != pair.Value)
            {
                if (!m_ui.SetText(m_context, document, pair.Key, pair.Value))
                    throw new InvalidOperationException("Engine UI lost a projected text element: " + pair.Key);
                dirty = true;
            }
        foreach (var pair in candidate.richText)
            if (changed || !previous!.richText.TryGetValue(pair.Key, out var old) || old != pair.Value)
            {
                if (!m_ui.SetContent(m_context, document, pair.Key, new UiDocumentFragment(new("inno.ui-language.rml"), pair.Value)))
                    throw new InvalidOperationException("Engine UI lost a projected rich-text element: " + pair.Key);
                dirty = true;
            }
        return dirty;
    }

    private void RealizeLayout()
    {
        // The public engine service realizes pending layout through Update. Never feed host input or dispatch its synthetic focus events here.
        m_ui.Update(m_context, new EngineInput(m_lastPointer, Vector2.ZERO, 0, [], [], [], [], []));
        foreach (var message in m_ui.DrainEvents(m_context)) ObserveHover(message);
    }

    private void ApplyFeedback()
    {
        if (!m_visible || m_projection is null) return;
        bool changed = false;
        foreach (var pair in m_projection.styles)
        {
            string value = m_feedback.Style(m_projection.targets[pair.Key], pair.Value, m_projection.disabled[pair.Key]);
            if (m_appliedStyles.TryGetValue(pair.Key, out var old) && old == value) continue;
            if (!m_ui.SetAttribute(m_context, m_document, pair.Key, "style", value))
                throw new InvalidOperationException("Engine UI lost a control's presentation: " + pair.Key);
            m_appliedStyles[pair.Key] = value;
            changed = true;
        }
        if (changed) RealizeLayout();
    }

    private bool IsCurrent(GameObject owner)
    {
        if (owner.isDestroyed || !owner.activeInHierarchy || m_root.isDestroyed) return false;
        for (var parent = owner.transform.parent; parent is not null; parent = parent.parent)
        {
            if (ReferenceEquals(parent, m_root.transform)) return true;
            if (parent.gameObject.TryGetComponent<NovelUiRootComponent>(out _)) return false;
        }
        return false;
    }
    private static int Dimension(float value) => float.IsFinite(value) && value >= 1 && value <= 32768 ? (int)MathF.Ceiling(value)
        : throw new InvalidOperationException("UI canvas dimensions must be within 1 and 32768 pixels.");

    /// <summary>Releases the engine context, its documents and associated font ownership exactly once.</summary>
    public void Dispose()
    {
        if (m_disposed) return;
        m_ui.DestroyContext(m_context);
        m_feedback.Clear(); m_focus.Clear(); m_appliedStyles.Clear();
        m_projection = null; m_template = null; m_disposed = true; m_visible = false;
    }
}
