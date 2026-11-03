using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Inno.VisualNovel.Authoring.Diagnostics;

/// <summary>Reports a failed authoring operation without changing the active document.</summary>
public sealed class AuthoringValidationException : InvalidOperationException
{
    /// <summary>Creates a normal validation exception using invariant formatting for the supplied values.</summary>
    /// <param name="message">Developer-authored source text with user values in interpolation holes.</param>
    /// <param name="innerException">The original failure when this message adds authoring context.</param>
    public AuthoringValidationException(
        FormattableString message,
        Exception? innerException = null
    ) : base(message?.ToString(CultureInfo.InvariantCulture) ?? throw new ArgumentNullException(nameof(message)), innerException) {
    }
}
