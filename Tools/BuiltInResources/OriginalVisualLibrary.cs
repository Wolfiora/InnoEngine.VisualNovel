using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Inno.Assets;
using Inno.Assets.Pipeline;
using Inno.Rendering.Assets;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Stage.Composition;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal static class OriginalVisualLibrary
{
    internal static void WriteSources(string root)
    {
        IReadOnlyList<string> icons = OriginalIconLibrary.Write(root);
        OriginalSceneArt.Write(root); OriginalCharacterArt.Write(root);
        string directory = Path.Combine(root, "Assets", "BuiltIn", "Licenses"); Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "OriginalVisuals.md"),
            "# 原创教学美术与图标\n\n作者：InnoEngine.VisualNovel 项目。来源：本仓库 Tools/BuiltInResources 的矢量绘制声明；SVG 源位于 Tools/ArtResources/Sources。未复制其他 VN 产品的素材。\n\n"
            + "许可：与仓库源码一致的 Apache License 2.0。允许复制、修改、商用及随游戏和插件分发，须保留许可及版权说明；许可全文随包位于同目录 Apache-2.0.txt。\n\n"
            + "包含 " + icons.Count + " 个统一线条图标、6 张 1920×1080 教学背景、2 个 768×1152 分层角色。每角色提供 2 姿势、6 表情、独立后发/身体/脸/表情/前发。PNG 使用直通透明度，SVG 与 PNG 由同一声明生成。\n\n"
            + "这些资源是教学用简洁矢量插画，可自由替换。字体与音频使用各自独立许可说明。\n");
        File.Copy(Path.Combine(root, "LICENSE"), Path.Combine(directory, "Apache-2.0.txt"), overwrite: true);
        Console.WriteLine("Original visual sources: " + icons.Count + " monochrome icons, 6 backgrounds, 2 layered characters with 2 poses and 6 expressions each.");
    }
    internal static void Import(
        string root,
        bool refreshCharacters = false,
        bool refreshStages = false
    )
    {
        var characters = new OwnedSourceRefresh(root, "BuiltIn/Characters", refreshCharacters);
        var stages = new OwnedSourceRefresh(root, "BuiltIn/Backgrounds", refreshStages);
        foreach (string name in OriginalSceneArt.names)
        {
            TextureAsset image = Load("BuiltIn/Backgrounds/" + name + ".png", 1920, 1080);
            string id = "builtin.stage." + name.ToLowerInvariant();
            stages.Save("BuiltIn/Backgrounds/" + name + ".ivnstage", new StageAsset { resourceId = id, title = Title(name), textures = [image],
                layers = [new() { id = "background", name = Title(name), texture = 0, width = 1920, height = 1080 }] });
            var stage = Assets.Assets.Load<StageAsset>(AssetPath.Project("BuiltIn/Backgrounds/" + name + ".ivnstage"));
            if (stage.isMissing || stage.textures.Length != 1) throw new InvalidOperationException("Native built-in stage reference failed: " + name);
        }
        foreach (string name in OriginalCharacterArt.names)
        {
            string[] parts = ["HairBack", "BodyIdle", "BodyWave", "Face", "HairFront", .. OriginalCharacterArt.expressions];
            var textures = parts.Select(part => Load("BuiltIn/Characters/" + name + "/" + part + ".png", 768, 1152)).ToArray();
            var source = new CharacterAsset { resourceId = "builtin.character." + name.ToLowerInvariant(), displayName = name == "Cheng" ? "澄" : "树", textures = textures,
                aliases = [name], nameColor = name == "Cheng" ? 0xA9D5F0FFu : 0xC6D8AAFFu, portrait = Load("BuiltIn/Characters/" + name + "/Avatar.png", 320, 400),
                defaultPose = "idle", defaultExpression = "neutral", breathing = new() { enabled = true, amplitude = .003, period = 4.2 },
                poses = new[] { "idle", "wave" }.Select((pose, index) => new CharacterPose { id = pose, name = index == 0 ? "自然站立" : "挥手致意",
                    parts = [Part("hair-back", 0, 0), Part("body", index == 0 ? 1 : 2, 10), Part("face", 3, 20), Part("features", 5, 30), Part("hair-front", 4, 40)] }).ToArray(),
                expressions = OriginalCharacterArt.expressions.Select((expression, index) => new CharacterExpression { id = expression.ToLowerInvariant(),
                    name = new[] { "平静", "微笑", "难过", "生气", "惊讶", "害羞" }[index], parts = [new() { partId = "features", texture = 5 + index }] }).ToArray() };
            string path = "BuiltIn/Characters/" + name + "/" + name + ".ivncharacter"; characters.Save(path, source);
            var imported = Assets.Assets.Load<CharacterAsset>(AssetPath.Project(path));
            if (imported.isMissing || imported.poses.Length != 2 || imported.expressions.Length != 6) throw new InvalidOperationException("Native layered character definition failed: " + name);
            var controller = new StageController(imported.textures);
            foreach (var pose in imported.poses) foreach (var expression in imported.expressions)
            {
                controller.ShowCharacter("sample", imported, pose.id, expression.id, new() { x = 660, y = 130 }, 0);
                if (controller.BuildFrame(1920, 1080).items.Count != 5) throw new InvalidOperationException("A native layered character combination lost a part.");
            }
        }
        foreach (string path in Directory.EnumerateFiles(Path.Combine(root, "Assets", "BuiltIn", "Icons"), "*.png"))
            _ = Load("BuiltIn/Icons/" + Path.GetFileName(path), 128, 128);
        Console.WriteLine("Native visual resources: 6 stage imports and all 24 character pose/expression combinations verified; icon PNG artifacts imported.");
    }
    private static CharacterPart Part(
        string id,
        int texture,
        int order
    ) => new() { id = id, name = id switch { "hair-back" => "后发", "body" => "身体", "face" => "脸", "features" => "表情", "hair-front" => "前发", _ => id }, texture = texture, order = order, width = 600, height = 900 };
    private static TextureAsset Load(
        string path,
        int width,
        int height
    ) {
        var image = Assets.Assets.Load<TextureAsset>(AssetPath.Project(path));
        if (image.isMissing || image.width != width || image.height != height) throw new InvalidOperationException("Native artwork image import failed: " + path);
        return image;
    }
    private static string Title(string name) => name switch
    { "Classroom" => "午后的教室", "Station" => "黄昏车站", "Seafront" => "海边步道", "Woodland" => "林间小径", "Bedroom" => "安静卧室", "Rooftop" => "天台远景", _ => throw new ArgumentException("Unknown built-in scene.") };
}
