using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Player.Sessions;

namespace Inno.VisualNovel.Authoring.ProjectEditing;

/// <summary>Supplies resource designers with the same linked declarations validated by the runtime.</summary>
public static class NovelProjectVariables
{
    /// <summary>Reads detached shared declarations without retaining mutable chapter or editor aliases.</summary>
    /// <param name="project">The explicit game definition.</param>
    /// <returns>The linked variable types, defaults and scopes.</returns>
    public static VariableDefinition[] Read(NovelProjectAsset project) => new StoryProgramLibrary(project).program.variables;
}
