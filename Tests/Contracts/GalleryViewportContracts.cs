using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Inno.Assets;
using Inno.Assets.Pipeline;
using Inno.Core.Diagnostics;
using Inno.Core.Identity;
using Inno.Core.Logging;
using Inno.Core.Mathematics;
using Inno.Core.Serialization;
using Inno.Extensibility.Modules;
using Inno.Extensibility.Types;
using Inno.Rendering;
using Inno.Rendering.Assets;
using Inno.VisualNovel.Authoring.CollectionEditing;
using Inno.VisualNovel.Authoring.LocalizationEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Collections;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Player.Collections;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Player.Shell;
using Inno.VisualNovel.Rendering.Frames;
using Inno.VisualNovel.UI.Binding;
using Inno.VisualNovel.UI.Documents;
using Inno.VisualNovel.UI.Imaging;
using Inno.VisualNovel.UI.Input;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class GalleryViewportContracts
{
    internal static void ImageInputAndNeutralPaint(SerializationRegistry native)
    {
        var data = new BitmapSource();
        var page = new ScreenAsset { width = 600, height = 400, root = new() { id = "root", kind = UiElementKind.Panel, blocksPointer = false,
            layout = new() { width = 600, height = 400 }, style = new() { background = 0 }, children = [
                new() { id = "photo", kind = UiElementKind.ImageViewport, imageBinding = "picture", imageFit = UiImageFit.Contain,
                    layout = new() { x = 40, y = 40, width = 200, height = 200 }, style = new() { background = 0x111111FF } }] } };
        var view = new UiDocumentInstance(page, static (_, _) => (0, 0));
        UiDrawItem item = view.BuildFrame(data).items.Single(static item => item.id == "photo");
        Check(item.bounds == new Rect(40, 40, 200, 200) && item.imageBounds == new Rect(40, 90, 200, 100), "Image fitting changed the focus or pointer rectangle.");
        var zoom = view.HandleInput(new() { x = 140, y = 140, pointerInside = true, scroll = 48 }, data);
        Check(zoom.consumed && zoom.actions.Count == 0 && Math.Abs(view.GetImageZoom("photo") - 1.25f) < .001,
            "A viewport scroll did not zoom exactly once while consuming the underlying page input.");
        view.ZoomImage("photo", 100); Check(view.GetImageZoom("photo") == 16, "Image zoom exceeded its bounded magnification.");
        view.HandleInput(new() { x = 140, y = 140, pointerInside = true, pressed = true, held = true }, data);
        Check(view.HandleInput(new() { x = -10000, y = -10000, held = true }, data).consumed
            && view.HandleInput(new() { x = -10000, y = -10000, released = true }, data).consumed,
            "Dragging or releasing outside the image lost its pointer ownership.");
        UiFrame frame = view.BuildFrame(data); item = frame.items.Single(static item => item.id == "photo");
        Rect image = item.imageBounds!.Value;
        Check(image.Contains(item.bounds) && item.clip == item.bounds, "Panning exposed a blank edge or escaped the authored clip.");
        var restored = native.Deserialize<UiFrameSnapshot>(native.Serialize(UiFrameSnapshot.Capture(frame))).Restore(_ => throw new Exception(), _ => throw new Exception());
        var painter = new NovelFrameComposer();
        var quad = painter.Compose(restored, new MaterialAsset()).quads.Single(static quad => quad.texture.bitmap is not null);
        Check(quad.bounds == image && quad.clip == item.clip, "Native UI capture or the real frame composer lost viewport geometry.");
        painter.Clear();
        Check(view.Focus("photo") && view.HandleInput(new() { pageDown = true }, data).consumed && view.GetImageZoom("photo") < 16,
            "Keyboard-only image zoom did not use the authored viewport focus.");
        float previousZoom = view.GetImageZoom("photo");
        view.HandleInput(new() { composing = true, pageDown = true, panUp = true, previous = true }, data);
        Check(view.GetImageZoom("photo") == previousZoom, "Composition input triggered image shortcuts.");
        Check(view.HandleInput(new() { panUp = true, previous = true }, data).consumed && view.focusedElement == "photo",
            "An image pan arrow moved keyboard focus instead of panning.");
        view.HandleInput(new() { confirm = true }, data); Check(view.GetImageZoom("photo") == 1, "Image confirmation did not restore its authored fit.");
        view.ZoomImage("photo", 3); data.picture = new(200, 400, new byte[200 * 400 * 4]);
        view.BuildFrame(data); Check(view.GetImageZoom("photo") == 1, "A replacement image inherited stale drag or zoom state.");
        var invalid = UiDefinitionCopy.Copy(page); invalid.root.children[0].imageFit = UiImageFit.NineSlice;
        Reject(() => new UiDocumentInstance(invalid, static (_, _) => (0, 0)));
    }

    internal static void ActualVariantsAndInputOwnership(
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var one = new TextureAsset(400, 200, TextureColorSpace.Srgb, "png");
        var two = new TextureAsset(200, 400, TextureColorSpace.Srgb, "png");
        var chapter = FirstStoryTemplate.Create(codec).ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        var library = Library(one, two);
        var game = new NovelProjectAsset { gameId = "gallery-variants", chapters = [chapter], startChapter = chapter.chapterId, collections = library,
            screens = new[] { "title", "reading", "pause", "settings", "history", "credits", "gallery", "collectionDetail" }
                .Select(static role => new NovelScreenBinding { role = role, screen = ShellScreenTemplates.Create(role, null!, "Clear") }).ToArray() };
        using var session = new NovelSession(game); session.StartNew(); session.OpenPage("gallery");
        using var view = new NovelGameView(game, session, static (_, text) => (text.Length * 10, 32));
        Click(view, view.BuildFrame().items.Single(static item => item.text == "Three views").id);
        Check(session.page == "collectionDetail" && Artwork(view).image == one && Label(view) == "1 / 2", "The actual game shell did not hide a gated CG variant.");
        Click(view, "image-next"); Check(Artwork(view).image == one && view.BuildFrame().items.Any(static item => item.id == "variant-name" && item.text == "C"), "Variant navigation exposed the locked middle image.");
        session.collections.Unlock("ending");
        Check(Label(view) == "3 / 3" && view.BuildFrame().items.Any(static item => item.id == "variant-name" && item.text == "C"), "Unlocking a variant changed the currently selected stable image identity.");
        Click(view, "image-previous"); Check(Artwork(view).image == two && view.BuildFrame().items.Any(static item => item.id == "variant-name" && item.text == "B"), "A newly unlocked variant was not available through the real buttons.");
        Click(view, "zoom-in");
        Check(Artwork(view).imageBounds is { } bounds && bounds.height > Artwork(view).bounds.height,
            "An editable zoom button did not affect its authored image viewport.");
        library.entries[0].variants[1].image = one;
        Check(Artwork(view).image == two, "Running gallery metadata retained mutable authoring aliases.");
        long interaction = session.story!.interactionSequence; int revealed = session.story.visibleGraphemes;
        var art = Artwork(view);
        view.HandleInput(new() { x = art.bounds.x + 50, y = art.bounds.y + 50, pointerInside = true, pressed = true, held = true });
        session.OpenPage("reading"); view.BuildFrame();
        view.HandleInput(new() { x = 1000, y = 200, pointerInside = true, released = true });
        Check(session.story.interactionSequence == interaction && session.story.visibleGraphemes == revealed,
            "Releasing a retired gallery drag advanced the newly exposed narrative page.");

        var ids = library.entries.Select(static entry => entry.id).ToHashSet(StringComparer.Ordinal);
        var editable = CollectionEntryCopy.Copy(library.entries[0]);
        var missing = editable.variants[0].Copy(); missing.unlockEntryId = "undeclared";
        Reject(() => CollectionVariantEditService.Set(editable, missing, ids));
        Check(editable.variants[0].unlockEntryId.Length == 0, "Invalid variant metadata partially mutated its owner.");
        CollectionVariantEditService.Move(editable, "C", -1); Check(editable.variants[1].id == "C", "Variant reordering changed stable identities.");
        CollectionVariantEditService.Remove(editable, "C"); Check(editable.variants.Length == 2, "Variant deletion damaged its owning collection entry.");
        Check(LocalizationExtractionService.Extract(game).Any(static row => row.id.EndsWith("variant/B/title", StringComparison.Ordinal)), "CG variant names were absent from the real translation workflow.");
    }

    internal static void NativeReferences(
        string project,
        ModuleHost modules,
        TypeCatalog types,
        SerializationRegistry native,
        IdentityAllocator identities
    ) {
        string root = Path.Combine(project, "artifacts", "AcceptanceProjects", "Gallery-" + Guid.NewGuid().ToString("N"));
        string source = Path.Combine(root, "Assets"); Directory.CreateDirectory(source);
        File.WriteAllBytes(Path.Combine(source, "one.png"), UiPngImage.Encode(new(2, 1, [255, 0, 0, 255, 0, 255, 0, 255])));
        File.WriteAllBytes(Path.Combine(source, "two.png"), UiPngImage.Encode(new(1, 2, [0, 0, 255, 255, 255, 255, 0, 255])));
        using var logs = new LogRouter(); using var assets = new AssetPipeline(modules, types, native, identities, new DiagnosticHub(), logs,
            AssetPipelineOptions.Create(source, Path.Combine(root, "Library")) with { enableFileSystemWatcher = false });
        using var scope = AssetExecutionContext.EnterScope(assets);
        Check(assets.Import(AssetPath.Project("one.png")) && assets.Import(AssetPath.Project("two.png")), "Native fixture images did not import.");
        var one = Assets.Assets.Load<TextureAsset>(AssetPath.Project("one.png")); var two = Assets.Assets.Load<TextureAsset>(AssetPath.Project("two.png"));
        var sourceLibrary = Library(one, two);
        Check(EditorAssets.Save(AssetPath.Project("Gallery.ivncollection"), sourceLibrary), "Native CG source failed to save and import.");
        var imported = Assets.Assets.Load<CollectionAsset>(AssetPath.Project("Gallery.ivncollection"));
        var entry = imported.entries.Single(static entry => entry.id == "scene");
        Check(entry.variants.Length == 3 && entry.variants[1].unlockEntryId == "ending" && entry.variants[1].image?.identity.persistentId == two.identity.persistentId
            && entry.variants[1].image?.assetPath == two.assetPath, "Native variant decoding lost artwork identity, source ownership or unlock metadata.");
        var dependencies = assets.CaptureProperties(imported).dependencies.Select(static dependency => dependency.persistentId).ToHashSet();
        Check(dependencies.Contains(one.identity.persistentId) && dependencies.Contains(two.identity.persistentId), "Native asset dependency closure omitted variant artwork.");
        _ = new NovelCollectionService(imported);
        LocalizedResourcesContracts.NativeResourceReferences(project, source, assets, one, two);
        Console.WriteLine("Native CG acceptance project: " + root);
    }

    private static CollectionAsset Library(
        TextureAsset one,
        TextureAsset two
    ) => new() { entries = [new() { id = "scene", kind = CollectionKind.Cg, title = "Three views", initiallyUnlocked = true, image = one,
        variants = [new() { id = "A", title = "A", image = one }, new() { id = "B", title = "B", image = two, unlockEntryId = "ending" }, new() { id = "C", title = "C", image = one }] },
        new() { id = "ending", kind = CollectionKind.Ending, title = "Locked ending" }] };
    private static UiDrawItem Artwork(NovelGameView view) => view.BuildFrame().items.Single(static item => item.id == "artwork");
    private static string Label(NovelGameView view) => view.BuildFrame().items.Single(static item => item.id == "image-page").text;
    private static void Click(
        NovelGameView view,
        string id
    ) {
        var item = view.BuildFrame().items.Single(item => item.id == id);
        float x = item.bounds.x + item.bounds.width / 2, y = item.bounds.y + item.bounds.height / 2;
        view.HandleInput(new() { x = x, y = y, pointerInside = true, pressed = true }); view.HandleInput(new() { x = x, y = y, pointerInside = true, released = true });
    }
    private static void Reject(Action action)
    {
        try { action(); } catch (Exception error) when (error is InvalidOperationException or ArgumentException) { return; }
        throw new InvalidOperationException("Invalid gallery metadata or image layout was accepted.");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class BitmapSource : UiDataSource
    {
        internal UiBitmap picture = new(400, 200, new byte[400 * 200 * 4]);
        public override UiBitmap? ReadBitmap(string path, UiDataRow? row) => path == "picture" ? picture : null;
        public override bool TryRead(string path, out Model.Values.NovelValue value) { value = Model.Values.NovelValue.Boolean(false); return false; }
        public override bool TryWrite(string path, Model.Values.NovelValue value) => false;
    }
}
