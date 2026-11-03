using System.Collections.Generic;
using System.Linq;
using InnoEngine.Graphs;
using Inno.VisualNovel.Model.Programs;

namespace Inno.VisualNovel.Compiler;

/// <summary>Contains an immutable successful program or native graph diagnostics.</summary>
public sealed class StoryCompilationResult
{
    internal StoryCompilationResult(
        StoryProgram? program,
        IEnumerable<GraphDiagnostic> diagnostics
    ) {
        this.program = program;
        validation = new(diagnostics.ToArray());
    }
    /// <summary>Gets the program only when semantic validation succeeded.</summary>
    public StoryProgram? program { get; }
    /// <summary>Gets native validation with stable node and edge identities.</summary>
    public GraphValidationResult validation { get; }
    /// <summary>Gets deterministic English diagnostics; authored values remain unchanged.</summary>
    public IReadOnlyList<GraphDiagnostic> diagnostics => validation.diagnostics;
    /// <summary>Gets whether compilation produced an executable program.</summary>
    public bool success => program is not null;
}