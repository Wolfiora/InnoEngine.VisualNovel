using System.Collections.Generic;
using InnoEngine.Text;
using InnoEngine.Rendering;
using Inno.VisualNovel.Model.Interface;

namespace Inno.VisualNovel.UI.Documents;

/// <summary>Owns a detached immutable set of resolved UI draw commands.</summary>
/// <param name="width">Logical canvas width.</param>
/// <param name="height">Logical canvas height.</param>
/// <param name="items">Back-to-front draw commands.</param>
public sealed record UiFrame(float width, float height, IReadOnlyList<UiDrawItem> items);
