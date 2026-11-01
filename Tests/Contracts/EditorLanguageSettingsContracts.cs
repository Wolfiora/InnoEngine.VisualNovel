using System;
using System.Collections.Generic;
using System.IO;
using Inno.Adapter.Modules.DotNet;
using Inno.Adapter.Serialization.DotNet;
using Inno.Assets;
using Inno.Assets.Pipeline;
using Inno.Core.Diagnostics;
using Inno.Core.Identity;
using Inno.Core.Input;
using Inno.Core.Logging;
using Inno.Core.Serialization;
using Inno.Editor.Core;
using Inno.Editor.Interactions;
using Inno.Editor.Settings;
using Inno.Extensibility.Modules;
using Inno.Extensibility.Types;
using Inno.VisualNovel.Authoring.AssetImporting;
using Inno.VisualNovel.Authoring.LocalizationEditing;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Editor.Localization;
using Inno.VisualNovel.Model.Localization;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class EditorLanguageSettingsContracts
{
    internal static void NativePreferenceHistoryAndRestart(string repository)
    {
        string root = Path.Combine(repository, "artifacts", "AcceptanceProjects", "EditorLanguage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Assets"));
        var identities = new IdentityAllocator(); using var identityScope = identities.EnterScope();
        using var modules = new ModuleHost(new ModuleHostOptions { catalogSource = new DotNetAssemblyCatalogSource(typeof(EditorSettings).Assembly) });
        _ = modules.Register("visualnovel.native-settings-contracts", [typeof(EditorSettings).Assembly]);
        _ = modules.Register("visualnovel.language-model", [typeof(LocalizationTableAsset).Assembly,
            typeof(Inno.VisualNovel.Player.Sessions.NovelSession).Assembly, typeof(Inno.VisualNovel.Narrative.Execution.StoryMachine).Assembly,
            typeof(Inno.VisualNovel.Stage.Timeline.PerformancePlayer).Assembly, typeof(Inno.VisualNovel.UI.Documents.UiDocumentInstance).Assembly]);
        _ = modules.Register("visualnovel.language-authoring", [typeof(LocalizationImporter).Assembly, typeof(StoryCompiler).Assembly]);
        _ = modules.Register("visualnovel.language-editor", [typeof(NovelEditorLanguageModule).Assembly]);
        using var types = new TypeCatalog(modules, new ReflectionTypeCatalogSource());
        using var serialization = new SerializationRegistry(types, new ReflectionSerializationMetadataSource());
        using var logs = new LogRouter();
        using var assets = new AssetPipeline(modules, types, serialization, identities, new DiagnosticHub(), logs,
            AssetPipelineOptions.Create(Path.Combine(root, "Assets"), Path.Combine(root, "Library")) with { enableFileSystemWatcher = false });
        using var assetScope = AssetExecutionContext.EnterScope(assets);
        var table = new LocalizationTableAsset { title = "Editor language test", languages = [new(), new() { id = "en", name = "English" }],
            entries = [new() { id = EditorTextCatalog.Key("标题"), source = "标题" }] };
        LocalizationEditService.SetTranslation(table, table.entries[0].id, "en", "Title", TranslationStatus.Reviewed);
        Check(EditorAssets.Save(AssetPath.Project("BuiltIn/Editor/EditorText.ivnlocale"), table), "The native editor translation source did not import.");
        var definition = new NovelEditorLanguageSetting(); var isolated = definition.defaultValue; isolated.SetAsString("language", "en");
        Check(definition.defaultValue.GetAsString("language") == "zh-CN", "The native field shared mutable default objects.");
        using (var runtime = Runtime())
        {
            runtime.Start();
            Check(runtime.interactions.TryGetModule<EditorSettings>(out var settings) && settings is not null
                && runtime.interactions.TryGetModule<NovelEditorLanguageModule>(out _), "Native Settings or the project-owned language module did not activate.");
            _ = runtime.interactions.TryGetModule<NovelEditorLanguageModule>(out var language);
            Check(language!.catalog.language == "zh-CN", "An empty project did not use Simplified Chinese.");
            Apply(settings!, "en"); Check(language.catalog.Text("标题") == "Title" && language.lastError.Length == 0, "Native Apply did not publish the English resource candidate.");
            Check(runtime.interactions.history.Undo().succeeded && language.catalog.language == "zh-CN", "Native Undo did not restore source language.");
            Check(runtime.interactions.history.Redo().succeeded && language.catalog.Text("标题") == "Title", "Native Redo lost the English catalog.");
            Apply(settings!, "unsupported"); Check(language.catalog.language == "en" && language.lastError.Length > 0, "A rejected language replaced the last valid view without a diagnostic.");
            Check(runtime.interactions.history.Undo().succeeded && language.catalog.language == "en" && language.lastError.Length == 0,
                "Undo did not recover from a rejected preference.");
        }
        using (var reopened = Runtime())
        {
            reopened.Start(); _ = reopened.interactions.TryGetModule<NovelEditorLanguageModule>(out var language);
            Check(language?.catalog.Text("标题") == "Title" && language.lastError.Length == 0, "The native project preference did not survive owner retirement and restart.");
        }
        EditorInteractionRuntime Runtime() => new(new EditorContext(root, new EditorKeyboardPolicy(KeyModifier.Control, "Super")), types, logs, [serialization, types, identities, assets]);
    }
    private static void Apply(
        EditorSettings settings,
        string language
    ) {
        var value = settings.Get(NovelEditorLanguageSetting.settingPath); value.SetAsString("language", language);
        Check(settings.Apply(new Dictionary<string, EditorSettingObject> { [NovelEditorLanguageSetting.settingPath] = value }), "Native preference Apply did not record a change.");
    }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}
