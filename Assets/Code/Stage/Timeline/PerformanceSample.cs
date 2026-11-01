using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Performance;

namespace Inno.VisualNovel.Stage.Timeline;

/// <summary>Describes a deterministic scalar sample at a specified logical time.</summary>
/// <param name="target">The target semantic slot.</param>
/// <param name="property">The scalar channel.</param>
/// <param name="value">The sampled finite value.</param>
public readonly record struct PerformanceSample(
    string target,
    PerformanceProperty property,
    double value
);
