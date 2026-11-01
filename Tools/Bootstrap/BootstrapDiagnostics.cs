using System;
using Inno.Core.Diagnostics;

namespace Inno.VisualNovel.Tools.Bootstrap;

internal sealed class BootstrapDiagnostics : IDiagnosticSink
{
    public void Replace(DiagnosticReport report)
    {
        foreach (Diagnostic diagnostic in report.diagnostics) Console.Error.WriteLine(diagnostic.code + ": " + diagnostic.message);
    }
    public void Clear(DiagnosticSource source) { }
}
