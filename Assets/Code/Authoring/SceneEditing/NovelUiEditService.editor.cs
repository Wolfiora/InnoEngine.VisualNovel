using System;
using InnoEngine.Assets;
using InnoEngine.Scene;
using InnoEngine.Text;
using InnoEngine.UI;
using InnoEditor.Inspection;
using InnoEditor.Scene;
using Inno.VisualNovel.Integration.Components.Interface;
using Inno.VisualNovel.Model.Interface;

namespace Inno.VisualNovel.Authoring.SceneEditing;

/// <summary>Creates editable UI objects and assigns engine document resources through native authoring services.</summary>
public sealed class NovelUiEditService
{
    private readonly SceneEdits m_scenes;
    private readonly IInspectionPropertyEditService m_properties;
    /// <summary>Creates current-generation native UI edits.</summary>
    /// <param name="scenes">Native object and component edits.</param>
    /// <param name="properties">Native property writes and History ownership.</param>
    public NovelUiEditService(
        SceneEdits scenes,
        IInspectionPropertyEditService properties
    ) { m_scenes = scenes; m_properties = properties; }

    /// <summary>Creates a canvas with the imported engine UI template and its default multilingual font.</summary>
    /// <param name="scene">The actual destination scene.</param>
    /// <returns>The new canvas component shown in Hierarchy and Inspector.</returns>
    public NovelUiRootComponent CreateCanvas(GameScene scene)
    {
        var template = Assets.Load<UiDocumentAsset>(Assets.LocalPath("BuiltIn/SceneUi.rml"));
        var font = Assets.Load<FontAsset>(Assets.LocalPath("BuiltIn/Fonts/NotoSansCJKsc-Regular.otf"));
        if (template.isMissing || template.source is null || template.fonts.Count == 0 || font.isMissing)
            throw new InvalidOperationException("Import the default engine UI template and font before creating a canvas.");
        var owner = m_scenes.CreateGameObject(scene);
        m_scenes.RenameGameObject(owner, "UI Canvas");
        var root = (NovelUiRootComponent)m_scenes.AddComponent(owner, typeof(NovelUiRootComponent));
        Change(root, nameof(root.documentTemplate), () => root.documentTemplate = template);
        Change(root, nameof(root.font), () => root.font = font);
        return root;
    }

    /// <summary>Creates an editable page under an existing native canvas.</summary>
    /// <param name="root">The parent canvas.</param>
    /// <returns>The real page component, with layout delegated to engine UI.</returns>
    public NovelPageComponent CreatePage(NovelUiRootComponent root)
    {
        var owner = Create(root.gameObject, "Page");
        var layout = (NovelUiLayoutComponent)m_scenes.AddComponent(owner, typeof(NovelUiLayoutComponent));
        Change(layout, nameof(layout.widthMode), () => layout.widthMode = UiSizeMode.Fill);
        Change(layout, nameof(layout.heightMode), () => layout.heightMode = UiSizeMode.Fill);
        return (NovelPageComponent)m_scenes.AddComponent(owner, typeof(NovelPageComponent));
    }

    /// <summary>Creates a button whose text, appearance and action remain ordinary editable components.</summary>
    /// <param name="parent">The real canvas, page or container.</param>
    /// <returns>The newly created button control.</returns>
    public NovelUiButtonComponent CreateButton(GameObject parent)
    {
        var owner = Create(parent, "Button");
        m_scenes.AddComponent(owner, typeof(NovelUiLayoutComponent));
        var panel = (NovelUiPanelComponent)m_scenes.AddComponent(owner, typeof(NovelUiPanelComponent));
        Change(panel, nameof(panel.color), () => panel.color = 0x283D5FFF);
        m_scenes.AddComponent(owner, typeof(NovelUiControlStyleComponent));
        var text = (NovelUiTextComponent)m_scenes.AddComponent(owner, typeof(NovelUiTextComponent));
        Change(text, nameof(text.text), () => text.text = "Continue");
        Change(text, nameof(text.alignment), () => text.alignment = UiTextAlignment.Center);
        m_scenes.AddComponent(owner, typeof(NovelUiActionComponent));
        return (NovelUiButtonComponent)m_scenes.AddComponent(owner, typeof(NovelUiButtonComponent));
    }

    /// <summary>Creates a navigation button with an explicit native page reference instead of a story action.</summary>
    /// <param name="parent">The actual page or layout container.</param>
    /// <returns>The editable navigation component; assign its destination before using it.</returns>
    public NovelUiPageLinkComponent CreatePageLink(GameObject parent)
    {
        var button = CreateButton(parent);
        var action = button.gameObject.GetComponent<NovelUiActionComponent>();
        m_scenes.RemoveComponent(action, "Use native page navigation");
        m_scenes.RenameGameObject(button.gameObject, "Page Link");
        return (NovelUiPageLinkComponent)m_scenes.AddComponent(button.gameObject, typeof(NovelUiPageLinkComponent));
    }

