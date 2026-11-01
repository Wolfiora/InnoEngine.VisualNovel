using System;

namespace Inno.VisualNovel.Narrative.Values;

/// <summary>Owns reproducible random draws whose exact state can be checkpointed.</summary>
public sealed class StoryRandomState
{
    private ulong m_state;

    /// <summary>Creates a random stream from a nonzero seed.</summary>
    /// <param name="seed">The stream seed; zero is invalid.</param>
    public StoryRandomState(ulong seed) => Restore(seed);
    /// <summary>Gets the complete stream state.</summary>
    public ulong state => m_state;
    /// <summary>Restores a previously captured nonzero state.</summary>
    /// <param name="value">The exact state.</param>
    public void Restore(ulong value) => m_state = value != 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    /// <summary>Draws one value in the half-open interval [0, 1).</summary>
    /// <returns>A deterministic uniform fraction.</returns>
    public double Next()
    {
        ulong value = m_state;
        value ^= value >> 12;
        value ^= value << 25;
        value ^= value >> 27;
        m_state = value;
        return (unchecked(value * 2685821657736338717UL) >> 11) * (1.0 / 9007199254740992.0);
    }
}
