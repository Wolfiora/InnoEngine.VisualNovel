using System.Threading;
using System.Threading.Tasks;
using Inno.VisualNovel.Player.Sessions;

namespace Inno.VisualNovel.Player.Ports;

/// <summary>Captures an exact save-boundary image without exposing rendering or device ownership to the player.</summary>
public interface INovelSaveThumbnail
{
    /// <summary>Freezes presentation synchronously before awaiting the host's later image readback.</summary>
    /// <param name="state">The already frozen state from the same owner-thread boundary.</param>
    /// <param name="cancellationToken">Cancellation when the save or game owner is retired.</param>
    /// <returns>A standard PNG thumbnail, or an explicit failure; no later live image may be substituted.</returns>
    ValueTask<byte[]> CaptureAsync(
        NovelSessionState state,
        CancellationToken cancellationToken = default
    );
}
