using System;
using System.IO;
using System.Linq;
using Inno.Assets;
using Inno.Assets.Pipeline;
using Inno.Text;
using Inno.UI;
using Inno.VisualNovel.Integration.Components.Interface;
using Inno.VisualNovel.Integration.Interface.Engine;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class NativeUiFontFixture
{
    internal static void Prepare(string assetsRoot)
    {
        string fonts = Path.Combine(assetsRoot, "BuiltIn", "Fonts");
        File.Copy(Path.Combine(fonts, "NotoSansCJKsc-Regular.otf"), Path.Combine(fonts, "Accent.otf"));
        string source = File.ReadAllText(Path.Combine(assetsRoot, "BuiltIn", "SceneUi.rml"));
        source = source.Replace("</style>", "@font-face { font-family: NovelAccent; src: url(Fonts/Accent.otf); font-weight: 400; }\n.vn-font-1 { font-family: NovelAccent; }\n</style>", StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(assetsRoot, "BuiltIn", "Accent.rml"), source);
    }

    internal static void Verify(
        NovelUiRootComponent root,
        NovelUiTextComponent label,
        NovelEngineUiSurface surface,
        AssetPipeline assets
    ) {
        var primary = assets.Load<FontAsset>(AssetPath.Project("BuiltIn/Fonts/NotoSansCJKsc-Regular.otf"));
        var accent = assets.Load<FontAsset>(AssetPath.Project("BuiltIn/Fonts/Accent.otf"));
        root.font = primary; label.font = accent;
        bool rejected = false;
        try { _ = surface.Capture(); } catch (InvalidOperationException) { rejected = true; }
        if (!rejected) throw new InvalidOperationException("A font absent from the engine document was silently ignored.");
        label.font = null;
        if (!surface.Capture().commands.Any(static command => command.texture.isValid))
            throw new InvalidOperationException("An invalid font override replaced the last usable document.");
        root.documentTemplate = assets.Load<UiDocumentAsset>(AssetPath.Project("BuiltIn/Accent.rml"));
        label.font = accent;
        if (root.documentTemplate.fonts.Count != 2 || !surface.Capture().commands.Any(static command => command.texture.isValid)
            || !ReferenceEquals(root.font, primary))
            throw new InvalidOperationException("A per-control font declared in the engine template failed to produce glyphs or changed the canvas default.");
        label.font = null;
        if (!surface.Capture().commands.Any(static command => command.texture.isValid))
            throw new InvalidOperationException("Removing a font override failed to restore the imported canvas font.");
    }
}
