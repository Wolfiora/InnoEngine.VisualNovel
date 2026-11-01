using System;
using Inno.Core.Diagnostics;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal sealed class ResourceDiagnostics : IDiagnosticSink
{
    public void Replace(DiagnosticReport report)
    {
        foreach (Diagnostic diagnostic in report.diagnostics) Console.Error.WriteLine(diagnostic.code + ": " + diagnostic.message);
    }

    public void Clear(DiagnosticSource source) { }
}
