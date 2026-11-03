using System;
using System.Buffers.Binary;
using System.Linq;
using Inno.Core.Mathematics;
using Inno.UI;
using Inno.VisualNovel.Rendering.Frames;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class EngineUiGeometryContracts
{
    internal static void RotatedClipsRetainTextureCoordinatesAndDetachedFrames()
    {
        var builder = new UiRenderFrameBuilder();
        var mesh = new UiMeshHandle(1);
        builder.AddMeshUpdate(mesh, 1, [new(0, 0, 0, 0, uint.MaxValue), new(100, 0, 1, 0, uint.MaxValue),
            new(100, 100, 1, 1, uint.MaxValue), new(0, 100, 0, 1, uint.MaxValue)], [0, 1, 2, 0, 2, 3]);
        var command = new UiDrawCommand(mesh, default, true, new(25, 25, 50, 50)); builder.AddCommand(command);
        Matrix transform = Matrix.CreateTranslation(130, 110, 0) * Matrix.CreateRotationZ(.7f) * Matrix.CreateScale(-1, 1, 1);
        var cache = new NovelEngineUiFrameCache();
        var draw = cache.Apply(builder.Build(), transform, 300, 300).Single();
        byte[] frozen = draw.vertices.ToArray();
        float area = 0;
        for (int triangle = 0; triangle < draw.vertexCount; triangle += 3)
        {
            var a = Point(triangle); var b = Point(triangle + 1); var c = Point(triangle + 2);
            area += Math.Abs((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x)) * .5f;
        }
        Check(Math.Abs(area - 2500) < .02f, "Rotated clipping lost area or retained geometry outside the original clip.");
        var repeated = new UiRenderFrameBuilder(); repeated.AddCommand(command);
        Check(cache.Apply(repeated.Build(), transform, 300, 300).Single().vertices.Span.SequenceEqual(frozen), "Resource deltas changed a repeated rotated UI frame.");
        _ = cache.Apply(UiRenderFrame.CreateRetirement([mesh], []), transform, 300, 300);
        Check(draw.vertices.Span.SequenceEqual(frozen), "Retiring the engine mesh mutated an already captured frame.");
        bool rejected = false;
        var stale = new UiRenderFrameBuilder(); stale.AddCommand(command);
        try { cache.Apply(stale.Build(), transform, 300, 300); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "A retired UI mesh was silently reused.");

        Vector2 Point(int index)
        {
            float Read(int field) => BinaryPrimitives.ReadSingleLittleEndian(frozen.AsSpan(index * 32 + field * 4, 4));
            var local = Vector2.Transform(new((Read(0) + 1) * 150, (1 - Read(1)) * 150), Matrix.Invert(transform));
            Check(local.x >= 24.999f && local.x <= 75.001f && local.y >= 24.999f && local.y <= 75.001f,
                "A rotated UI triangle escaped its native clip.");
            Check(Math.Abs(Read(2) - local.x / 100) < .0001f && Math.Abs(Read(3) - local.y / 100) < .0001f,
                "Clipped UI vertices lost interpolated texture coordinates.");
            return local;
        }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
