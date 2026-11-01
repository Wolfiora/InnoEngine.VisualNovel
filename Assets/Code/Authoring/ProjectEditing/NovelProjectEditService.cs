using System;
using System.Linq;
using InnoEngine.Assets;
using InnoEngine.Audio;
using InnoEngine.Rendering;
using InnoEngine.Text;
using Inno.VisualNovel.Model.Audio;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Interface.Themes;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Localization;
using Inno.VisualNovel.Model.Collections;

namespace Inno.VisualNovel.Authoring.ProjectEditing;

/// <summary>Maintains explicit game resource roots without discovering assets by filesystem scanning.</summary>
public static class NovelProjectEditService
{
    /// <summary>Packages a dynamically selected resource through an ordinary native asset reference.</summary>
    /// <param name="project">The detached game document.</param>
    /// <param name="resource">The persistent imported asset selected by the author.</param>
    public static void AddPackagingRoot(
        NovelProjectAsset project,
        AssetObject resource
    ) {
        if (resource.isMissing || resource.identity.persistentId == Guid.Empty) throw new InvalidOperationException("Packaging roots must be persistent imported assets.");
        if (resource is NovelProjectAsset) throw new InvalidOperationException("A second game definition cannot be an additional packaging root.");
        if (!project.additionalResources.Any(item => item.identity.persistentId == resource.identity.persistentId))
            project.additionalResources = [.. project.additionalResources, resource];
    }
    /// <summary>Adds or replaces a canonical imported resource in its appropriate game library.</summary>
    /// <param name="project">The detached game definition.</param>
    /// <param name="resource">The canonical imported resource.</param>
    /// <param name="screenRole">An implemented shell role when attaching a screen.</param>
    public static void Attach(
        NovelProjectAsset project,
        AssetObject resource,
        string screenRole = "reading"
    ) {
        if (resource.isMissing || resource.identity.persistentId == Guid.Empty) throw new InvalidOperationException("Only persistent imported resources can be attached to a game.");
        switch (resource)
        {
            case StoryAsset chapter:
                project.chapters = [.. project.chapters.Where(item => item.chapterId != chapter.chapterId), chapter]; break;
            case StageAsset stage:
                project.stages = [.. project.stages.Where(item => item.resourceId != stage.resourceId), stage]; break;
            case CharacterAsset character:
                project.characters = [.. project.characters.Where(item => item.resourceId != character.resourceId), character]; break;
            case AudioCueAsset audio:
                project.audio = [.. project.audio.Where(item => item.resourceId != audio.resourceId), audio]; break;
            case AudioClipAsset titleAudio: project.titleAudio = titleAudio; break;
            case PerformanceClipAsset performance:
                project.performances = [.. project.performances.Where(item => item.resourceId != performance.resourceId), performance]; break;
            case VisualEffectAsset effect when effect.kind == VisualEffectKind.Screen:
                project.screenEffects = [.. project.screenEffects.Where(item => item.resourceId != effect.resourceId), effect]; break;
            case VisualEffectAsset effect when effect.kind == VisualEffectKind.Transition:
                project.transitions = [.. project.transitions.Where(item => item.resourceId != effect.resourceId), effect]; break;
            case VisualEffectAsset particles when particles.kind == VisualEffectKind.Particles:
                project.particles = [.. project.particles.Where(item => item.resourceId != particles.resourceId), particles]; break;
            case LocalizationTableAsset texts: project.localization = texts; break;
            case CollectionAsset collections: project.collections = collections; break;
            case Model.Extensions.NovelCommandCatalogAsset commands:
                project.commandDefinitions = [.. project.commandDefinitions.Where(definition => !commands.definitions.Any(item => item.id == definition.id)), .. commands.definitions.Select(Model.Extensions.CommandContract.Copy)]; break;
            case Model.Interface.Actions.UiActionCatalogAsset actions:
                Model.Interface.Actions.UiActionContract.ValidateCatalog(actions.definitions);
                var mergedActions = project.uiActions.Where(definition => !actions.definitions.Any(item => item.id == definition.id)).Concat(actions.definitions).Select(Model.Interface.Actions.UiActionContract.Copy).ToArray();
                Model.Interface.Actions.UiActionContract.ValidateCatalog(mergedActions); project.uiActions = mergedActions; break;
            case Model.Interface.Data.UiDataSourceCatalogAsset sources:
                Model.Interface.Data.UiDataSourceContract.ValidateCatalog(sources.definitions);
                var mergedSources = project.uiDataSources.Where(definition => !sources.definitions.Any(item => item.id == definition.id)).Concat(sources.definitions).Select(Model.Interface.Data.UiDataSourceContract.Copy).ToArray();
                Model.Interface.Data.UiDataSourceContract.ValidateCatalog(mergedSources); project.uiDataSources = mergedSources; break;
            case ScreenAsset screen:
                if (!Model.Story.Nodes.Interface.SystemNodeRules.IsPageRole(screenRole)) throw new ArgumentException("Page role must be a bounded stable identifier.", nameof(screenRole));
                project.screens = [.. project.screens.Where(item => item.role != screenRole), new() { role = screenRole, screen = screen }]; break;
            case ThemeAsset theme:
                project.themes = [.. project.themes.Where(item => item.resourceId != theme.resourceId), theme]; break;
            case FontAsset font: project.font = font; break;
            case MaterialAsset material when material.shader?.definition?.techniques.Any(static item => item.contract == new ShaderContractId("inno.vn.post")) == true: project.effectsMaterial = material; break;
            case MaterialAsset material: project.material = material; break;
            default: throw new ArgumentException("The selected resource cannot be a VN game root.", nameof(resource));
        }
    }
}
