using System;
using System.Collections.Generic;
using System.Linq;
using Inno.Audio;
using Inno.Core.Identity;
using Inno.Extensibility.Types;
using Inno.Scene;
using Inno.VisualNovel.Integration.Components;
using Inno.VisualNovel.Integration.Hosting;
using Inno.VisualNovel.Model.Audio;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Player.Ports;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class NativeAudioContracts
{
    internal static void NativeLifetimeAndSharedPolicy(
        IdentityAllocator identities,
        TypeCatalog types
    ) {
        using var world = new SceneWorld(identities, types); using var scope = world.EnterScope();
        var scene = SceneManager.LoadNewSceneAdditive("Native audio");
        var system = scene.AddSystem<NovelSceneSystem>();
        var parent = scene.CreateObject("Audio parent");
        var audio = scene.CreateObject("Ambience").AddComponent<NovelAudioComponent>(); audio.transform.SetParent(parent.transform);
        audio.playOnEnable = false;
        audio.cue = new AudioCueAsset { resourceId = "rain", clip = new AudioClipAsset(), channel = NovelAudioChannel.Ambience, volume = .8, fadeIn = 1, fadeOut = 1, loop = true };
        world.FixedUpdate(0);
        var output = new Output(); audio.gain = .5; audio.SetPlayerGain(.5); audio.Play(output);
        Check(output.gains.Values.Single() == 0, "Scene audio ignored the existing cue fade-in policy.");
        NovelSceneSimulation.Tick(scene, .5);
        Check(Math.Abs(output.gains.Values.Single() - .1) < 1e-8 && audio.Capture().voices.Single().position == .5,
            "Native audio did not combine cue/component/player gains or advance its existing policy clock.");
        audio.paused = true; NovelSceneSimulation.Tick(scene, .5);
        Check(output.paused.Values.Single() && audio.Capture().voices.Single().position == .5, "Pausing scene audio advanced playback position.");
        var snapshot = audio.Capture();
        var rejected = new Output { fail = true };
        try { audio.Restore(snapshot, rejected); throw new ArgumentException("A failed native voice candidate was accepted."); }
        catch (InvalidOperationException) { }
        Check(rejected.disposed && output.gains.Count == 1 && !output.disposed, "Failed native audio restoration replaced active playback.");
        var restored = new Output(); audio.Restore(snapshot, restored);
        Check(output.disposed && output.gains.Count == 0 && restored.paused.Values.Single() && restored.positions.Single() == .5,
            "Successful native audio restoration did not replace ownership and preserve pause/position.");
        audio.paused = false;
        var performance = scene.CreateObject("Volume fade").AddComponent<NovelPerformanceComponent>();
        performance.clip = new PerformanceClipAsset { duration = 1, tracks = [new() { target = "audio", property = PerformanceProperty.Volume,
            keys = [new() { value = 0 }, new() { time = 1, value = 1 }] }] };
        performance.bindings = [new() { slot = "audio", target = audio.gameObject }]; performance.Seek(.25);
        Check(audio.gain == .25 && Math.Abs(restored.gains.Values.Single() - .05) < 1e-8,
            "A native volume track did not write the actual component gain and voice policy.");
        audio.Stop(); NovelSceneSimulation.Tick(scene, 1);
        Check(!audio.isPlaying && restored.disposed, "A completed native fade-out retained its owned audio output.");
        var stopped = new Output(); audio.Play(stopped); system.StopGame();
        Check(stopped.disposed && !audio.isPlaying, "Stopping the scene system left an owned native emitter playing.");
        var active = new Output(); audio.Play(active); parent.SetActive(false);
        Check(active.disposed && active.gains.Count == 0, "Disabling a native parent left scene audio active.");
        parent.SetActive(true); var final = new Output(); audio.Play(final); scene.DestroyObject(audio.gameObject);
        Check(final.disposed && final.gains.Count == 0, "Deleting the native audio object leaked its output.");
    }
    private sealed class Output : INovelAudioOutput, IDisposable
    {
        private long m_next;
        internal readonly Dictionary<long, double> gains = [];
        internal readonly Dictionary<long, bool> paused = [];
        internal readonly List<double> positions = [];
        internal bool disposed, fail;
        public long Play(string cueId, double gain, bool loop, double position)
        {
            if (fail) throw new InvalidOperationException("Injected voice preparation failure.");
            long token = ++m_next; gains.Add(token, gain); paused.Add(token, false); positions.Add(position); return token;
        }
        public void Stop(long token) { gains.Remove(token); paused.Remove(token); }
        public void SetGain(long token, double value) => gains[token] = value;
        public void SetPaused(long token, bool value) => paused[token] = value;
        public bool IsActive(long token) => gains.ContainsKey(token);
        public void Dispose() { disposed = true; gains.Clear(); paused.Clear(); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
