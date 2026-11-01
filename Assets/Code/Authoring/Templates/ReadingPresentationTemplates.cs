using System.Collections.Generic;
using Inno.VisualNovel.Model.Interface;

namespace Inno.VisualNovel.Authoring.Templates;

internal static class ReadingPresentationTemplates
{
    internal static void AddPortrait(ScreenAsset screen)
    {
        var dialogue = UiEditing.ScreenEditService.Find(screen, "dialogue");
        var speaker = UiEditing.ScreenEditService.Find(screen, "speaker"); var scroll = UiEditing.ScreenEditService.Find(screen, "dialogue-scroll");
        speaker.layout.x = speaker.layout.y = 0; speaker.layout.widthMode = UiSizeMode.Fill; speaker.layout.heightMode = UiSizeMode.Content; speaker.layout.minHeight = 50;
        scroll.layout.x = scroll.layout.y = 0; scroll.layout.widthMode = UiSizeMode.Fill; scroll.layout.heightMode = UiSizeMode.Fill;
        dialogue.children = [new() { id = "dialogue-content", kind = UiElementKind.Horizontal, blocksPointer = false,
            layout = new() { widthMode = UiSizeMode.Fill, heightMode = UiSizeMode.Fill, gap = 20 }, children = [
                new() { id = "dialogue-portrait", kind = UiElementKind.Image, imageBinding = "dialogue.portrait", visibleBinding = "dialogue.hasPortrait", imageFit = UiImageFit.Contain, blocksPointer = false,
                    layout = new() { width = 180, heightMode = UiSizeMode.Fill } },
                new() { id = "dialogue-body", kind = UiElementKind.Vertical, blocksPointer = false,
                    layout = new() { widthMode = UiSizeMode.Fill, heightMode = UiSizeMode.Fill, gap = 12 }, children = [speaker, scroll] }] }];
    }
    internal static void Add(
        List<UiElementDefinition> children,
        uint panel,
        uint foreground,
        uint accent
    ) {
        var page = Panel("nvl-page", "dialogue.nvl", 160, 110, 1600, 840, panel);
        page.children = [new()
        {
            id = "nvl-scroll", kind = UiElementKind.Scroll, followEndBinding = "dialogue.nvl", blocksPointer = false,
            layout = new() { widthMode = UiSizeMode.Fill, heightMode = UiSizeMode.Fill, padding = 24, clip = true },
            children = [Body("nvl-text", "dialogue.pageText", foreground, 34)]
        }];
        children.Add(page);
        var letter = Panel("letter-page", "dialogue.letter", 360, 100, 1200, 850, panel);
        UiElementDefinition heading = Body("letter-speaker", "dialogue.speaker", accent, 34); heading.layout.heightMode = UiSizeMode.Fixed; heading.layout.height = 70;
        letter.children = [heading, new()
        {
            id = "letter-scroll", kind = UiElementKind.Scroll, followEndBinding = "dialogue.letter", blocksPointer = false,
            layout = new() { y = 86, widthMode = UiSizeMode.Fill, height = 710, padding = 20, clip = true }, children = [Body("letter-text", "dialogue.text", foreground, 34)]
        }];
        children.Add(letter);
        Positioned("bubble", "dialogue.bubble", panel, 32);
        Positioned("overlay", "dialogue.overlay", 0, 42);

        void Positioned(string id, string visible, uint background, float size)
        {
            var element = Panel(id + "-panel", visible, 480, 270, 960, 260, background);
            element.geometryBinding = new() { x = "dialogue.positionX", y = "dialogue.positionY", width = "dialogue.widthRatio", normalized = true };
            element.layout.heightMode = UiSizeMode.Content;
            UiElementDefinition speaker = Body(id + "-speaker", "dialogue.speaker", accent, 28); speaker.layout.heightMode = UiSizeMode.Fixed; speaker.layout.height = 50;
            UiElementDefinition body = Body(id + "-text", "dialogue.text", foreground, size); body.layout.y = 56;
            element.children = [speaker, body]; children.Add(element);
        }
    }

    private static UiElementDefinition Panel(string id, string visible, float x, float y, float width, float height, uint color) => new()
    {
        id = id, kind = UiElementKind.Panel, visibleBinding = visible, blocksPointer = false,
        layout = new() { x = x, y = y, width = width, height = height, padding = 20 }, style = new() { background = color }
    };
    private static UiElementDefinition Body(string id, string binding, uint foreground, float size) => new()
    {
        id = id, kind = UiElementKind.Text, binding = binding, blocksPointer = false,
        layout = new() { widthMode = UiSizeMode.Fill, heightMode = UiSizeMode.Content },
        style = new() { foreground = foreground, fontSize = size, lineSpacing = 1.6f, textPadding = 12 }
    };
}
