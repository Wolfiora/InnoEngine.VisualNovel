using System;
using System.Linq;
using System.Numerics;
using Inno.VisualNovel.Model.Stages;

namespace Inno.VisualNovel.Stage.Composition;

internal static class StageAffineTransform
{
    internal static Matrix3x2 From(StageTransform transform)
    {
        if (new[] { transform.x, transform.y, transform.pivotX, transform.pivotY, transform.scaleX, transform.scaleY, transform.rotation, transform.opacity }.Any(static value => !double.IsFinite(value) || Math.Abs(value) > 1e12)
            || transform.opacity is < 0 or > 1) throw new ArgumentException("Stage transform must be finite and bounded.");
        return Matrix3x2.CreateTranslation((float)-transform.pivotX, (float)-transform.pivotY)
            * Matrix3x2.CreateScale((float)transform.scaleX, (float)transform.scaleY) * Matrix3x2.CreateRotation((float)(transform.rotation * Math.PI / 180))
            * Matrix3x2.CreateTranslation((float)transform.x, (float)transform.y);
    }
    internal static Matrix3x2 From(double[] value) => new((float)value[0], (float)value[1], (float)value[2], (float)value[3], (float)value[4], (float)value[5]);
    internal static double[] Values(Matrix3x2 value) => [value.M11, value.M12, value.M21, value.M22, value.M31, value.M32];
}
