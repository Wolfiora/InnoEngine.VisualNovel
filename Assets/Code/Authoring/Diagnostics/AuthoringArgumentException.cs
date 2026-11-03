using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Inno.VisualNovel.Authoring.Diagnostics;

/// <summary>Preserves normal argument-error semantics with invariant formatting of authoring context.</summary>
public sealed class AuthoringArgumentException : ArgumentException
{
    /// <summary>Creates an actionable argument error preserving its original cause.</summary>
    /// <param name="message">Developer-authored interpolated source.</param>
    /// <param name="paramName">The unchanged public argument identity.</param>
    /// <param name="innerException">The original cause when supplied.</param>
    public AuthoringArgumentException(
        FormattableString message,
        string? paramName = null,
        Exception? innerException = null
    ) : base(message?.ToString(CultureInfo.InvariantCulture) ?? throw new ArgumentNullException(nameof(message)), paramName, innerException) {
    }
}
