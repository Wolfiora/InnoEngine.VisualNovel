using InnoEngine.Assets;
using InnoEngine.Text;
using Inno.VisualNovel.UI.Text;

namespace Inno.VisualNovel.Authoring.LocalizationEditing;

/// <summary>Describes one static text in its actual theme, control and language font context.</summary>
/// <param name="textId">The stable localization or control identity.</param>
/// <param name="location">The human-readable theme/page/control or chapter location.</param>
/// <param name="text">The resolved language text before variable interpolation.</param>
/// <param name="rich">Whether authored markup is parsed.</param>
/// <param name="interpolated">Whether the narrative template grammar supplies dynamic variable values.</param>
/// <param name="font">The resolved default font, preserving unassigned state.</param>
/// <param name="size">The resolved control font size.</param>
/// <param name="resources">The resolved named fonts, images and fallback chain.</param>
/// <param name="assetPath">The native story or screen source to open.</param>
/// <param name="nodeId">The authored narrative node, empty for page text.</param>
/// <param name="elementId">The authored page element to select for a UI source.</param>
public sealed record GlyphCoverageDocument(string textId, string location, string text, bool rich, bool interpolated, FontAsset? font, float size,
    UiRichTextResources resources, AssetPath assetPath, string nodeId = "", string elementId = "");
