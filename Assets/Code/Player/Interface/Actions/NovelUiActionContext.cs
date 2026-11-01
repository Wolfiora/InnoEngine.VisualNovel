using System;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.UI.Binding;

namespace Inno.VisualNovel.Player.Interface.Actions;

/// <summary>Provides typed binding access and explicit page navigation without exposing UI internals or introducing another state store.</summary>
public sealed class NovelUiActionContext
{
    private readonly UiDataSource m_source;
    private readonly Action<string> m_open;
    private readonly bool m_readOnly;
    internal NovelUiActionContext(
        UiDataSource source,
        Action<string> open,
        bool preview,
        bool readOnly
    ) { m_source = source; m_open = open; this.preview = preview; m_readOnly = readOnly; }
    /// <summary>Gets whether this action runs in an isolated authoring preview.</summary>
    public bool preview { get; }
    /// <summary>Reads a defensive value from the current page, story variables, settings or system bindings.</summary>
    /// <param name="path">The existing typed binding path.</param>
    /// <returns>The detached current value.</returns>
    public NovelValue Get(string path) => m_source.TryRead(path, out var value) ? value.Copy() : throw new InvalidOperationException("UI action binding is unavailable: " + path);
    /// <summary>Writes through the existing typed domain binding, retaining its own persistence and rollback lifetime.</summary>
    /// <param name="path">The writable binding identity.</param>
    /// <param name="value">A complete compatible value.</param>
    public void Set(
        string path,
        NovelValue value
    ) {
        if (m_readOnly) throw new InvalidOperationException("UI action availability/selection queries cannot modify game state.");
        if (!m_source.TryWrite(path, value.Copy())) throw new InvalidOperationException("UI action binding rejected the value: " + path);
    }
    /// <summary>Opens an authored page through the same modal and input owner used by built-in actions.</summary>
    /// <param name="role">The declared shell role.</param>
    public void OpenPage(string role)
    {
        if (m_readOnly) throw new InvalidOperationException("UI action queries cannot navigate pages.");
        m_open(role);
    }
}
