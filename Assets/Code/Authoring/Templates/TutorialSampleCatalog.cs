using System;
using System.Collections.Generic;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Compiler;

namespace Inno.VisualNovel.Authoring.Templates;

/// <summary>Provides the five executable tutorial graph factories without retaining imported assets.</summary>
public static class TutorialSampleCatalog
{
    /// <summary>Gets stable distribution directory names, titles and required setup descriptions.</summary>
    public static IReadOnlyList<(string id, string title, string description)> entries { get; } = Array.AsReadOnly(new (string, string, string)[]
    {
        ("FirstStory", "First Story", "Six nodes of dialogue and choices introduce two editing views of one graph."),
        ("BranchingStory", "Branches and Loops", "Player name, typed variables, merges, a two-iteration loop and fragment calls."),
        ("PerformanceShowcase", "Performance Showcase", "Two characters, timeline, music, particles, vignette and background cross dissolve."),
        ("InterfaceShowcase", "Interface Showcase", "Working settings, named checkpoints and five reading layouts."),
        ("ScriptExtension", "Script Extension", "The accompanying DelayScoreCommand is a regular C# class requiring explicit registration; includes typed results, cancellation and save restoration.")
    });
    /// <summary>Creates fresh graph, chapter and text identities for one declared sample.</summary>
    /// <param name="id">One stable sample directory identity.</param>
    /// <param name="codec">The native graph settings codec.</param>
    /// <returns>A detached chapter ready to import or instantiate into ordinary Game assets.</returns>
    public static StoryDraftState Create(
        string id,
        StoryNodeDataCodec codec
    ) => id switch
    {
        "FirstStory" => FirstStoryTemplate.Create(codec), "BranchingStory" => TutorialStoryTemplates.Branching(codec),
        "PerformanceShowcase" => PerformanceStoryTemplate.Create(codec), "InterfaceShowcase" => TutorialStoryTemplates.Interface(codec),
        "ScriptExtension" => ExtensionStoryTemplate.Create(codec), _ => throw new ArgumentException("Unknown tutorial sample: " + id, nameof(id))
    };
}
