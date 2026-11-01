using Inno.VisualNovel.Model.Interface;

namespace Inno.VisualNovel.UI.Text;

/// <summary>Measures the same full rich layout used by rendering within a resolved available width.</summary>
/// <param name="element">The authored control with its inherited font.</param>
/// <param name="content">The immutable full text and visible range.</param>
/// <param name="resources">Page-owned text resources.</param>
/// <param name="width">Available inner text width.</param>
/// <param name="scale">Accessibility scale for absolute rich sizes.</param>
/// <returns>Full text bounds and the revealed vertical extent before control padding.</returns>
public delegate (float width, float height, float visibleHeight) UiTextMeasurement(UiElementDefinition element, UiRichText content, UiRichTextResources resources, float width, float scale);
