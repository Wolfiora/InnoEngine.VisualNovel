using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.UI.Documents;

/// <summary>Creates session-owned declaration copies while borrowing imported resource identities.</summary>
public static class UiDefinitionCopy
{
    /// <summary>Copies the full definition tree without mutable source aliases.</summary>
    /// <param name="screen">The source declaration.</param>
    /// <returns>A detached page shell with independent layout and style values.</returns>
    public static ScreenAsset Copy(ScreenAsset screen) => new()
    {
        title = screen.title, width = screen.width, height = screen.height, modal = screen.modal, initialFocus = screen.initialFocus,
        stateVariables = screen.stateVariables.Select(VariableValueRules.Copy).ToArray(),
        font = screen.font, textFonts = screen.textFonts.Select(static item => new UiTextFont { key = item.key, font = item.font }).ToArray(),
        textIcons = screen.textIcons.Select(static item => new UiTextIcon { key = item.key, image = item.image }).ToArray(),
        root = Element(screen.root, new HashSet<UiElementDefinition>(ReferenceEqualityComparer.Instance), 0)
    };

    private static UiElementDefinition Element(
        UiElementDefinition item,
        HashSet<UiElementDefinition> seen,
        int depth
    ) {
        if (depth > 64 || seen.Count >= 10000 || !seen.Add(item)) throw new ArgumentException("UI declarations must be bounded trees with unique ownership.");
        UiStyleDefinition s = item.style;
        return new()
        {
            id = item.id, kind = item.kind, text = item.text, textId = item.textId, richText = item.richText, font = item.font, image = item.image, imageBinding = item.imageBinding, imageFit = item.imageFit,
            tooltip = item.tooltip, tooltipTextId = item.tooltipTextId, tooltipBinding = item.tooltipBinding,
            imageSlice = item.imageSlice.Copy(),
            animation = item.animation.Copy(),
            styleClass = item.styleClass, styleOverrides = item.styleOverrides,
            geometryBinding = new() { x = item.geometryBinding.x, y = item.geometryBinding.y, width = item.geometryBinding.width, height = item.geometryBinding.height, normalized = item.geometryBinding.normalized },
            binding = item.binding, repeatBinding = item.repeatBinding, visibleBinding = item.visibleBinding, enabledBinding = item.enabledBinding,
            followEndBinding = item.followEndBinding,
            action = item.action, argument = item.argument, argumentBinding = item.argumentBinding, minimum = item.minimum, maximum = item.maximum,
            step = item.step,
            maxLength = item.maxLength, interactable = item.interactable, blocksPointer = item.blocksPointer, behindStage = item.behindStage,
            optionsBinding = item.optionsBinding,
            options = item.options.Select(static option => new UiOptionDefinition { id = option.id, text = option.text, textId = option.textId, value = option.value.Copy(), enabled = option.enabled }).ToArray(),
            layout = item.layout.Copy(),
            style = s.Copy(),
            children = item.children.Select(child => Element(child, seen, depth + 1)).ToArray()
        };
    }
}
