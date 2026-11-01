using InnoEngine.Graphs;

namespace Inno.VisualNovel.Compiler.Extensions;

/// <summary>Defines an editor-only node that lowers to a validated built-in instruction without adding a runtime interpreter.</summary>
public abstract class StoryNodeCompiler : GraphNodeDefinition
{
    /// <summary>Creates a stable, generation-owned compiler declaration.</summary>
    /// <param name="id">A unique extension identity outside the reserved inno.vn namespace.</param>
    /// <param name="displayName">The author-facing instruction name.</param>
    /// <param name="category">The authoring menu category.</param>
    protected StoryNodeCompiler(
        string id,
        string displayName,
        string category
    ) : base(id, displayName, category) { }

    /// <summary>Creates a built-in node with the same source identity and exact control-flow port contract.</summary>
    /// <param name="context">Detached native settings and declarations; no assets, files or mutable source graph are exposed.</param>
    /// <returns>A built-in node created through the supplied context; it is validated by the production compiler.</returns>
    public abstract GraphNodeRecord Lower(StoryNodeCompilationContext context);
}
