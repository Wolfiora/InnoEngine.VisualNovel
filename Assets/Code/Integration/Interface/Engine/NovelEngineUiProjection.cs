using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using InnoEngine.Scene;
using Inno.VisualNovel.Integration.Components.Interface;
using Inno.VisualNovel.UI.Binding;
using Inno.VisualNovel.Model.Interface;

namespace Inno.VisualNovel.Integration.Interface.Engine;

/// <summary>Builds disposable document inputs from native objects; the engine owns layout, focus and input.</summary>
internal sealed class NovelEngineUiProjection
{
    internal readonly Dictionary<string, GameObject> targets = new(StringComparer.Ordinal);
    internal readonly Dictionary<string, string> text = new(StringComparer.Ordinal);
    internal readonly Dictionary<string, string> richText = new(StringComparer.Ordinal);
    internal readonly Dictionary<string, string> styles = new(StringComparer.Ordinal);
    internal readonly Dictionary<string, bool> disabled = new(StringComparer.Ordinal);
    internal string structure = string.Empty;
    internal string focusKey = string.Empty;
    internal string focusTarget = string.Empty;
    internal bool hasInitialFocus => focusTarget.Length > 0;
    internal bool modal;
    internal bool hasControls => disabled.Any(pair => !pair.Value && targets.TryGetValue(pair.Key, out var owner)
        && (owner.TryGetComponent<NovelUiButtonComponent>(out _) || owner.TryGetComponent<NovelUiToggleComponent>(out _)));
    internal string markup => focusTarget.Length == 0 ? structure
        : structure.Replace(" id=\"" + focusTarget + "\"", " id=\"" + focusTarget + "\" autofocus", StringComparison.Ordinal);
    internal bool NeedsContent(NovelEngineUiProjection? previous) => previous is null || structure != previous.structure || focusKey != previous.focusKey;

