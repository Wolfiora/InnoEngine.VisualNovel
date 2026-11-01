using System;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.UI.Binding;

namespace Inno.VisualNovel.UI.Controls.Inputs;

internal static class SliderValueWriter
{
    internal static bool Apply(
        UiDataSource source,
        UiElementDefinition definition,
        double target,
        UiDataRow? row = null
    ) {
        if (!source.TryRead(definition.binding, row, out NovelValue current) || current.kind is not (NovelValueKind.Number or NovelValueKind.Integer) || !double.IsFinite(target)) return false;
        if (definition.step > 0) target = definition.minimum + Math.Round((target - definition.minimum) / definition.step, MidpointRounding.AwayFromZero) * definition.step;
        target = Math.Clamp(target, definition.minimum, definition.maximum);
        if (current.kind == NovelValueKind.Integer)
        {
            double lower = Math.Ceiling(definition.minimum), upper = Math.Floor(definition.maximum);
            if (lower > upper || lower >= 9223372036854775808d || upper < long.MinValue) return false;
            target = Math.Clamp(Math.Round(target, MidpointRounding.AwayFromZero), lower, upper);
        }
        NovelValue value = current.kind == NovelValueKind.Integer
            ? NovelValue.Integer(target >= long.MaxValue ? long.MaxValue : target <= long.MinValue ? long.MinValue : (long)Math.Round(target, MidpointRounding.AwayFromZero)) : NovelValue.Number(target);
        return source.TryWrite(definition.binding, value, row);
    }
    internal static void Move(
        UiDataSource source,
        UiElementDefinition definition,
        int direction,
        UiDataRow? row = null
    ) {
        if (!source.TryRead(definition.binding, row, out NovelValue current) || current.kind is not (NovelValueKind.Integer or NovelValueKind.Number)) return;
        double step = definition.step > 0 ? definition.step : (definition.maximum - definition.minimum) / 20;
        if (current.kind == NovelValueKind.Integer) step = Math.Max(1, step);
        Apply(source, definition, current.number + direction * step, row);
    }
}
