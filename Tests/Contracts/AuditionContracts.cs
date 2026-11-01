using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Authoring.Preview;
using Inno.VisualNovel.Model.Audio;
using Inno.VisualNovel.Player.Ports;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class AuditionContracts
{
    internal static void Run(AudioCueAsset cue)
    {
        var outputs = new List<Output>(); bool fail = false;
        using var preview = new AudioAudition(_ => { var output = new Output { fail = fail }; outputs.Add(output); return output; });
        preview.Tick(1);
        Check(outputs.Count == 0 && !preview.active, "Opening or updating an audio preview started device playback without an explicit action.");
        preview.Play(cue); Check(outputs.Count == 1 && outputs[0].voices.Count == 1 && preview.active, "Explicit preview did not create one isolated voice owner.");
        preview.Tick(.025); double position = preview.position; preview.TogglePause(); preview.Tick(.025);
        Check(preview.paused && preview.position == position && outputs[0].voices.Values.Single().paused, "Paused audition advanced or failed to suspend its own voice.");
        preview.Seek(.05); Check(Math.Abs(preview.position - .05) < 1e-9 && outputs[0].voices.Count == 1 && preview.paused, "Preview seek leaked the replaced voice or lost pause state.");
        fail = true; Reject(() => preview.Play(cue));
        Check(preview.active && preview.paused && outputs[0].voices.Count == 1 && outputs[1].voices.Count == 0, "Failed candidate audition discarded the currently usable preview.");
        fail = false; preview.Play(cue); Check(outputs[0].voices.Count == 0 && outputs[2].voices.Count == 1, "Selecting a new audition left the previous output owner playing.");
        preview.Stop(); Check(outputs.All(static output => output.voices.Count == 0) && !preview.active, "Stopping audition left a loop or replacement voice alive.");
        preview.Play(cue); preview.Dispose(); Check(outputs.All(static output => output.voices.Count == 0), "Closing an audition owner leaked physical voice tokens.");
    }
    private sealed class Output : INovelAudioOutput
    {
        private long m_next;
        internal bool fail;
        internal readonly Dictionary<long, (double position, bool paused)> voices = [];
        public long Play(string cueId, double gain, bool loop, double position)
        { if (fail) throw new InvalidOperationException("Injected output preparation failure."); long id = ++m_next; voices[id] = (position, false); return id; }
        public void Stop(long token) => voices.Remove(token);
        public void SetGain(long token, double gain) { }
        public void SetPaused(long token, bool paused) { if (voices.TryGetValue(token, out var voice)) voices[token] = (voice.position, paused); }
        public bool IsActive(long token) => voices.ContainsKey(token);
    }
    private static void Reject(Action action) { try { action(); } catch (InvalidOperationException) { return; } throw new InvalidOperationException("Failed audio output was accepted."); }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
