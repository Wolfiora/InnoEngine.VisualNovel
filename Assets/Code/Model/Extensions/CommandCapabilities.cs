using System;

namespace Inno.VisualNovel.Model.Extensions;

/// <summary>Declares lifecycle guarantees required by preview, saves and rollback.</summary>
[Flags]
public enum CommandCapabilities
{
    /// <summary>Declares no optional lifecycle guarantees.</summary>
    None = 0,
    /// <summary>Permits execution in a side-effect-controlled authoring preview.</summary>
    Preview = 1,
    /// <summary>Supports explicit user cancellation through the cancel output.</summary>
    Cancel = 2,
    /// <summary>Can resume exclusively from its validated neutral command state.</summary>
    Save = 4,
    /// <summary>Can be crossed by ordinary game rollback.</summary>
    Rollback = 8,
    /// <summary>Performs effects outside the owned VN state and establishes a rollback barrier.</summary>
    ExternalEffects = 16
}
