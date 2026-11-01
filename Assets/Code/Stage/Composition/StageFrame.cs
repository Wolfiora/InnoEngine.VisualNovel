using NumericsVector2 = System.Numerics.Vector2;
using System.Collections.Generic;
using InnoEngine.Rendering;
using InnoEngine.Mathematics;
using Inno.VisualNovel.Model.Stages;

namespace Inno.VisualNovel.Stage.Composition;

/// <summary>Describes one fully transformed stage quad.</summary>
/// <param name="id">The instance identity.</param>
/// <param name="topLeft">Transformed upper-left point.</param>
/// <param name="topRight">Transformed upper-right point.</param>
/// <param name="bottomRight">Transformed lower-right point.</param>
/// <param name="bottomLeft">Transformed lower-left point.</param>
/// <param name="texture">Borrowed imported texture, or null for a solid.</param>
/// <param name="color">Packed RRGGBBAA tint including opacity.</param>
/// <param name="group">The declared composition band.</param>
/// <param name="owner">The complete layer owner used by targeted transitions.</param>
/// <param name="order">The explicit owner order within the composition band.</param>
/// <param name="uv">The normalized texture region retained after local cropping.</param>
/// <param name="blend">Linear-light composition against the stage backdrop.</param>
/// <param name="mask">Optional borrowed mask stretched over the full uncropped local geometry.</param>
/// <param name="maskMode">Coverage interpretation for the assigned mask.</param>
/// <param name="maskUv">The retained fraction of the full local mask.</param>
public sealed record StageDrawItem(string id, NumericsVector2 topLeft, NumericsVector2 topRight, NumericsVector2 bottomRight, NumericsVector2 bottomLeft, TextureAsset? texture, uint color,
    StageLayerGroup group = StageLayerGroup.Background, string owner = "", int order = 0, Rect? uv = null,
    StageBlendMode blend = StageBlendMode.Normal, TextureAsset? mask = null, StageMaskMode maskMode = StageMaskMode.Alpha, Rect? maskUv = null);

/// <summary>Contains an immutable ordered stage extraction.</summary>
/// <param name="items">Back-to-front draw items.</param>
/// <param name="effects">Immutable evaluated screen operations.</param>
/// <param name="transition">An optional frozen previous composition and logical blend.</param>
public sealed record StageFrame(IReadOnlyList<StageDrawItem> items, IReadOnlyList<Effects.ScreenEffectFrame>? effects = null, Transitions.StageTransitionFrame? transition = null);
