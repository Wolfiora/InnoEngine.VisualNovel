using System;
using System.IO;
using System.Linq;
using Inno.Rendering.Assets;
using Inno.VisualNovel.Authoring.BuiltInResources;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Player.Sessions;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class BuiltInResourceContracts
{
    internal static void CatalogAndPreview(string root)
    {
        var catalog = BuiltInResourceCatalog.entries;
        Check(catalog.Count == 159 && catalog.Count(static item => item.kind == BuiltInResourceKind.Icon) == 68
            && catalog.Count(static item => item.kind == BuiltInResourceKind.Widget) == 7
            && catalog.All(entry => File.Exists(Path.Combine(root, "Assets", entry.path))), "Known packaged preset catalog contains missing sources or lost its actual resource counts.");
        TextureAsset image = new(2, 2, TextureColorSpace.Srgb, "png");
        var scene = new StageAsset { textures = [image], layers = [new() { id = "background", texture = 0, width = 1920, height = 1080 }] };
        var next = StageAssetCopy.Copy(scene); next.layers[0].color = 0x445566FF;
        var actor = new CharacterAsset { textures = [image], poses = [new() { parts = [new() { id = "body", texture = 0 }] }], expressions = [new()] };
        var motion = new PerformanceClipAsset { resourceId = "move", tracks = [new() { target = "actor", property = PerformanceProperty.X,
            keys = [new() { time = 0, value = 0 }, new() { time = 1, value = 100 }] }] };
        var preview = new BuiltInStagePreview(motion, scene, actor, next);
        var half = preview.Evaluate(.5); preview.Evaluate(.9); var repeated = preview.Evaluate(.5);
        Check(half.items.Single(static item => item.owner == "character/preview").topLeft == repeated.items.Single(static item => item.owner == "character/preview").topLeft,
            "Preset scrubbing accumulated prior frame state instead of evaluating the same initial presentation.");
        var transition = new VisualEffectAsset { resourceId = "fade", kind = VisualEffectKind.Transition };
        var boundary = new BuiltInStagePreview(transition, scene, actor, next).Evaluate(.2);
        Check(boundary.transition is not null && boundary.items.Any(static item => item.owner == "character/preview"), "Native transition preview did not retain real previous and new stage composition.");
    }
    internal static void StarterStory(
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var source = StarterPresentationTemplate.Create(codec).ToAsset(); var compiled = compiler.Compile(source);
        Check(compiled.success && compiled.program!.instructions.Length == 9, "Beginner presentation graph did not compile as ordinary visible story nodes."); source.program = compiled.program;
        TextureAsset image = new(2, 2, TextureColorSpace.Srgb, "png");
        var actor = new CharacterAsset { resourceId = "builtin.character.cheng", defaultPose = "wave", defaultExpression = "smile", textures = [image],
            poses = [new() { id = "wave", parts = [new() { id = "body", texture = 0 }] }], expressions = [new() { id = "smile" }] };
        var stages = new[] { "classroom", "seafront", "woodland" }.Select((key, index) => new StageAsset { resourceId = "builtin.stage." + key, textures = [image],
            layers = [new() { id = key, texture = 0, width = 1920, height = 1080, color = index == 0 ? 0xFFFFFFFF : 0xAABBCCFF }] }).ToArray();
        using var session = new NovelSession(new NovelProjectAsset { chapters = [source], startChapter = source.chapterId, characters = [actor], stages = stages, initialStage = stages[0].resourceId });
        session.StartNew();
        Check(session.story!.nodeId == "hello" && session.stage.IsCharacterShown("guide"), "A new teaching story did not visibly show its declared actor before the first line.");
        session.Advance(); session.Advance(); session.Choose("sea");
        Check(session.stage.Capture().layers.Any(static layer => layer.id == "stage/seafront") && session.stage.IsCharacterShown("guide"), "Selecting a route did not change scenery while retaining the existing actor.");
        session.Rollback(); session.Choose("forest");
        Check(session.stage.Capture().layers.Any(static layer => layer.id == "stage/woodland") && !session.stage.Capture().layers.Any(static layer => layer.id == "stage/seafront"), "Rollback and a changed teaching choice retained abandoned scenery.");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
