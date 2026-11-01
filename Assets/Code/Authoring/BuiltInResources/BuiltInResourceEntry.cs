namespace Inno.VisualNovel.Authoring.BuiltInResources;

/// <summary>Describes one explicitly packaged preset without holding project assets or generation-local CLR objects.</summary>
/// <param name="kind">The implemented preview and editing category.</param>
/// <param name="path">The exact plugin-local native resource path.</param>
/// <param name="name">The searchable author-facing name.</param>
/// <param name="usage">The supported target and intended use.</param>
public sealed record BuiltInResourceEntry(
    BuiltInResourceKind kind,
    string path,
    string name,
    string usage
);
