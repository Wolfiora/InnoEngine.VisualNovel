using System;
using System.Linq;
using Inno.VisualNovel.Model.Collections;
using Inno.VisualNovel.Model.Story.Nodes.Audio;
using Inno.VisualNovel.Player.Audio;
using Inno.VisualNovel.Player.Sessions;

namespace Inno.VisualNovel.Player.Collections;

/// <summary>Owns transient music-room playback and restores the caller's exact audio policy on exit.</summary>
public sealed class MusicGalleryController(NovelSession session)
{
    private AudioPlaybackSnapshot? m_saved;
    private readonly Random m_random = new();
    /// <summary>Gets the selected collection identity, empty before a track is selected.</summary>
    public string selected { get; private set; } = string.Empty;
    /// <summary>Gets whether the selected gallery instance is paused.</summary>
    public bool paused { get; private set; }
    /// <summary>Gets or sets whether a completed track repeats instead of advancing.</summary>
    public bool repeat { get; set; }
    /// <summary>Gets or sets whether advancing chooses another unlocked track at random.</summary>
    public bool shuffle { get; set; }
    /// <summary>Gets whether an audio adapter and any unlocked tracks are available.</summary>
    public bool available => session.audio is not null && Playlist().Length > 0;
    /// <summary>Prepares an unlocked track before replacing caller audio; failed preparation leaves the prior state intact.</summary>
    /// <param name="id">An unlocked music entry.</param>
    public void Play(string id)
    {
        CollectionEntry entry = session.collections.Get(id);
        if (entry.kind != CollectionKind.Music || !session.collections.IsUnlocked(id)) throw new InvalidOperationException("Music is locked or unavailable: " + id);
        NovelAudioDirector director = session.audio ?? throw new InvalidOperationException("Music playback requires an audio adapter.");
        var before = director.Capture(); director.PlayGallery(entry.musicCueId); m_saved ??= before;
        selected = id; paused = false;
    }
    /// <summary>Pauses or resumes only the gallery instance.</summary>
    public void TogglePause()
    {
        if (selected.Length == 0) return;
        session.audio!.Apply(new() { slot = "gallery", operation = paused ? AudioOperation.Resume : AudioOperation.Pause }); paused = !paused;
    }
    /// <summary>Moves through the currently unlocked playlist, wrapping at either end.</summary>
    /// <param name="direction">Positive for next, negative for previous.</param>
    public void Move(int direction)
    {
        CollectionEntry[] list = Playlist(); if (list.Length == 0 || session.audio is null) return;
        int current = Array.FindIndex(list, item => item.id == selected);
        int next = current < 0 ? 0 : (current + (direction < 0 ? -1 : 1) + list.Length) % list.Length;
        if (shuffle && direction > 0 && list.Length > 1 && current >= 0) { next = m_random.Next(list.Length - 1); if (next >= current) next++; }
        Play(list[next].id);
    }
    /// <summary>Advances naturally completed non-looping output; pause never selects another track.</summary>
    public void Tick()
    {
        if (selected.Length == 0 || paused || session.audio!.IsActive("gallery")) return;
        if (repeat) Play(selected); else Move(1);
    }
    /// <summary>Restores caller audio before discarding local playlist state.</summary>
    public void Leave()
    {
        if (m_saved is null) return;
        session.audio!.Restore(m_saved); m_saved = null; selected = string.Empty; paused = false;
    }
    private CollectionEntry[] Playlist() => session.collections.entries.Where(entry => entry.kind == CollectionKind.Music && session.collections.IsUnlocked(entry.id)).ToArray();
}
