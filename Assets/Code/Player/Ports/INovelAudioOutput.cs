namespace Inno.VisualNovel.Player.Ports;

/// <summary>Owns generation-local voices while exposing only session-local tokens to audio policy; the session retires an output implementing IDisposable.</summary>
public interface INovelAudioOutput
{
    /// <summary>Starts a declared cue; implementations must release a partially created voice on failure.</summary>
    /// <param name="cueId">An explicitly loaded resource key.</param>
    /// <param name="gain">Initial linear gain.</param>
    /// <param name="loop">Whether playback loops.</param>
    /// <param name="position">Restored clip-relative seconds.</param>
    /// <returns>An owner-local token that is never serialized.</returns>
    long Play(
        string cueId,
        double gain,
        bool loop,
        double position);
    /// <summary>Stops and releases an owned voice; unknown or completed tokens are harmless.</summary>
    /// <param name="token">The owner-local voice.</param>
    void Stop(long token);
    /// <summary>Changes one live voice's gain without changing another session's mixer.</summary>
    /// <param name="token">The owner-local voice.</param>
    /// <param name="gain">Current linear gain.</param>
    void SetGain(
        long token,
        double gain);
    /// <summary>Pauses or resumes a live voice.</summary>
    /// <param name="token">The owner-local voice.</param>
    /// <param name="paused">The requested playback state.</param>
    void SetPaused(
        long token,
        bool paused);
    /// <summary>Reports preparation, playback or pause as active, and completion as inactive.</summary>
    /// <param name="token">The owner-local voice.</param>
    /// <returns>Whether the voice still accepts playback controls.</returns>
    bool IsActive(long token);
}
