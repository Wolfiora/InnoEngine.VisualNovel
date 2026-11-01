using System;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.UI.Binding;

namespace Inno.VisualNovel.Player.Interface.Data;

/// <summary>Provides current native game values to derived UI sources with writes restricted to control submission.</summary>
public sealed class NovelUiDataSourceContext
{
    private readonly NovelSession m_session;
    private readonly UiDataSource m_native;
    internal bool writing { get; set; }
    internal NovelUiDataSourceContext(
        NovelSession session,
        UiDataSource native
    ) { m_session = session; m_native = native; }
    /// <summary>Gets whether preview side-effect policy is active.</summary>
    public bool preview => m_session.isPreview;
    /// <summary>Gets the current session replacement revision for invalidating derived caches.</summary>
    public long stateRevision => m_session.stateRevision;
    /// <summary>Gets the currently selected game language.</summary>
    public string language => m_session.localization.language;
    /// <summary>Reads a detached ordinary page, variable, setting or system value; recursive source queries are prohibited.</summary>
    /// <param name="path">An existing native binding path.</param>
    /// <returns>The current detached value.</returns>
    public NovelValue Get(string path)
    {
        Guard(path);
        return m_native.TryRead(path, out var value) ? value.Copy() : throw new InvalidOperationException("Source context binding is unavailable: " + path);
    }
    /// <summary>Writes existing typed native state only while the owner is submitting an editable source field.</summary>
    /// <param name="path">The ordinary writable native binding.</param>
    /// <param name="value">A complete compatible value.</param>
    public void Set(
        string path,
        NovelValue value
    ) {
        Guard(path);
        if (!writing) throw new InvalidOperationException("UI data source queries cannot modify native state.");
        if (!m_native.TryWrite(path, value.Copy())) throw new InvalidOperationException("Source context binding rejected the value: " + path);
    }
    private static void Guard(string path)
    {
        if (path.StartsWith("custom.", StringComparison.Ordinal) || path.StartsWith("item.", StringComparison.Ordinal))
            throw new InvalidOperationException("A source context cannot recursively query custom sources or unowned row fields.");
    }
}
