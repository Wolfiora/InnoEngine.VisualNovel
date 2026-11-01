using System;
using System.Linq;

namespace Inno.VisualNovel.Authoring.Preview;

/// <summary>Checks preview case bounds independently of any active editor panel or game session.</summary>
public static class PreviewCaseRules
{
    /// <summary>Rejects ambiguous or unbounded preview declarations before execution.</summary>
    /// <param name="test">The native preview declaration.</param>
    public static void Validate(NovelPreviewCaseAsset test)
    {
        if (test.breakpoints.Length > 10000 || test.breakpoints.Select(static item => (item.chapter, item.node)).Distinct().Count() != test.breakpoints.Length)
            throw new InvalidOperationException("Preview breakpoints need unique bounded source identities.");
        foreach (var breakpoint in test.breakpoints) _ = breakpoint.Build();
        if (!Enum.IsDefined(test.mode) || test.targetInteraction is < 0 or > 1_000_000 || test.initialValues.Length > 4096 || test.initialValues.Any(static item => string.IsNullOrWhiteSpace(item.id))
            || test.initialValues.Select(static item => item.id).Distinct(StringComparer.Ordinal).Count() != test.initialValues.Length
            || test.choices.Length > 4096 || test.choices.Any(string.IsNullOrWhiteSpace) || test.inputs.Length > 4096
            || test.inputs.Any(static item => item.chapterId.Length == 0 || item.nodeId.Length == 0 || item.text.Length > 32768)
            || !double.IsFinite(test.maximumSeconds) || test.maximumSeconds is < 0.1 or > 3600
            || !double.IsFinite(test.stepSeconds) || test.stepSeconds is < 0.001 or > 1
            || test.mode != PreviewRunMode.Complete && string.IsNullOrWhiteSpace(test.nodeId))
            throw new InvalidOperationException("Preview cases require unique bounded inputs, an explicit target and finite logical replay limits.");
    }
}
