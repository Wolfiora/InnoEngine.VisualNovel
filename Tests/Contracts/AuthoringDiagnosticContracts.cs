using System;
using Inno.Core.Serialization;
using Inno.VisualNovel.Authoring.Diagnostics;
using Inno.VisualNovel.Authoring.Variables;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class AuthoringDiagnosticContracts
{
    internal static void NativeResourcesActualErrorsAndLiteralValues(SerializationRegistry native)
    {
        _ = native;
        string name = "标题{0}##原始名称";
        AuthoringValidationException? captured = null;
        try { VariableTableEditService.Remove(new VariableTableAsset(), name); }
        catch (AuthoringValidationException error) { captured = error; }
        if (captured?.Message != "The shared variable does not exist: " + name)
            throw new InvalidOperationException("The English diagnostic changed a literal user value.");
        var argument = new AuthoringArgumentException($"Invalid argument: {name} · {12:D4} · {{literal}}", "source", captured);
        if (argument.ParamName != "source" || argument.InnerException != captured
            || !argument.Message.StartsWith("Invalid argument: " + name + " · 0012 · {literal}", StringComparison.Ordinal))
            throw new InvalidOperationException("Argument diagnostics lost their cause or invariant formatting.");
    }
}