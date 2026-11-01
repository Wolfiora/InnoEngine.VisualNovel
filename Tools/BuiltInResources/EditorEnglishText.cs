using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Inno.VisualNovel.Player.Localization;
using Inno.VisualNovel.Authoring.LocalizationEditing;
using Inno.VisualNovel.Editor.Localization;
using Inno.VisualNovel.Model.Localization;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal static class EditorEnglishText
{
    internal static void Review(string root)
        => _ = Build(root, false);

    internal static LocalizationTableAsset Create(string root) => Build(root, true);

    private static LocalizationTableAsset Build(
        string root,
        bool requireComplete
    )
    {
        var entries = EditorTextInventory.Write(root);
        var translated = new Dictionary<(string kind, string source), string>();
        foreach (var item in EditorEnglishAudio.entries.Concat(EditorEnglishCharacters.entries).Concat(EditorEnglishCharacterBatch.entries)
            .Concat(EditorEnglishCollections.entries).Concat(EditorEnglishDebugging.entries).Concat(EditorEnglishPreview.entries).Concat(EditorEnglishEffects.entries)
            .Concat(EditorEnglishInstructionParameters.entries).Concat(EditorEnglishStoryEditing.entries).Concat(EditorEnglishParallelPerformance.entries)
            .Concat(EditorEnglishReadingStage.entries).Concat(EditorEnglishEditingReviews.entries).Concat(EditorEnglishFormatting.entries)
            .Concat(EditorEnglishLocalization.entries).Concat(EditorEnglishProject.entries).Concat(EditorEnglishPublishing.entries)
            .Concat(EditorEnglishNodeMetadata.entries).Concat(EditorEnglishBuiltIns.entries).Concat(EditorEnglishResourceAudit.entries)
            .Concat(EditorEnglishLicenses.entries).Concat(EditorEnglishScreenDesigner.entries).Concat(EditorEnglishUiFields.entries)
            .Concat(EditorEnglishUiState.entries).Concat(EditorEnglishUiExtensions.entries).Concat(EditorEnglishUiAnimation.entries)
            .Concat(EditorEnglishHierarchy.entries).Concat(EditorEnglishScriptView.entries).Concat(EditorEnglishStageDesigner.entries)
            .Concat(EditorEnglishThemeDesigner.entries).Concat(EditorEnglishTimeline.entries).Concat(EditorEnglishVariables.entries)
            .Concat(EditorEnglishNavigation.entries).Concat(EditorEnglishStoryImport.entries).Concat(EditorEnglishSearch.entries)
            .Concat(EditorEnglishPresetMetadata.entries).Concat(EditorEnglishBindingValues.entries).Concat(EditorEnglishBindingActions.entries)
            .Concat(EditorEnglishStageBatches.entries).Concat(EditorEnglishAuthoringDiagnostics.entries).Concat(EditorEnglishCompilerDiagnostics.entries).Concat(EditorEnglishResourceLabels.entries))
        {
            if (string.IsNullOrWhiteSpace(item.english) || !translated.TryAdd((item.kind, item.source), item.english)) throw new InvalidOperationException("Empty or duplicate editor translation: " + item.source);
            NovelLocalizationService.RequireMatchingPlaceholders(item.kind == "format" ? item.source : Inno.VisualNovel.Editor.Localization.EditorTextCatalog.EncodeLiteral(item.source),
                item.kind == "format" ? item.english : Inno.VisualNovel.Editor.Localization.EditorTextCatalog.EncodeLiteral(item.english));
            int sourceId = item.source.IndexOf("##", StringComparison.Ordinal), translatedId = item.english.IndexOf("##", StringComparison.Ordinal);
            if ((sourceId < 0) != (translatedId < 0) || sourceId >= 0 && item.source[sourceId..] != item.english[translatedId..])
                throw new InvalidOperationException("Editor translation changed an ImGui control identity: " + item.source);
        }
        var missing = entries.Where(item => !translated.ContainsKey((item.kind, item.source))).ToArray();
        var orphan = translated.Keys.Where(key => !entries.Any(item => item.kind == key.kind && item.source == key.source)).ToArray();
        string report = Path.Combine(root, "artifacts", "Reports", "EditorEnglishCoverage.txt");
        File.WriteAllText(report, "English editor resources: " + (entries.Count - missing.Length) + "/" + entries.Count + " current entries translated; " + missing.Length + " missing, " + orphan.Length + " orphan.\n"
            + "Not complete: no English editor mode or native language asset is published by this review.\n" + string.Join("\n", missing.Select(static item => item.id + "\t" + item.source.Replace("\n", "\\n", StringComparison.Ordinal)))
            + (orphan.Length == 0 ? "" : "\nORPHAN\n" + string.Join("\n", orphan.Select(static item => item.kind + "\t" + item.source))), new UTF8Encoding(false));
        Console.WriteLine("English editor resource review: " + (entries.Count - missing.Length) + "/" + entries.Count + "; missing=" + missing.Length + "; orphan=" + orphan.Length + ". English editor mode remains unfinished.");
        Console.WriteLine(report);
        if (requireComplete && (missing.Length > 0 || orphan.Length > 0)) throw new InvalidOperationException("Resolve the editor translation inventory before publishing native resources: " + report);
        var table = new LocalizationTableAsset { title = "Visual Novel Editor", sourceLanguage = "zh-CN",
            languages = [new() { id = "zh-CN", name = "简体中文" }, new() { id = "en", name = "English" }],
            entries = entries.Select(static item => new LocalizedText { id = item.id, source = item.kind == "format" ? item.source : EditorTextCatalog.EncodeLiteral(item.source) }).ToArray() };
        foreach (var item in entries)
            if (translated.TryGetValue((item.kind, item.source), out string? english)) LocalizationEditService.SetTranslation(table, item.id, "en",
                item.kind == "format" ? english : EditorTextCatalog.EncodeLiteral(english), TranslationStatus.Reviewed);
        _ = new EditorTextCatalog(table, "en");
        return table;
    }
}

internal sealed record EditorEnglishEntry(string kind, string source, string english);
