using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Stage.Composition;

namespace Inno.VisualNovel.Rendering.Frames;

internal static class NovelStageImagePlanner
{
    internal static bool RequiresProcessing(StageFrame? frame) => frame is not null &&
        (frame.items.Any(static item => item.mask is not null || item.blend != StageBlendMode.Normal) || RequiresProcessing(frame.transition?.before));

    internal static NovelImagePlan Append(
        NovelImagePlan backdrop,
        IReadOnlyList<StageDrawItem> items,
        Func<IReadOnlyList<StageDrawItem>, NovelImagePlan> paint
    ) {
        var pending = new List<StageDrawItem>(); int special = 0;
        foreach (var item in items)
        {
            if (item.mask is null && item.blend == StageBlendMode.Normal) { pending.Add(item); continue; }
            if (++special > 48) throw new InvalidOperationException("Stage image composition exceeds its 48 specialized layer budget.");
            if (!Enum.IsDefined(item.blend) || !Enum.IsDefined(item.maskMode)) throw new InvalidOperationException("Unknown stage blend or mask mode.");
            Flush(); NovelImagePlan image = paint([item]);
            if (item.mask is not null)
            {
                var mask = item with { texture = item.mask, mask = null, color = 0xFFFFFFFF, uv = item.maskUv, blend = StageBlendMode.Normal };
                image = new NovelProcessImage(image, paint([mask]), new(210 + (int)item.maskMode, 1, 0, 0), default);
            }
            backdrop = item.blend == StageBlendMode.Normal ? new NovelStackImage([backdrop, image])
                : new NovelProcessImage(image, backdrop, new(199 + (int)item.blend, 1, 0, 0), default);
        }
        Flush(); return backdrop;

        void Flush()
        {
            if (pending.Count == 0) return;
            backdrop = new NovelStackImage([backdrop, paint(pending.ToArray())]); pending.Clear();
        }
    }
}