    internal static NovelEngineUiProjection Capture(
        NovelUiRootComponent root,
        UiDataSource? source,
        NovelEngineUiFocusMemory? focus = null
    ) {
        if (root.isDestroyed || !root.gameObject.activeInHierarchy)
            throw new InvalidOperationException("The scene UI root is not active.");
        var result = new NovelEngineUiProjection();
        var body = new StringBuilder();
        int count = 0;
        GameObject? modal = FindModal(root.gameObject, 0);
        result.modal = modal is not null;
        GameObject? pageOwner = modal ?? FindPage(root.gameObject, 0);
        var pageComponent = pageOwner?.GetComponent<NovelPageComponent>();
        GameObject? initialFocus = pageComponent?.initialFocus;
        if (initialFocus is not null)
        {
            if (initialFocus.isDestroyed || !initialFocus.isRuntimeValid || !ReferenceEquals(initialFocus.scene, root.gameObject.scene))
                throw new InvalidOperationException("The page's initial focus object is missing or belongs to another scene.");
            var ancestor = initialFocus.transform.parent;
            while (ancestor is not null && !ReferenceEquals(ancestor.gameObject, pageOwner))
            {
                if (ancestor.gameObject.TryGetComponent<NovelUiRootComponent>(out _)) break;
                ancestor = ancestor.parent;
            }
            if (ancestor is null || !ReferenceEquals(ancestor.gameObject, pageOwner)
                || !initialFocus.TryGetComponent<NovelUiButtonComponent>(out _) && !initialFocus.TryGetComponent<NovelUiToggleComponent>(out _))
                throw new InvalidOperationException("Initial focus must reference a button or toggle inside its native page and canvas.");
        }
        foreach (var child in root.transform.children) Append(child.gameObject, UiContainerLayout.Absolute, 0, true);
        result.structure = body.ToString();
        result.focusKey = pageOwner?.identity.persistentId.ToString("N") + "/" + initialFocus?.identity.persistentId.ToString("N");
        var remembered = pageComponent is null ? null : focus?.Read(pageComponent);
        string FocusId(GameObject? target)
        {
            if (target is null || target.isDestroyed || !target.isRuntimeValid) return string.Empty;
            string id = "vn-" + target.identity.persistentId.ToString("N");
            return result.disabled.TryGetValue(id, out bool disabled) && !disabled ? id : string.Empty;
        }
        result.focusTarget = FocusId(remembered);
        if (result.focusTarget.Length == 0) result.focusTarget = FocusId(initialFocus);
        result.focusKey += "/" + (FocusId(initialFocus).Length > 0);
        return result;

        void Append(
            GameObject owner,
            UiContainerLayout parentLayout,
            int depth,
            bool ancestorsEnabled
        ) {
            if (!owner.activeInHierarchy || owner.TryGetComponent<NovelUiRootComponent>(out _)) return;
            if (depth > 64 || ++count > 10000) throw new InvalidOperationException("Scene UI exceeds its supported document size.");
            owner.TryGetComponent<NovelUiBindingComponent>(out var binding);
            var rowData = NovelUiRowComponent.Find(owner);
            if (!NovelUiActionReader.Condition(source, binding?.visible ?? string.Empty, rowData)) return;
            // No value is invented for a Change event: the current service does not expose its payload.
            if (owner.TryGetComponent<NovelUiSliderComponent>(out _))
                throw new NotSupportedException("Engine UI Change events do not expose slider values. Slider writeback requires that public capability.");
            if (owner.TryGetComponent<NovelUiImageComponent>(out _))
                throw new NotSupportedException("Scene image registration into the engine UI document is not implemented yet.");
            string id = "vn-" + owner.identity.persistentId.ToString("N");
            if (!result.targets.TryAdd(id, owner)) throw new InvalidOperationException("Duplicate native UI identity.");
            bool button = owner.TryGetComponent<NovelUiButtonComponent>(out var control);
            owner.TryGetComponent<NovelUiToggleComponent>(out var toggle);
            if (button && toggle is not null) throw new InvalidOperationException("A UI object cannot be both a button and a toggle.");
            string tag = button || toggle is not null ? "button" : "div";
            bool enabled = ancestorsEnabled && (control?.interactable ?? toggle?.interactable ?? true);
            enabled &= NovelUiActionReader.Condition(source, binding?.interactable ?? string.Empty, rowData);
            if (owner.TryGetComponent<NovelUiListNavigationComponent>(out var navigation) && navigation is not null)
                enabled &= navigation.canNavigate;
            if (NovelUiActionReader.Read(owner, source) is { } action && source is not null) enabled &= source.CanInvoke(action);
            bool acceptsInput = enabled && InsideModal(owner);
            result.disabled[id] = !acceptsInput;
            result.styles[id] = NovelEngineUiStyle.Build(owner, parentLayout, acceptsInput);
            body.Append('<').Append(tag).Append(" id=\"").Append(id).Append('"');
            body.Append('>');
            if (toggle is not null)
            {
                result.text[id + "-check"] = NovelEngineUiToggle.Read(toggle, source) ? "[x] " : "[ ] ";
                result.targets.Add(id + "-check", owner);
                body.Append("<span id=\"").Append(id).Append("-check\" style=\"pointer-events:none;\"></span>");
            }
            if (owner.TryGetComponent<NovelUiTextComponent>(out var label) && label is not null)
            {
                string fontClass = (label.font ?? root.font) is { } font
                    ? NovelEngineUiText.FontClass(font, root.documentTemplate) : string.Empty;
                string value = source?.Localize(label.textId, label.text) ?? label.text;
                if (binding is { text.Length: > 0 } && source is not null)
                {
                    if (!source.TryRead(binding.text, rowData, out var data)) throw new InvalidOperationException("Missing UI text binding: " + binding.text);
                    value = data.ToString();
                }
                var rich = binding is { text.Length: > 0 } ? source?.ReadRichText(binding.text, rowData) : null;
                if (rich is not null || label.richText || fontClass.Length > 0)
                {
                    string fragment = rich is not null || label.richText
                        ? NovelEngineUiText.Fragment(rich ?? new(Inno.VisualNovel.Model.Text.NovelTextMarkup.Parse(value)), root.documentTemplate)
                        : NovelEngineUiText.Escape(value);
                    // Imported template classes retain the engine's document-version font ownership.
                    result.richText[id + "-text"] = fontClass.Length == 0 ? fragment
                        : "<span class=\"" + fontClass + "\">" + fragment + "</span>";
                }
                else result.text[id + "-text"] = value;
                result.targets.Add(id + "-text", owner);
                // Typography fragments have no scene identity; input belongs to the real control that owns them.
                body.Append("<span id=\"").Append(id).Append("-text\" style=\"pointer-events:none;\"></span>");
            }
            var flow = owner.TryGetComponent<NovelUiContainerComponent>(out var container) && container is not null ? container.layout : UiContainerLayout.Absolute;
            if (flow == UiContainerLayout.Grid)
            {
                if (container!.columns is < 1 or > 64) throw new InvalidOperationException("Grid column count must be between 1 and 64.");
                var children = owner.transform.children.Select(static child => child.gameObject).Where(child =>
                {
                    child.TryGetComponent<NovelUiBindingComponent>(out var childBinding);
                    return child.activeInHierarchy && !child.TryGetComponent<NovelUiRootComponent>(out _)
                        && NovelUiActionReader.Condition(source, childBinding?.visible ?? string.Empty, NovelUiRowComponent.Find(child));
                }).ToArray();
                for (int row = 0; row < children.Length; row += container.columns)
                {
                    string rowId = id + "-row-" + row.ToString(CultureInfo.InvariantCulture);
                    result.targets.Add(rowId, owner);
                    body.Append("<div id=\"").Append(rowId).Append("\" style=\"display:flex;flex-direction:row;flex-shrink:0;width:100%;gap:")
                        .Append(container.gap.ToString("R", CultureInfo.InvariantCulture)).Append("px;\">");
                    for (int column = 0; column < container.columns; column++)
                    {
                        string cellId = rowId + "-" + column.ToString(CultureInfo.InvariantCulture);
                        result.targets.Add(cellId, row + column < children.Length ? children[row + column] : owner);
                        body.Append("<div id=\"").Append(cellId).Append("\" style=\"display:flex;flex-direction:column;position:relative;flex:1 1 0px;min-width:0;\">");
                        if (row + column < children.Length) Append(children[row + column], UiContainerLayout.Grid, depth + 1, enabled);
                        body.Append("</div>");
                    }
                    body.Append("</div>");
                }
            }
            else foreach (var child in owner.transform.children) Append(child.gameObject, flow, depth + 1, enabled);
            body.Append("</").Append(tag).Append('>');
        }
        bool InsideModal(GameObject owner)
        {
            if (modal is null) return true;
            for (var item = owner.transform; item is not null; item = item.parent)
                if (ReferenceEquals(item.gameObject, modal)) return true;
            return false;
        }
        GameObject? FindModal(
            GameObject owner,
            int depth
        ) {
            if (depth > 64) throw new InvalidOperationException("Native UI nesting exceeds 64 levels.");
            owner.TryGetComponent<NovelUiBindingComponent>(out var binding);
            if (!owner.activeInHierarchy || !NovelUiActionReader.Condition(source, binding?.visible ?? string.Empty, NovelUiRowComponent.Find(owner))) return null;
            GameObject? found = owner.TryGetComponent<NovelPageComponent>(out var page) && page is { modal: true } ? owner : null;
            foreach (var child in owner.transform.children)
                if (!child.gameObject.TryGetComponent<NovelUiRootComponent>(out _) && FindModal(child.gameObject, depth + 1) is { } last) found = last;
            return found;
        }
        GameObject? FindPage(
            GameObject owner,
            int depth
        ) {
            if (depth > 64) throw new InvalidOperationException("Native UI nesting exceeds 64 levels.");
            owner.TryGetComponent<NovelUiBindingComponent>(out var binding);
            if (!owner.activeInHierarchy || !NovelUiActionReader.Condition(source, binding?.visible ?? string.Empty, NovelUiRowComponent.Find(owner))) return null;
            GameObject? found = owner.TryGetComponent<NovelPageComponent>(out _) ? owner : null;
            foreach (var child in owner.transform.children)
                if (!child.gameObject.TryGetComponent<NovelUiRootComponent>(out _) && FindPage(child.gameObject, depth + 1) is { } last) found = last;
            return found;
        }
    }
}
