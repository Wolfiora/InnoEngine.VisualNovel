using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Assets;
using InnoEngine.Rendering;
using InnoEditor.Assets;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Localization;
using Inno.VisualNovel.Player.Localization;

namespace Inno.VisualNovel.Authoring.Publishing;

/// <summary>Audits explicit native content roots without inventing an asset database or game packager.</summary>
public static class NovelPublishPreflight
{
    /// <summary>Validates game content and walks dependencies collected by the engine's serializer.</summary>
    /// <param name="project">The current detached draft or canonical imported definition.</param>
    /// <returns>An immutable audit; this operation never builds or launches a game.</returns>
    public static NovelPublishReport Check(NovelProjectAsset project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var issues = new List<PublishIssue>();
        bool imported = project.assetPath.isValid && project.assetPath.localPath.Length > 0 && project.identity.persistentId != Guid.Empty;
        string root = imported ? project.assetPath.ToString() : "Game definition draft";
        try { NovelProjectValidator.Validate(project); }
        catch (Exception error) when (error is not OutOfMemoryException) { Error("VN-PUBLISH-CONTENT", root, error.Message); }
        foreach (string role in new[] { "loading", "title", "reading", "pause", "settings", "history", "credits", "save", "load", "confirm", "extras", "gallery", "music", "replays", "endings", "glossary", "characters", "collectionDetail" })
            if (!project.screens.Any(item => item.role == role && item.screen is { isMissing: false }))
                Error("VN-PUBLISH-SCREEN", role, "A required game shell page is missing. Assign an editable page in the VN game settings.");
        CheckMaterial(project.material, "inno.vn.surface", "Surface material");
        if (project.transitions.Length + project.screenEffects.Length > 0) CheckMaterial(project.effectsMaterial, "inno.vn.post", "Image composition material");
        if (project.localization is { } texts)
            foreach (var row in texts.entries)
                foreach (var language in texts.languages.Where(item => item.id != texts.sourceLanguage))
                {
                    var translation = row.translations.FirstOrDefault(item => item.language == language.id);
                    if (translation is null || translation.status == TranslationStatus.Draft
                        || translation.sourceFingerprint != NovelLocalizationService.Fingerprint(row.source))
                        issues.Add(new(PublishIssueLevel.Warning, "VN-PUBLISH-TRANSLATION", row.id + "/" + language.id, "The translation is missing, unreviewed or outdated. The game will use its localization fallback."));
                }
        issues.Add(new(PublishIssueLevel.Pending, "VN-PUBLISH-PERSISTENCE", root,
            "The selected Runtime API does not expose native state encoding. Standalone Player persistence still requires host integration through ConfigurePersistence; no-code publishing is not yet verified."));
        foreach (var command in project.commandDefinitions)
            issues.Add(new(PublishIssueLevel.Pending, "VN-PUBLISH-COMMAND", command.id,
                "Register matching C# commands and restore factories in the target host. Metadata is not a runtime implementation; the selected Runtime API does not provide automatic discovery."));
        foreach (var action in project.uiActions)
            issues.Add(new(PublishIssueLevel.Pending, "VN-PUBLISH-UI-ACTION", action.id,
                "Call ConfigureUiActions on the scene system before session creation. Action catalogs describe forms; matching runtime implementations require explicit registration."));
        foreach (var source in project.uiDataSources)
            issues.Add(new(PublishIssueLevel.Pending, "VN-PUBLISH-UI-DATA", source.id,
                "Register current-generation implementations with ConfigureUiDataSources. Include dynamic images in the additional resource closure. Private data source fields are not saved."));
        issues.Add(new(PublishIssueLevel.Pending, "VN-PUBLISH-STARTUP", root,
            "Select the generated startup scene and Windows x64 in Export as Game. Publishing uses the native engine build settings and pipeline."));

        var rows = new Dictionary<Guid, (AssetPath path, int bytes, HashSet<string> parents)>();
        var pending = new Queue<(AssetDependency dependency, string parent)>();
        var expanded = new HashSet<Guid>();
        if (imported) expanded.Add(project.identity.persistentId);
        try
        {
            var source = EditorAssets.CaptureProperties(project);
            if (imported) rows.Add(project.identity.persistentId, (project.assetPath, source.data.Length, new(StringComparer.Ordinal)));
            foreach (AssetDependency dependency in source.dependencies) pending.Enqueue((dependency, root));
        }
        catch (Exception error) when (error is not OutOfMemoryException) { Error("VN-PUBLISH-REFERENCES", root, error.Message); }
        while (pending.TryDequeue(out var next))
        {
            AssetDependency dependency = next.dependency;
            if (rows.TryGetValue(dependency.persistentId, out var existing)) { existing.parents.Add(next.parent); continue; }
            if (!expanded.Add(dependency.persistentId)) continue;
            if (expanded.Count > 100000) { Error("VN-PUBLISH-BUDGET", root, "The resource closure exceeds 100,000 items. Split the game or inspect extension references."); break; }
            try
            {
                AssetObject asset = Assets.Load<AssetObject>(dependency.persistentId);
                if (asset.isMissing || asset.identity.persistentId != dependency.persistentId)
                { Error("VN-PUBLISH-MISSING", next.parent, "Missing resource: " + dependency.lastKnownPath + " [" + dependency.persistentId + "]"); continue; }
                AssetPath path = asset.assetPath;
                if (path.localPath.Split('/').Any(static segment => segment.StartsWith('~')))
                    Error("VN-PUBLISH-SAMPLE", path.ToString(), "Player excludes ~ subtrees. Instantiate the sample under Assets/Game and rebuild references. Source: " + next.parent);
                if (AssetAuditing.ResourceInventoryRules.IsAuthoring(path.localPath))
                    Error("VN-PUBLISH-EDITOR-ONLY", path.ToString(), "Authoring catalogs are Editor-only. Include the actual runtime assets in additional resources; attach command or UI declarations through game settings.");
                var snapshot = EditorAssets.CaptureProperties(asset);
                rows.Add(dependency.persistentId, (path, snapshot.data.Length, new(StringComparer.Ordinal) { next.parent }));
                foreach (AssetDependency child in snapshot.dependencies) pending.Enqueue((child, path.ToString()));
            }
            catch (Exception error) when (error is not OutOfMemoryException) { Error("VN-PUBLISH-DEPENDENCY", dependency.lastKnownPath, error.Message); }
        }
        return new(project.title, project.buildFingerprint, rows.OrderBy(static item => item.Value.path.ToString(), StringComparer.Ordinal)
            .Select(static item => new PublishAsset(item.Key, item.Value.path, item.Value.bytes, item.Value.parents.Order(StringComparer.Ordinal).ToArray())), issues);

        void Error(
            string code,
            string location,
            string message
        ) => issues.Add(new(PublishIssueLevel.Error, code, location, message));
        void CheckMaterial(
            MaterialAsset? material,
            string contract,
            string location
        ) {
            if (material?.shader?.definition?.techniques.Any(item => item.contract == new ShaderContractId(contract)) != true)
                Error("VN-PUBLISH-SHADER", location, "The native shader must declare the " + contract + " contract. Target compilation is performed by the engine export pipeline.");
        }
    }
}
