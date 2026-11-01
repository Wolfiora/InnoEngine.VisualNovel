using System;
using System.IO;
using System.Linq;
using Inno.Assets;
using Inno.Assets.Pipeline;
using Inno.Audio;
using Inno.Core.Diagnostics;
using Inno.Core.Identity;
using Inno.Core.Logging;
using Inno.Core.Serialization;
using Inno.Extensibility.Modules;
using Inno.Extensibility.Types;
using Inno.Rendering.Assets;
using Inno.VisualNovel.Model.Audio;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Stage.Composition;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class NativeResourceContracts
{
    internal static void Run(
        string project,
        ModuleHost modules,
        TypeCatalog types,
        SerializationRegistry serialization,
        IdentityAllocator identities
    ) {
        string root = Path.Combine(project, "artifacts", "AcceptanceProjects", "NativeResources-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Assets"));
        File.WriteAllBytes(Path.Combine(root, "Assets", "Pixel.png"), Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jXioAAAAASUVORK5CYII="));
        File.WriteAllBytes(Path.Combine(root, "Assets", "Tone.wav"), Wave());
        using var logs = new LogRouter();
        using var assets = new AssetPipeline(modules, types, serialization, identities, new DiagnosticHub(), logs,
            AssetPipelineOptions.Create(Path.Combine(root, "Assets"), Path.Combine(root, "Library")) with { enableFileSystemWatcher = false, deferUnavailableExtensions = true });
        using var scope = AssetExecutionContext.EnterScope(assets);
        TextureAsset image = Assets.Assets.Load<TextureAsset>(AssetPath.Project("Pixel.png"));
        Check(image.width == 1 && image.height == 1 && image.identity.persistentId != Guid.Empty, "Native PNG import failed.");
        File.WriteAllBytes(Path.Combine(root, "Assets", "Snapshot.png"), Inno.VisualNovel.UI.Imaging.UiPngImage.Encode(new(2, 1, new byte[] { 32, 64, 128, 255, 128, 64, 32, 128 })));
        TextureAsset snapshotImage = Assets.Assets.Load<TextureAsset>(AssetPath.Project("Snapshot.png"));
        Check(!snapshotImage.isMissing && snapshotImage.width == 2 && snapshotImage.height == 1, "Save thumbnail PNG was rejected by the engine's independent native importer.");
        var character = new CharacterAsset { resourceId = "actor", aliases = ["Actor"], portrait = image, nameColor = 0xA9D5F0FF, textures = [image], poses = [new() { parts = [
            new() { id = "body", texture = 0, width = 100, height = 200 },
            new() { id = "face", texture = 0, width = 20, height = 20, order = 1, transform = new() { x = 40, y = 20 } }] }],
            expressions = [new(), new() { id = "hidden", parts = [new() { partId = "face", visibility = 0, offsetX = 5 }] }] };
        Check(EditorAssets.Save(AssetPath.Project("Actor.ivncharacter"), character), "Native character source import failed.");
        CharacterAsset imported = Assets.Assets.Load<CharacterAsset>(AssetPath.Project("Actor.ivncharacter"));
        Check(imported.aliases.Single() == "Actor" && imported.nameColor == 0xA9D5F0FF && imported.portrait?.identity.persistentId == image.identity.persistentId, "Native character metadata or its declared portrait dependency was lost.");
        CharacterImageImportContracts.Run(root, assets, imported);
        var animatedSource = CharacterAssetCopy.Copy(imported); animatedSource.breathing.enabled = true; animatedSource.breathing.anchorX = 0.25;
        StageSurfaceContracts.NativeCharacter(imported, image, serialization);
        animatedSource.poses[0].parts[1].animation = new() { mode = Model.Characters.CharacterAnimationMode.Talking, essential = true,
            frames = [new() { texture = 0, seconds = 0.13 }] };
        animatedSource.poses[0].parts[1].color = 0xCCDDFFFF;
        animatedSource.poses[0].parts[1].mask = 0; animatedSource.poses[0].parts[1].surface.cropWidth = .5;
        animatedSource.poses[0].parts[1].surface.maskMode = Model.Stages.StageMaskMode.InverseAlpha;
        animatedSource.poses[0].parts[1].transform.pivotX = 7;
        Check(EditorAssets.Save(AssetPath.Project("Animated.ivncharacter"), animatedSource), "Native animated character declaration did not import.");
        var animated = Assets.Assets.Load<CharacterAsset>(AssetPath.Project("Animated.ivncharacter"));
        Check(animated.breathing.enabled && animated.breathing.anchorX == 0.25 && animated.poses[0].parts[1].animation.frames[0].seconds == 0.13
            && animated.poses[0].parts[1].color == 0xCCDDFFFF, "Native character import lost frame, breathing or nondestructive tint data.");
        Check(animated.poses[0].parts[1].mask == 0 && animated.poses[0].parts[1].surface.cropWidth == .5
            && animated.poses[0].parts[1].surface.maskMode == Model.Stages.StageMaskMode.InverseAlpha && animated.poses[0].parts[1].transform.pivotX == 7,
            "Native character source lost mask, crop or pivot declarations.");
        var variant = CharacterAssetCopy.Copy(animated);
        string variantPose = Inno.VisualNovel.Authoring.CharacterEditing.CharacterEditService.DuplicatePose(variant, variant.defaultPose);
        var basePart = variant.poses[0].parts[1];
        Inno.VisualNovel.Authoring.CharacterEditing.CharacterEditService.SetPartImage(variant, variantPose, basePart.id, snapshotImage);
        var variantPart = variant.poses[1].parts[1];
        Check(variant.poses[0].parts[1].texture == basePart.texture && variantPart.width == basePart.width && variantPart.mask == basePart.mask
            && variantPart.animation.frames[0].texture == basePart.animation.frames[0].texture && variantPart.texture != basePart.texture,
            "Replacing one pose's base image changed another pose, authored dimensions, masks or animation frames.");
        Inno.VisualNovel.Authoring.CharacterEditing.CharacterEditService.SetPartImage(variant, variantPose, basePart.id, snapshotImage, true);
        Check(variantPart.width == 2 && variantPart.height == 1, "Explicit base image resizing ignored native image dimensions.");
        variant.defaultPose = variantPose;
        string variantExpression = Inno.VisualNovel.Authoring.CharacterEditing.CharacterVariantEditService.DuplicateExpression(variant, "hidden");
        variant.defaultExpression = variantExpression;
        Check(EditorAssets.Save(AssetPath.Project("Variants.ivncharacter"), variant), "Edited variant library did not save through native imports.");
        var nativeVariant = Assets.Assets.Load<CharacterAsset>(AssetPath.Project("Variants.ivncharacter"));
        Check(nativeVariant.defaultPose == variantPose && nativeVariant.defaultExpression == variantExpression
            && nativeVariant.textures[nativeVariant.poses.Single(item => item.id == variantPose).parts[1].texture].identity.persistentId == snapshotImage.identity.persistentId,
            "Native variant source roundtrip changed defaults or the pose-specific image dependency.");
        var stage = new StageController(imported.textures);
        stage.ShowCharacter("lead", imported, "", "neutral", new() { x = 100, y = 200, scaleX = -1 });
        var frame = stage.BuildFrame(1920, 1080);
        Check(frame.items.Count == 2 && frame.items[1].id.EndsWith("/face", StringComparison.Ordinal) && frame.items[1].topLeft.X == 60, "Character affine composition or part order was lost.");
        var saved = serialization.Deserialize<StageState>(serialization.Serialize(stage.Capture()));
        stage.ShowCharacter("lead", imported, "", "hidden", new());
        Check(stage.BuildFrame(1920, 1080).items.Count == 1, "Expression visibility did not override the pose nondestructively.");
        stage.Restore(saved);
        Check(stage.BuildFrame(1920, 1080).items.Count == 2 && stage.BuildFrame(1920, 1080).items[1].topLeft.X == 60, "Native character stage restoration changed the composition.");
        AudioClipAsset clip = Assets.Assets.Load<AudioClipAsset>(AssetPath.Project("Tone.wav"));
        Check(Math.Abs(clip.duration.TotalSeconds - 0.1) < 1e-9, "Native WAV metadata has an incorrect duration.");
        var content = Inno.VisualNovel.Authoring.VoiceManagement.NativeWaveformSource.Read(clip);
        var trimmed = Inno.VisualNovel.Authoring.VoiceManagement.NativeWaveformSource.CreateTrimmedClip(root, content, 0.025, 0.075);
        Check(!trimmed.isMissing && Math.Abs(trimmed.duration.TotalSeconds - 0.05) < 1e-9 && clip.duration.TotalSeconds == 0.1
            && trimmed.identity.persistentId != clip.identity.persistentId, "Native audio crop replaced its source, failed to import or changed frame duration.");
        Check(EditorAssets.Save(AssetPath.Project("Tone.ivnaudio"), new AudioCueAsset { resourceId = "tone", clip = clip }), "Native audio cue source import failed.");
        AudioCueAsset cue = Assets.Assets.Load<AudioCueAsset>(AssetPath.Project("Tone.ivnaudio"));
        AudioTimelineContracts.Run(cue, serialization);
        AuditionContracts.Run(cue);
        Check(cue.clip!.identity.persistentId == clip.identity.persistentId, "Native cue import changed the clip reference.");
        var particles = ParticleContracts.Preset(); particles.particles.texture = image;
        Check(EditorAssets.Save(AssetPath.Project("Rain.ivneffect"), particles), "Native polymorphic effect source import failed.");
        var importedEffect = Assets.Assets.Load<Inno.VisualNovel.Model.Performance.VisualEffectAsset>(AssetPath.Project("Rain.ivneffect"));
        Check(!importedEffect.isMissing && importedEffect.resourceId == "rain" && importedEffect.particles.texture!.identity.persistentId == image.identity.persistentId,
            "Native particle resource import changed its derived type or sprite dependency.");
        var preview = new Inno.VisualNovel.Authoring.Preview.NovelPreviewCaseAsset { title = "局部舞台", mode = Inno.VisualNovel.Authoring.Preview.PreviewRunMode.Local, nodeId = "hello", useStageSnapshot = true, stage = saved };
        preview.breakpoints = [new() { chapter = "chapter", node = "hello", conditional = true }];
        Check(EditorAssets.Save(AssetPath.Project("Preview.ivntest"), preview), "Native editor-only preview case did not import.");
        var importedPreview = Assets.Assets.Load<Inno.VisualNovel.Authoring.Preview.NovelPreviewCaseAsset>(AssetPath.Project("Preview.ivntest"));
        Check(!importedPreview.isMissing && importedPreview.stage.layers.Length == 2 && importedPreview.stage.layers[0].textureId == image.identity.persistentId,
            "Native preview case lost explicit neutral stage preconditions.");
        Check(importedPreview.breakpoints.Length == 1 && importedPreview.breakpoints[0].Build().condition!.literal.boolean,
            "Native preview case lost its immutable source breakpoint condition.");
        Check(EditorAssets.Save(AssetPath.Project("Styled.ivnui"), ThemeContracts.Screen()), "Native page style references did not import.");
        var themedPage = Assets.Assets.Load<Model.Interface.ScreenAsset>(AssetPath.Project("Styled.ivnui"));
        var theme = ThemeContracts.Create(); theme.screens = [new() { role = "title", screen = themedPage }];
        Check(EditorAssets.Save(AssetPath.Project("Clear.ivntheme"), theme), "Native theme source did not import.");
        var nativeTheme = Assets.Assets.Load<Model.Interface.Themes.ThemeAsset>(AssetPath.Project("Clear.ivntheme"));
        Check(nativeTheme.screens[0].screen!.identity.persistentId == themedPage.identity.persistentId
            && new UI.Theming.UiThemeResolver(nativeTheme).Apply(themedPage).root.children[0].style.background == 0x336699FF,
            "Native theme roundtrip lost typed tokens, inheritance or canonical page references.");
        Console.WriteLine("Native resource acceptance project: " + root);
    }
    private static byte[] Wave()
    {
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output);
        const int frames = 800;
        writer.Write("RIFF"u8); writer.Write(36 + frames * 2); writer.Write("WAVEfmt "u8); writer.Write(16);
        writer.Write((ushort)1); writer.Write((ushort)1); writer.Write(8000); writer.Write(16000); writer.Write((ushort)2); writer.Write((ushort)16);
        writer.Write("data"u8); writer.Write(frames * 2);
        for (int index = 0; index < frames; index++) writer.Write((short)(Math.Sin(index * Math.PI * 2 * 440 / 8000) * 4000));
        writer.Flush(); return output.ToArray();
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
