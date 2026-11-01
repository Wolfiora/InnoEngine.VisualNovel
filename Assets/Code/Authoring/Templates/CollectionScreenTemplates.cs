using System;
using System.Collections.Generic;
using InnoEngine.Text;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Localization;

namespace Inno.VisualNovel.Authoring.Templates;

internal static class CollectionScreenTemplates
{
    internal static ScreenAsset Create(
        string role,
        FontAsset font,
        uint background,
        uint panel,
        uint foreground,
        uint accent
    ) {
        string heading = role switch
        {
            "extras" => "鉴赏与资料", "gallery" => "CG 鉴赏", "music" => "音乐鉴赏", "replays" => "回想重播", "endings" => "结局记录", "glossary" => "词条", "characters" => "角色资料", "collectionDetail" => "", _ => throw new ArgumentException("Unknown collection role.", nameof(role))
        };
        var children = new List<UiElementDefinition>
        {
            new() { id = "background", kind = UiElementKind.Panel, layout = new() { width = 1920, height = 1080 }, style = new() { background = background } },
            Text("heading", heading, "", 112, 80, 1500, 100, 52),
            At(Button("back", "返回", "back"), 1550, 70)
        };
        if (role == "extras")
        {
            string[] roles = ["gallery", "music", "replays", "endings", "glossary", "characters"];
            string[] names = ["CG 鉴赏", "音乐鉴赏", "回想重播", "结局记录", "词条", "角色资料"];
            for (int index = 0; index < roles.Length; index++)
            {
                var button = Button(roles[index], names[index], "open", roles[index]); button.layout.width = 700; button.layout.height = 150;
                children.Add(At(button, 112 + index % 2 * 820, 300 + index / 2 * 200));
            }
        }
        else if (role == "collectionDetail")
        {
            children[1].binding = "collection.title";
            children.Add(Text("variant-name", "", "collection.variantName", 112, 174, 1000, 48, 26));
            children.Add(new() { id = "artwork", kind = UiElementKind.ImageViewport, imageBinding = "collection.image", imageFit = UiImageFit.Contain,
                layout = new() { x = 112, y = 230, width = 1000, height = 500 }, style = new() { background = 0x090D12CC, focus = accent } });
            var previousImage = Button("image-previous", "上一张", "collection.image.previous"); previousImage.enabledBinding = "collection.imagePrevious"; previousImage.layout.width = 210; previousImage.layout.height = 110;
            var nextImage = Button("image-next", "下一张", "collection.image.next"); nextImage.enabledBinding = "collection.imageNext"; nextImage.layout.width = 210; nextImage.layout.height = 110;
            var zoomIn = Button("zoom-in", "放大", "image.zoomIn", "artwork");
            var zoomOut = Button("zoom-out", "缩小", "image.zoomOut", "artwork");
            var reset = Button("zoom-reset", "适应", "image.reset", "artwork");
            foreach (var button in new[] { zoomIn, zoomOut, reset }) { button.layout.width = 300; button.layout.height = 110; button.enabledBinding = "collection.hasImage"; }
            children.Add(new() { id = "image-controls", kind = UiElementKind.Horizontal, blocksPointer = false,
                layout = new() { x = 112, y = 752, width = 1000, height = 110, gap = 24 },
                children = [previousImage, Text("image-page", "", "collection.imagePage", 0, 0, 360, 110, 26), nextImage] });
            children.Add(new() { id = "zoom-controls", kind = UiElementKind.Horizontal, blocksPointer = false,
                layout = new() { x = 112, y = 878, width = 1000, height = 110, gap = 24 }, children = [zoomIn, zoomOut, reset] });
            children.Add(Text("image-help", "滚轮缩放 · 拖动查看 · Enter 复位", "", 112, 1000, 1000, 72, 22));
            children.Add(new() { id = "article", kind = UiElementKind.Scroll, blocksPointer = true,
                layout = new() { x = 1150, y = 240, width = 650, height = 740, clip = true, gap = 20 },
                children = [Text("author", "", "collection.author", 0, 0, 610, 80, 28),
                    new() { id = "description", kind = UiElementKind.Text, binding = "collection.description", blocksPointer = false,
                        layout = new() { width = 610, heightMode = UiSizeMode.Content }, style = new() { foreground = foreground, fontSize = 30 } }] });
        }
        else
        {
            children.Add(Text("search-label", "搜索已解锁内容", "", 112, 190, 400, 60, 26));
            children.Add(new() { id = "search", kind = UiElementKind.Input, binding = "collections.search", maxLength = 100,
                layout = new() { x = 500, y = 185, width = 1020, height = 68 }, style = Style() });
            UiElementDefinition select = Button("select", "", "collection.select"); select.binding = "item.title"; select.argumentBinding = "item.id"; select.enabledBinding = "item.unlocked"; select.layout.width = 1150;
            children.Add(new()
            {
                id = "items", kind = UiElementKind.Scroll, repeatBinding = "collections.items", layout = new() { x = 112, y = 285, width = 1696, height = role == "music" ? 410 : 590, gap = 20, padding = 8, clip = true },
                children = [new() { id = "item", kind = UiElementKind.Horizontal, blocksPointer = false, layout = new() { width = 1620, height = 140, gap = 28 },
                    children = [new() { id = "cover", kind = UiElementKind.Image, imageBinding = "item.image", imageFit = UiImageFit.Contain, blocksPointer = false, layout = new() { width = 230, height = 130 }, style = new() { background = 0 } }, select] }]
            });
            var empty = Text("empty", "这里还没有内容。在 VN 收藏中添加条目并关联到游戏。", "", 112, 320, 1550, 120, 30); empty.visibleBinding = "collections.empty"; children.Add(empty);
            UiElementDefinition previous = Button("previous", "上一页", "collections.previous"); previous.enabledBinding = "collections.previous";
            UiElementDefinition next = Button("next", "下一页", "collections.next"); next.enabledBinding = "collections.next";
            children.Add(new() { id = "pages", kind = UiElementKind.Horizontal, blocksPointer = false,
                layout = new() { x = 112, y = 962, width = 1550, height = 76, gap = 28 }, children = [previous, Text("page", "", "collections.page", 0, 0, 220, 76, 28), next] });
            if (role == "music")
            {
                children.Add(Text("track", "", "music.title", 112, 705, 1400, 65, 32));
                var controls = new[] { Button("music-previous", "上一首", "music.previous"), Button("music-pause", "暂停 / 继续", "music.pause"), Button("music-next", "下一首", "music.next") };
                foreach (var control in controls) control.enabledBinding = "music.available";
                children.Add(new() { id = "music-controls", kind = UiElementKind.Horizontal, blocksPointer = false,
                    layout = new() { x = 112, y = 790, width = 1600, height = 76, gap = 28 }, children = controls });
                children.Add(Text("repeat-label", "单曲循环", "", 112, 880, 300, 60, 26));
                children.Add(At(new() { id = "repeat", kind = UiElementKind.Toggle, binding = "music.repeat", layout = new() { width = 100, height = 60 }, style = Style() }, 420, 875));
                children.Add(Text("shuffle-label", "随机顺序", "", 650, 880, 300, 60, 26));
                children.Add(At(new() { id = "shuffle", kind = UiElementKind.Toggle, binding = "music.shuffle", layout = new() { width = 100, height = 60 }, style = Style() }, 960, 875));
            }
        }
        var root = new UiElementDefinition { id = "root", kind = UiElementKind.Panel, blocksPointer = false, layout = new() { width = 1920, height = 1080, clip = true }, style = new() { background = 0 }, children = children.ToArray() };
        AssignTextIds(root);
        UiAnimationTemplates.Apply(root);
        return new() { title = heading, font = font, modal = true, root = root };

        UiStyleDefinition Style() => new() { background = panel, foreground = foreground, focus = accent, hover = Lighten(panel), pressed = background, fontSize = 28, textPadding = 16 };
        UiElementDefinition Button(
            string id,
            string text,
            string action,
            string argument = ""
        ) => new() { id = id, kind = UiElementKind.Button, text = text, action = action, argument = argument, layout = new() { width = 240, height = 76 }, style = Style() };
        UiElementDefinition Text(
            string id,
            string text,
            string binding,
            float x,
            float y,
            float width,
            float height,
            float size
        ) => new() { id = id, kind = UiElementKind.Text, text = text, binding = binding, blocksPointer = false,
            layout = new() { x = x, y = y, width = width, height = height }, style = new() { foreground = foreground, fontSize = size, background = 0 } };
        void AssignTextIds(UiElementDefinition element) { element.textId = StoryTextKeys.Ui(role, element.id); foreach (var child in element.children) AssignTextIds(child); }
    }
    private static UiElementDefinition At(
        UiElementDefinition element,
        float x,
        float y
    ) { element.layout.x = x; element.layout.y = y; return element; }
    private static uint Lighten(uint color) => Math.Min(255u, (color >> 24) + 8) << 24
        | Math.Min(255u, (color >> 16 & 255) + 8) << 16 | Math.Min(255u, (color >> 8 & 255) + 8) << 8 | color & 255;
}
