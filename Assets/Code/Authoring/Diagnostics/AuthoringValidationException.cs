using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Inno.VisualNovel.Authoring.Diagnostics;

/// <summary>Retains an actionable authoring message and its separate values for owner-local editor translation.</summary>
public sealed class AuthoringValidationException : InvalidOperationException
{
    /// <summary>Creates a normal validation exception while retaining the interpolated source for presentation.</summary>
    /// <param name="message">Developer-authored source text with user values in interpolation holes.</param>
    /// <param name="innerException">The original failure when this message adds authoring context.</param>
    public AuthoringValidationException(
        FormattableString message,
        Exception? innerException = null
    ) : base(message?.ToString(CultureInfo.InvariantCulture) ?? throw new ArgumentNullException(nameof(message)), innerException) {
        source = message.Format; arguments = Array.AsReadOnly(message.GetArguments().ToArray());
    }
    /// <summary>Gets the untranslated composite source; it never includes interpolated user values.</summary>
    public string source { get; }
    /// <summary>Gets copied, transient interpolation arguments; these are not a persistence or reload payload.</summary>
    public IReadOnlyList<object?> arguments { get; }
}