    /// <summary>Creates a paging button with an explicit scene list reference instead of a story action.</summary>
    /// <param name="parent">The actual page or layout container.</param>
    /// <returns>The navigation component; assign its list and movement in Inspector.</returns>
    public NovelUiListNavigationComponent CreateListNavigation(GameObject parent)
    {
        var button = CreateButton(parent);
        m_scenes.RemoveComponent(button.gameObject.GetComponent<NovelUiActionComponent>(), "Use native list navigation");
        m_scenes.RenameGameObject(button.gameObject, "List Navigation");
        var text = button.gameObject.GetComponent<NovelUiTextComponent>();
        Change(text, nameof(text.text), () => text.text = "Next page");
        return (NovelUiListNavigationComponent)m_scenes.AddComponent(button.gameObject, typeof(NovelUiListNavigationComponent));
    }

    /// <summary>Creates a plain text object under an existing native UI parent.</summary>
    /// <param name="parent">The real canvas, page or container.</param>
    /// <returns>The independently editable text component.</returns>
    public NovelUiTextComponent CreateText(GameObject parent)
    {
        var owner = Create(parent, "Text");
        m_scenes.AddComponent(owner, typeof(NovelUiLayoutComponent));
        return (NovelUiTextComponent)m_scenes.AddComponent(owner, typeof(NovelUiTextComponent));
    }

    /// <summary>Creates a Boolean toggle with a native owner and engine-managed keyboard activation.</summary>
    /// <param name="parent">The real canvas, page or container.</param>
    /// <returns>The toggle; an optional binding component connects its value to a Boolean data field.</returns>
    public NovelUiToggleComponent CreateToggle(GameObject parent)
    {
        var owner = Create(parent, "Toggle");
        m_scenes.AddComponent(owner, typeof(NovelUiLayoutComponent));
        m_scenes.AddComponent(owner, typeof(NovelUiPanelComponent));
        m_scenes.AddComponent(owner, typeof(NovelUiControlStyleComponent));
        var text = (NovelUiTextComponent)m_scenes.AddComponent(owner, typeof(NovelUiTextComponent));
        Change(text, nameof(text.text), () => text.text = "Option");
        return (NovelUiToggleComponent)m_scenes.AddComponent(owner, typeof(NovelUiToggleComponent));
    }

    /// <summary>Creates an automatic layout container whose children remain normal scene objects.</summary>
    /// <param name="parent">The native parent.</param>
    /// <param name="layout">The engine-managed arrangement.</param>
    /// <returns>The editable container component.</returns>
    public NovelUiContainerComponent CreateContainer(
        GameObject parent,
        UiContainerLayout layout
    ) {
        if (!Enum.IsDefined(layout)) throw new ArgumentOutOfRangeException(nameof(layout));
        var owner = Create(parent, layout + " Layout");
        var size = (NovelUiLayoutComponent)m_scenes.AddComponent(owner, typeof(NovelUiLayoutComponent));
        Change(size, nameof(size.size), () => size.size = new(600, 320));
        var container = (NovelUiContainerComponent)m_scenes.AddComponent(owner, typeof(NovelUiContainerComponent));
        Change(container, nameof(container.layout), () => container.layout = layout);
        return container;
    }

    /// <summary>Creates a real vertical collection container; the author assigns its native item Prefab in Inspector.</summary>
    /// <param name="parent">The native UI parent.</param>
    /// <returns>The list whose explicit collection and Prefab define runtime rows.</returns>
    public NovelUiListComponent CreateList(GameObject parent)
    {
        var container = CreateContainer(parent, UiContainerLayout.Vertical);
        m_scenes.RenameGameObject(container.gameObject, "Collection List");
        m_scenes.AddComponent(container.gameObject, typeof(NovelUiScrollComponent));
        var list = (NovelUiListComponent)m_scenes.AddComponent(container.gameObject, typeof(NovelUiListComponent));
        Change(list, nameof(list.collection), () => list.collection = "choices");
        return list;
    }

    private GameObject Create(
        GameObject parent,
        string name
    ) {
        if (parent.isDestroyed) throw new InvalidOperationException("The UI parent no longer exists.");
        var owner = m_scenes.CreateGameObject(parent.scene, parent.transform); m_scenes.RenameGameObject(owner, name); return owner;
    }
    private void Change(
        object owner,
        string property,
        Action edit
    )
        => m_properties.ChangeProperty(owner, property, edit, "Configure scene UI", null, null);
}
