using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Inno.VisualNovel.Model.Text;
using Inno.VisualNovel.UI.Text;
using InnoEngine.UI;
using InnoEngine.Text;

namespace Inno.VisualNovel.Integration.Interface.Engine;

/// <summary>Projects VN text semantics into engine-owned text layout, preserving unrevealed layout content.</summary>
internal static class NovelEngineUiText
{
    internal static string FontClass(
        FontAsset font,
        UiDocumentAsset? template
    ) {
        if (font.isMissing || font.identity.persistentId == Guid.Empty)
            throw new InvalidOperationException("The UI font is missing or has not been imported.");
        string[] families = template?.fonts.Where(face => face.assetId == font.identity.persistentId)
            .Select(static face => face.family).Distinct(StringComparer.Ordinal).ToArray() ?? [];
        if (families.Length != 1)
            throw new InvalidOperationException("Declare the selected font under exactly one family in this canvas's engine UI template.");
        return FamilyClass(families[0], template!);
    }

    private static string FamilyClass(
        string family,
        UiDocumentAsset template
    ) {
        string[] declared = template.fonts.Select(static face => face.family).Distinct(StringComparer.Ordinal).ToArray();
        int index = Array.IndexOf(declared, family);
        if (index < 0) throw new InvalidOperationException("Declare the rich-text font family in the engine UI template: " + family);
        string selector = "vn-font-" + index.ToString(CultureInfo.InvariantCulture);
        if (template.source is null || !template.source.text.Contains("." + selector, StringComparison.Ordinal))
            throw new InvalidOperationException("The engine UI template must declare the font style class: " + selector);
        return selector;
    }

    internal static string Fragment(
        UiRichText text,
        UiDocumentAsset? template
    ) {
        var document = text.document;
        int count = Math.Clamp(text.visibleGraphemes, 0, document.elements.Count);
        int visible = count == document.elements.Count ? document.text.Length : document.elements[count];
        var result = new StringBuilder();
        foreach (var run in document.runs)
        {
            if (run.icon.Length > 0)
                throw new NotSupportedException("Inline images require an engine UI imported-image registration contract.");
            var style = run.style;
            var css = new StringBuilder("white-space:pre-wrap;");
            if (style.bold) css.Append("font-weight:700;");
            if (style.italic) css.Append("font-style:italic;");
            if (style.size > 0) css.Append("font-size:").Append(N(style.size)).Append("px;");
            if (style.color is { } color) css.Append("color:#").Append(color.ToString("X8", CultureInfo.InvariantCulture)).Append(';');
            string fontClass = style.font.Length == 0 ? string.Empty : FamilyClass(style.font,
                template ?? throw new InvalidOperationException("Rich-text fonts require an imported engine template."));
            if (style.outline > 0 || style.shadow != 0)
            {
                css.Append("font-effect:");
                if (style.outline > 0) css.Append("outline(").Append(N(style.outline)).Append("px #").Append(style.outlineColor.ToString("X8", CultureInfo.InvariantCulture)).Append(')');
                if (style.outline > 0 && style.shadow != 0) css.Append(' ');
                if (style.shadow != 0) css.Append("shadow(").Append(N(style.shadow)).Append("px ").Append(N(style.shadow)).Append("px #").Append(style.shadowColor.ToString("X8", CultureInfo.InvariantCulture)).Append(')');
                css.Append(';');
            }
            result.Append("<span class=\"").Append(fontClass).Append("\" style=\"").Append(Escape(css.ToString())).Append("\">");
            if (run.ruby.Length > 0)
            {
                result.Append("<span style=\"display:inline-block;text-align:center;\"><span style=\"display:block;font-size:50%;line-height:1;");
                if (visible <= run.start) result.Append("visibility:hidden;");
                result.Append("\">").Append(Escape(run.ruby)).Append("</span><span style=\"display:block;\">");
            }
            int shown = Math.Clamp(visible - run.start, 0, run.length);
            result.Append(Escape(document.text.Substring(run.start, shown)));
            if (shown < run.length)
                result.Append("<span style=\"visibility:hidden;\">").Append(Escape(document.text.Substring(run.start + shown, run.length - shown))).Append("</span>");
            if (run.ruby.Length > 0) result.Append("</span></span>");
            result.Append("</span>");
        }
        return result.ToString();
    }

    internal static string Escape(string text) => text.Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal).Replace("'", "&#39;", StringComparison.Ordinal);
    private static string N(float number) => number.ToString("R", CultureInfo.InvariantCulture);
}
