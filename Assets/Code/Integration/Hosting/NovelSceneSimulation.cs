using System;
using InnoEngine.Scene;
using Inno.VisualNovel.Integration.Components;

namespace Inno.VisualNovel.Integration.Hosting;

/// <summary>Advances native VN components in the owning System update, independently of rendering.</summary>
public static class NovelSceneSimulation
{
    /// <summary>Advances active native component clocks; passive view capture never calls this method.</summary>
    /// <param name="scene">The explicit native scene owner.</param>
    /// <param name="seconds">Finite nonnegative logical elapsed seconds.</param>
    public static void Tick(
        GameScene scene,
        double seconds
    ) {
        ArgumentNullException.ThrowIfNull(scene);
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        NovelPerformanceComponent.ValidateWriters(scene);
        foreach (var item in scene.GetObjects())
            if (item.activeInHierarchy)
                foreach (var camera in item.GetComponents<NovelCameraComponent>()) camera.ValidateFollow();
        foreach (var item in scene.GetObjects())
        {
            if (!item.activeInHierarchy) continue;
            foreach (var component in item.GetComponents<NovelPerformanceComponent>()) component.Tick(seconds);
            foreach (var component in item.GetComponents<NovelCharacterComponent>()) component.Tick(seconds);
            foreach (var component in item.GetComponents<NovelParticleComponent>()) component.Tick(seconds);
            foreach (var component in item.GetComponents<NovelScreenEffectComponent>()) component.Tick(seconds);
            foreach (var component in item.GetComponents<NovelAudioComponent>()) component.Tick(seconds);
        }
        // Follow after every target's performance update, so native sibling order cannot change camera behavior.
        foreach (var item in scene.GetObjects())
            if (item.activeInHierarchy)
                foreach (var camera in item.GetComponents<NovelCameraComponent>()) camera.ApplyFollow();
    }
}
