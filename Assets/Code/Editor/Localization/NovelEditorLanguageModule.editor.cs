using System;
using InnoEditor.Core;
using InnoEditor.Settings;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Model.Localization;

namespace Inno.VisualNovel.Editor.Localization;

/// <summary>Owns an editor translation view for one native project generation without static culture or cross-project state.</summary>
[EditorModule("inno.vn.editor-language", order: 423)]
public sealed class NovelEditorLanguageModule : EditorModule
{
    private EditorTextCatalog m_catalog = new(null);
    private readonly EditorSettings? m_settings;
    private IDisposable? m_subscription;
    /// <summary>Creates a generation-owned language observer; a null owner permits isolated catalog-only hosts.</summary>
    /// <param name="settings">The native project Settings owner supplied by engine constructor injection.</param>
    public NovelEditorLanguageModule(EditorSettings? settings = null) => m_settings = settings;
    /// <summary>Gets the latest rejected native preference or resource error without replacing the previous valid view.</summary>
    public string lastError { get; private set; } = string.Empty;
    /// <summary>Gets the current source or translated view; this owner does not advertise complete English resource coverage.</summary>
    public EditorTextCatalog catalog => m_catalog;
    /// <summary>Installs a fully validated native translation candidate without retaining mutable source rows.</summary>
    /// <param name="table">The native language resource supplied by the current host.</param>
    /// <param name="language">A language declared by that resource.</param>
    public void Use(
        LocalizationTableAsset table,
        string language
    ) { ArgumentNullException.ThrowIfNull(table); var candidate = new EditorTextCatalog(table, language); m_catalog = candidate; lastError = string.Empty; }
    /// <summary>Returns to the built-in source language without changing any game preference or document.</summary>
    public void Reset() { m_catalog = new(null); lastError = string.Empty; }
    /// <inheritdoc />
    protected override void OnStart(EditorContext context)
    {
        if (m_settings is null) throw new InvalidOperationException("The VN editor language owner requires native project Settings.");
        m_subscription = m_settings.ListenChanges(ApplySettings);
        ApplySettings(m_settings);
    }
    /// <inheritdoc />
    protected override void OnStop(EditorContext context) { m_subscription?.Dispose(); m_subscription = null; Reset(); }
    private void ApplySettings(EditorSettings settings)
    {
        try
        {
            string language = settings.Get(NovelEditorLanguageSetting.settingPath).GetAsString("language", "zh-CN") ?? "zh-CN";
            if (language is not ("zh-CN" or "en")) throw new InvalidOperationException("Unsupported VN editor language: " + language);
            if (language == "zh-CN") Reset();
            else if (m_catalog.language != language) Use(BuiltInNovelAssets.Load<LocalizationTableAsset>("BuiltIn/Editor/EditorText.ivnlocale"), language);
            lastError = string.Empty;
        }
        catch (Exception error) when (error is not OutOfMemoryException) { lastError = error.Message; }
    }
}
