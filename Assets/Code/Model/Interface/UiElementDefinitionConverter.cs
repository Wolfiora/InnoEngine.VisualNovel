using InnoEngine.Rendering;
using InnoEngine.Serialization;
using InnoEngine.Text;
using Inno.VisualNovel.Model.Interface.Themes;

namespace Inno.VisualNovel.Model.Interface;

/// <summary>Preserves nullable native font and image references in authored UI trees.</summary>
public sealed class UiElementDefinitionConverter : SerializationConverter<UiElementDefinition>
{
    /// <inheritdoc />
    public override void Write(
        SerializationWriter writer,
        UiElementDefinition value
    ) {
        writer.Write("id", value.id); writer.Write("kind", value.kind);
        writer.Write("styleClass", value.styleClass); writer.Write("styleOverrides", value.styleOverrides);
        writer.Write("layout", value.layout); writer.Write("style", value.style); writer.Write("children", value.children);
        writer.Write("geometryBinding", value.geometryBinding);
        writer.Write("animation", value.animation);
        writer.Write("text", value.text); writer.Write("binding", value.binding); writer.Write("repeatBinding", value.repeatBinding);
        writer.Write("followEndBinding", value.followEndBinding);
        writer.Write("textId", value.textId);
        writer.Write("tooltip", value.tooltip); writer.Write("tooltipTextId", value.tooltipTextId); writer.Write("tooltipBinding", value.tooltipBinding);
        writer.Write("richText", value.richText);
        writer.Write("imageBinding", value.imageBinding);
        writer.Write("imageFit", value.imageFit);
        writer.Write("imageSlice", value.imageSlice);
        writer.Write("options", value.options); writer.Write("optionsBinding", value.optionsBinding);
        writer.Write("visibleBinding", value.visibleBinding); writer.Write("enabledBinding", value.enabledBinding);
        writer.Write("action", value.action); writer.Write("argument", value.argument); writer.Write("argumentBinding", value.argumentBinding);
        writer.Write<FontAsset?>("font", value.font); writer.Write<TextureAsset?>("image", value.image);
        writer.Write("minimum", value.minimum); writer.Write("maximum", value.maximum); writer.Write("maxLength", value.maxLength);
        writer.Write("step", value.step);
        writer.Write("interactable", value.interactable); writer.Write("blocksPointer", value.blocksPointer);
        writer.Write("behindStage", value.behindStage);
    }
    /// <inheritdoc />
    public override UiElementDefinition Read(SerializationReader reader) => new()
    {
        id = reader.Read<string>("id"), kind = reader.Read<UiElementKind>("kind"), layout = reader.Read<UiLayoutDefinition>("layout"),
        styleClass = reader.Read<string>("styleClass"), styleOverrides = reader.Read<UiStyleProperty>("styleOverrides"),
        geometryBinding = reader.Read<UiGeometryBinding>("geometryBinding"),
        animation = reader.Read<Animation.UiAnimationDefinition>("animation"),
        style = reader.Read<UiStyleDefinition>("style"), children = reader.Read<UiElementDefinition[]>("children"), text = reader.Read<string>("text"),
        binding = reader.Read<string>("binding"), repeatBinding = reader.Read<string>("repeatBinding"), visibleBinding = reader.Read<string>("visibleBinding"),
        followEndBinding = reader.Read<string>("followEndBinding"),
        textId = reader.Read<string>("textId"),
        tooltip = reader.Read<string>("tooltip"), tooltipTextId = reader.Read<string>("tooltipTextId"), tooltipBinding = reader.Read<string>("tooltipBinding"),
        richText = reader.Read<bool>("richText"),
        imageBinding = reader.Read<string>("imageBinding"),
        imageFit = reader.Read<UiImageFit>("imageFit"),
        imageSlice = reader.Read<UiImageSlice>("imageSlice"),
        options = reader.Read<UiOptionDefinition[]>("options"), optionsBinding = reader.Read<string>("optionsBinding"),
        enabledBinding = reader.Read<string>("enabledBinding"), action = reader.Read<string>("action"), argument = reader.Read<string>("argument"),
        argumentBinding = reader.Read<string>("argumentBinding"), font = reader.Read<FontAsset?>("font"), image = reader.Read<TextureAsset?>("image"),
        minimum = reader.Read<double>("minimum"), maximum = reader.Read<double>("maximum"), maxLength = reader.Read<int>("maxLength"),
        step = reader.Read<double>("step"),
        interactable = reader.Read<bool>("interactable"), blocksPointer = reader.Read<bool>("blocksPointer"), behindStage = reader.Read<bool>("behindStage")
    };
}
