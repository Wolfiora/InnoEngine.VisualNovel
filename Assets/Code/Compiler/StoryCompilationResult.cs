using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using InnoEngine.Graphs;
using Inno.VisualNovel.Model.Programs;

namespace Inno.VisualNovel.Compiler;

/// <summary>Contains an immutable successful program or actionable diagnostics.</summary>
public sealed class StoryCompilationResult
{
    private readonly IReadOnlyDictionary<GraphDiagnostic, FormattableString> m_diagnosticText;
    internal StoryCompilationResult(
        StoryProgram? program,
        IEnumerable<GraphDiagnostic> diagnostics,
        IReadOnlyDictionary<GraphDiagnostic, FormattableString>? diagnosticText = null
    ) {
        this.program = program;
        validation = new(diagnostics.ToArray());
        m_diagnosticText = (diagnosticText ?? new Dictionary<GraphDiagnostic, FormattableString>()).ToDictionary(static item => item.Key,
            static item => FormattableStringFactory.Create(item.Value.Format, item.Value.GetArguments().ToArray()));
    }

    /// <summary>Gets the program only when semantic validation succeeded.</summary>
    public StoryProgram? program { get; }
    /// <summary>Gets native graph validation with stable node and edge identities, including narrative-specific rules.</summary>
    public GraphValidationResult validation { get; }
    /// <summary>Gets deterministic native graph diagnostics without a parallel VN diagnostic type.</summary>
    public IReadOnlyList<GraphDiagnostic> diagnostics => validation.diagnostics;
    internal IReadOnlyDictionary<GraphDiagnostic, FormattableString> diagnosticText => m_diagnosticText;
    /// <summary>Formats a known VN source template for the current presentation owner, leaving native or extension messages unchanged.</summary>
    /// <param name="diagnostic">An exact native diagnostic owned by this result.</param>
    /// <param name="format">The current editor's template formatter; arguments retain their authored values.</param>
    /// <returns>The current localized message, independent of program data, source identity and earlier formatting calls.</returns>
    public string FormatDiagnostic(
        GraphDiagnostic diagnostic,
        Func<FormattableString, string> format
    ) {
        ArgumentNullException.ThrowIfNull(diagnostic); ArgumentNullException.ThrowIfNull(format);
        if (!diagnostics.Contains(diagnostic)) throw new ArgumentException("The diagnostic belongs to another compilation result.", nameof(diagnostic));
        return m_diagnosticText.TryGetValue(diagnostic, out var message)
            ? format(FormattableStringFactory.Create(message.Format, message.GetArguments().ToArray())) : diagnostic.message;
    }
    /// <summary>Gets whether compilation produced an executable program.</summary>
    public bool success => program is not null;
}
