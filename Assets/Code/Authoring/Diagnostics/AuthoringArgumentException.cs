using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Inno.VisualNovel.Authoring.Diagnostics;

/// <summary>Preserves normal argument-error semantics and separate authoring text for editor presentation.</summary>
public sealed class AuthoringArgumentException : ArgumentException
{
    /// <summary>Creates an actionable argument error without merging user values into its translation identity.</summary>
    /// <param name="message">Developer-authored interpolated source.</param>
    /// <param name="paramName">The unchanged public argument identity.</param>
    /// <param name="innerException">The original cause when supplied.</param>
    public AuthoringArgumentException(
        FormattableString message,
        string? paramName = null,
        Exception? innerException = null
    ) : base(message?.ToString(CultureInfo.InvariantCulture) ?? throw new ArgumentNullException(nameof(message)), paramName, innerException) {
        source = message.Format; arguments = Array.AsReadOnly(message.GetArguments().ToArray());
    }
    /// <summary>Gets the untranslated composite source independently of the argument name.</summary>
    public string source { get; }
    /// <summary>Gets copied transient interpolation arguments, never serialized into a document or game save.</summary>
    public IReadOnlyList<object?> arguments { get; }
}
