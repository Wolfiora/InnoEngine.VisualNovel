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
        string root = imported ? project.assetPath.ToString() : "游戏定义草稿";
        try { NovelProjectValidator.Validate(project); }
        catch (Exception error) when (error is not OutOfMemoryException) { Error("VN-PUBLISH-CONTENT", root, error.Message); }
        foreach (string role in new[] { "loading", "title", "reading", "pause", "settings", "history", "credits", "save", "load", "confirm", "extras", "gallery", "music", "replays", "endings", "glossary", "characters", "collectionDetail" })
            if (!project.screens.Any(item => item.role == role && item.screen is { isMissing: false }))
                Error("VN-PUBLISH-SCREEN", role, "缺少完整游戏壳页面；在 VN 游戏设置中关联可编辑页面。");
        CheckMaterial(project.material, "inno.vn.surface", "基础图像材质");
        if (project.transitions.Length + project.screenEffects.Length > 0) CheckMaterial(project.effectsMaterial, "inno.vn.post", "图像合成材质");
        if (project.localization is { } texts)
            foreach (var row in texts.entries)
                foreach (var language in texts.languages.Where(item => item.id != texts.sourceLanguage))
                {
                    var translation = row.translations.FirstOrDefault(item => item.language == language.id);
                    if (translation is null || translation.status == TranslationStatus.Draft
                        || translation.sourceFingerprint != NovelLocalizationService.Fingerprint(row.source))
                        issues.Add(new(PublishIssueLevel.Warning, "VN-PUBLISH-TRANSLATION", row.id + "/" + language.id, "译文缺失、待审核或原文已更新；游戏将按本地化回退规则显示。"));
                }
        issues.Add(new(PublishIssueLevel.Pending, "VN-PUBLISH-PERSISTENCE", root,
            "当前正式 Runtime API 未导出原生状态编解码入口；独立 Player 的自动存档接入尚未完成。宿主显式 ConfigurePersistence 的逻辑路径已实现，但不能据此认定无代码发布完成。"));
        foreach (var command in project.commandDefinitions)
            issues.Add(new(PublishIssueLevel.Pending, "VN-PUBLISH-COMMAND", command.id,
                "确认目标宿主已显式注册匹配的 C# 命令及恢复工厂；元数据表不是运行实现。当前 Runtime API 不提供自动类型发现入口。"));
        foreach (var action in project.uiActions)
            issues.Add(new(PublishIssueLevel.Pending, "VN-PUBLISH-UI-ACTION", action.id,
                "确认启动组件已在会话创建前 ConfigureUiActions 注册匹配实现；原生动作目录只包含表单契约，当前 Runtime API 无自动类型发现。"));
        foreach (var source in project.uiDataSources)
            issues.Add(new(PublishIssueLevel.Pending, "VN-PUBLISH-UI-DATA", source.id,
                "确认启动组件已 ConfigureUiDataSources 注册匹配的当前代际实现；动态图片须加入原生附加资源闭包。数据源私有字段不属于存档。"));
        issues.Add(new(PublishIssueLevel.Pending, "VN-PUBLISH-STARTUP", root,
            "在引擎 Export as Game 中选择向导生成的启动场景和 Windows x64；本插件未绕过引擎构建设置或另建打包器。"));

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
            if (expanded.Count > 100000) { Error("VN-PUBLISH-BUDGET", root, "资源闭包超过 100000 项，请拆分游戏或检查扩展引用。"); break; }
            try
            {
                AssetObject asset = Assets.Load<AssetObject>(dependency.persistentId);
                if (asset.isMissing || asset.identity.persistentId != dependency.persistentId)
                { Error("VN-PUBLISH-MISSING", next.parent, "缺失资源：" + dependency.lastKnownPath + " [" + dependency.persistentId + "]"); continue; }
                AssetPath path = asset.assetPath;
                if (path.localPath.Split('/').Any(static segment => segment.StartsWith('~')))
                    Error("VN-PUBLISH-SAMPLE", path.ToString(), "Player 不打包 ~ 子树；将样例实例化到 Assets/Game 并重建引用。来源：" + next.parent);
                if (AssetAuditing.ResourceInventoryRules.IsAuthoring(path.localPath))
                    Error("VN-PUBLISH-EDITOR-ONLY", path.ToString(), "创作目录使用 Editor 范围。请将运行时需要的资源本身加入附加打包集合；命令或 UI 声明通过游戏设置关联。");
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
                Error("VN-PUBLISH-SHADER", location, "需要声明 " + contract + " 契约的原生 Shader；目标编译仍由引擎导出流程执行。");
        }
    }
}
