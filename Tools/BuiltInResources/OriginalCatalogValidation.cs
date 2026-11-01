using System;
using System.Linq;
using Inno.Assets;
using Inno.Rendering.Assets;
using Inno.VisualNovel.Authoring.BuiltInResources;
using Inno.VisualNovel.Model.Audio;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Model.Interface.Themes;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.UI.Documents;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Stages;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal static class OriginalCatalogValidation
{
    internal static void Run()
    {
        var scene = Assets.Assets.Load<StageAsset>(AssetPath.Project("BuiltIn/Backgrounds/Classroom.ivnstage"));
        var actor = Assets.Assets.Load<CharacterAsset>(AssetPath.Project("BuiltIn/Characters/Cheng/Cheng.ivncharacter"));
        var destination = Assets.Assets.Load<StageAsset>(AssetPath.Project("BuiltIn/Backgrounds/Seafront.ivnstage"));
        int stagePreviews = 0;
        foreach (var entry in BuiltInResourceCatalog.entries)
        {
            var path = AssetPath.Project(entry.path);
            AssetObject asset = entry.kind switch
            {
                BuiltInResourceKind.Theme => Assets.Assets.Load<ThemeAsset>(path), BuiltInResourceKind.Stage => Assets.Assets.Load<StageAsset>(path),
                BuiltInResourceKind.Character => Assets.Assets.Load<CharacterAsset>(path), BuiltInResourceKind.Motion => Assets.Assets.Load<PerformanceClipAsset>(path),
                BuiltInResourceKind.Audio => Assets.Assets.Load<AudioCueAsset>(path), BuiltInResourceKind.Icon => Assets.Assets.Load<TextureAsset>(path),
                BuiltInResourceKind.Widget => Assets.Assets.Load<ScreenAsset>(path),
                _ => Assets.Assets.Load<VisualEffectAsset>(path)
            };
            if (asset.isMissing) throw new InvalidOperationException("Catalog preset did not resolve through native Assets: " + entry.path);
            if (asset is ScreenAsset widget) _ = new UiDocumentInstance(widget, static (_, _) => (100, 40));
            if (asset is not (StageAsset or CharacterAsset or PerformanceClipAsset or VisualEffectAsset)) continue;
            var preview = new BuiltInStagePreview(asset, scene, actor, destination);
            var frame = preview.Evaluate(preview.duration * .37);
            if (frame.items.Any(static item => !float.IsFinite(item.topLeft.X) || !float.IsFinite(item.topLeft.Y)))
                throw new InvalidOperationException("Catalog preview produced invalid stage geometry: " + entry.path);
            stagePreviews++;
        }
        Console.WriteLine("Catalog: " + BuiltInResourceCatalog.entries.Count + " native resources loaded; " + stagePreviews + " stage previews evaluated without GPU or audio devices.");
    }
}
