using System;

namespace Inno.VisualNovel.Authoring.BuiltInResources;

/// <summary>Describes a committed native project output without retaining an installation asset or editor-generation object.</summary>
/// <param name="sourcePath">The original preset or containing theme source.</param>
/// <param name="path">The writable project output path.</param>
/// <param name="assetId">The newly assigned native persistent identity.</param>
public sealed record BuiltInResourceCopyResult(string sourcePath, string path, Guid assetId);
