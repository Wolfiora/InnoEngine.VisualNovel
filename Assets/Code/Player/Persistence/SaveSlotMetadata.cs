namespace Inno.VisualNovel.Player.Persistence;

/// <summary>Describes a stored packet, including explicit damaged and incompatible states.</summary>
public sealed record SaveSlotMetadata(
    SaveSlot slot,
    long utcTicks,
    string summary,
    double playTime,
    byte[] thumbnail,
    bool compatible,
    string error);
