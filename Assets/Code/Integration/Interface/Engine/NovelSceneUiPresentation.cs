using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Mathematics;
using InnoEngine.Scene;
using InnoEngine.UI;
using Inno.VisualNovel.Integration.Components.Interface;
using Inno.VisualNovel.Integration.Rendering;
using Inno.VisualNovel.Rendering.Frames;

namespace Inno.VisualNovel.Integration.Interface.Engine;

/// <summary>Owns engine UI contexts for the real canvases of one scene; cached data contains no authored world.</summary>
internal sealed class NovelSceneUiPresentation : IDisposable
{
    private readonly Dictionary<NovelUiRootComponent, Surface> m_surfaces = [];
    private IUiService? m_service;
    private NovelUiRootComponent? m_pointerOwner;
    private NovelUiRootComponent? m_keyboardOwner;
    private readonly HashSet<InnoEngine.Input.MouseButton> m_buttons = [];
    private bool m_discardRelease;

    internal Inno.VisualNovel.UI.Input.UiInputResult Handle(
        GameScene scene,
        NovelSceneProjection projection,
        UiInputSnapshot input,
        Inno.VisualNovel.UI.Binding.UiDataSource? source
    ) {
        var roots = Synchronize(scene);
        // RML finalizes changed transforms during geometry extraction. Retain every delta in the render cache before hit testing.
        foreach (var root in roots)
        {
            var surface = m_surfaces[root];
            _ = surface.cache.Apply(surface.ui.Capture(source), Transform(root, projection), projection.width, projection.height);
        }
        var modal = roots.LastOrDefault(root => m_surfaces[root].ui.modal);
        bool invalidCapture = m_pointerOwner is not null && (!roots.Contains(m_pointerOwner)
            || modal is not null && !ReferenceEquals(modal, m_pointerOwner));
        if (invalidCapture)
        {
            if (m_surfaces.TryGetValue(m_pointerOwner!, out var old)) old.ui.CancelInput();
            m_pointerOwner = null; m_discardRelease = m_buttons.Count > 0; m_buttons.Clear();
        }
        if (m_keyboardOwner is not null && !roots.Contains(m_keyboardOwner)) m_keyboardOwner = null;
        bool pressed = input.buttonsPressed.Count > 0;
        if (pressed) m_discardRelease = false;
        if (m_discardRelease)
        {
            if (input.buttonsReleased.Count > 0) m_discardRelease = false;
            return new(true, []);
        }
        NovelUiRootComponent? hit = null;
        foreach (var root in roots.Reverse())
        {
            if (hit is not null || modal is not null && !ReferenceEquals(root, modal))
            { _ = m_surfaces[root].ui.Pick(new(-100000, -100000), source); continue; }
            var matrix = Transform(root, projection);
            if (Math.Abs(Matrix.Determinant(matrix)) < .000001f) continue;
            if (m_surfaces[root].ui.Pick(Vector2.Transform(input.mousePosition, Matrix.Invert(matrix)), source) is not null)
                hit = root;
        }
        if (pressed)
        {
            m_pointerOwner ??= modal ?? hit;
            foreach (var button in input.buttonsPressed) m_buttons.Add(button);
            if (m_pointerOwner is not null) m_keyboardOwner = m_pointerOwner;
        }
        bool keyboard = input.keysPressed.Count > 0 || input.keysReleased.Count > 0 || input.textInput.Count > 0;
        var owner = modal ?? m_pointerOwner ?? (keyboard
            ? m_keyboardOwner ?? roots.LastOrDefault(root => m_surfaces[root].ui.hasControls) : hit);
        var actions = new List<Inno.VisualNovel.UI.Input.UiActionRequest>();
        if (owner is not null)
        {
            var matrix = Transform(owner, projection);
            if (Math.Abs(Matrix.Determinant(matrix)) >= .000001f)
            {
                var inverse = Matrix.Invert(matrix);
                Vector2 position = Vector2.Transform(input.mousePosition, inverse);
                actions.AddRange(m_surfaces[owner].ui.Handle(new(position, input.scrollDelta, input.modifiers,
                    input.keysPressed, input.keysReleased, input.buttonsPressed, input.buttonsReleased, input.textInput), source));
                if (keyboard) m_keyboardOwner = owner;
            }
        }
        foreach (var button in input.buttonsReleased) m_buttons.Remove(button);
        if (m_buttons.Count == 0) m_pointerOwner = null;
        return new(owner is not null || modal is not null, actions.AsReadOnly());
    }

    internal void ReleaseInput()
    {
        foreach (var surface in m_surfaces.Values) surface.ui.CancelInput();
        m_discardRelease |= m_buttons.Count > 0;
        m_buttons.Clear(); m_pointerOwner = null; m_keyboardOwner = null;
    }
    internal IReadOnlyList<NovelDrawMesh> Capture(
        GameScene scene,
        NovelSceneProjection projection,
        Inno.VisualNovel.UI.Binding.UiDataSource? source = null
    ) {
        var roots = Synchronize(scene);
        var draws = new List<NovelDrawMesh>();
        foreach (var root in roots)
        {
            var surface = m_surfaces[root];
            draws.AddRange(surface.cache.Apply(surface.ui.Capture(source), Transform(root, projection), projection.width, projection.height));
        }
        return draws.AsReadOnly();
    }
    internal GameObject? Pick(
        GameScene scene,
        NovelSceneProjection projection,
        Vector2 position,
        Inno.VisualNovel.UI.Binding.UiDataSource? source = null
    ) {
        foreach (var root in Synchronize(scene).Reverse())
        {
            Matrix transform = Transform(root, projection);
            if (Math.Abs(Matrix.Determinant(transform)) < .000001f) continue;
            var owner = m_surfaces[root].ui.Pick(Vector2.Transform(position, Matrix.Invert(transform)), source);
            if (owner is not null) return owner;
        }
        return null;
    }
    private NovelUiRootComponent[] Synchronize(GameScene scene)
    {
        var roots = NovelSceneCapture.Objects(scene).SelectMany(static item => item.GetComponents<NovelUiRootComponent>()).OrderBy(static root => root.order).ToArray();
        foreach (var root in m_surfaces.Keys.Where(root => !roots.Contains(root)).ToArray())
        { m_surfaces[root].ui.Dispose(); m_surfaces.Remove(root); }
        if (roots.Length == 0) return roots;
        var service = UiExecutionContext.current;
        if (m_service is not null && !ReferenceEquals(m_service, service)) Dispose();
        m_service = service;
        foreach (var root in roots)
            if (!m_surfaces.ContainsKey(root)) m_surfaces.Add(root, new(new(root, service), new()));
        return roots;
    }
    private static Matrix Transform(
        NovelUiRootComponent root,
        NovelSceneProjection projection
    )
        => Matrix.CreateTranslation(projection.width * .5f, projection.height * .5f, 0) * Matrix.CreateScale(projection.pixelsPerUnit, -projection.pixelsPerUnit, 1)
            * projection.worldToView * root.transform.localToWorldMatrix * Matrix.CreateTranslation(-root.width * .5f, root.height * .5f, 0) * Matrix.CreateScale(1, -1, 1);
    public void Dispose()
    {
        foreach (var surface in m_surfaces.Values) { surface.ui.Dispose(); surface.cache.Clear(); }
        m_surfaces.Clear(); m_service = null;
        m_buttons.Clear(); m_pointerOwner = null; m_keyboardOwner = null; m_discardRelease = false;
    }
    private sealed record Surface(NovelEngineUiSurface ui, NovelEngineUiFrameCache cache);
}
