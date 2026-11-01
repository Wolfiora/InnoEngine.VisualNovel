using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Inno.VisualNovel.Model.Programs;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Player.Sessions;

/// <summary>Links imported chapter programs through stable public entries without reading authoring graphs.</summary>
public sealed class StoryProgramLibrary
{
    private readonly bool m_qualified;
    private readonly HashSet<string> m_chapters;
    /// <summary>Validates shared declarations and links exact imported chapter programs.</summary>
    /// <param name="project">The explicit game's chapter roots.</param>
    public StoryProgramLibrary(NovelProjectAsset project)
    {
        StoryProgram[] chapters = project.chapters.Select(static chapter => StoryProgramCopy.Copy(chapter.program
            ?? throw new ArgumentException("Every chapter must have a compiled runtime program."))).ToArray();
        var byId = chapters.ToDictionary(static item => item.chapterId, StringComparer.Ordinal);
        m_chapters = byId.Keys.ToHashSet(StringComparer.Ordinal);
        if (byId.Count == 0 || !byId.ContainsKey(project.startChapter)) throw new ArgumentException("The game's initial compiled chapter is missing.");
        m_qualified = chapters.Length > 1;
        var variables = new Dictionary<string, VariableDefinition>(StringComparer.Ordinal);
        foreach (StoryProgram chapter in chapters)
            foreach (VariableDefinition variable in chapter.variables)
            {
                if (variables.TryGetValue(variable.id, out VariableDefinition? previous))
                {
                    if (previous.scope != variable.scope || !previous.initialValue.Equals(variable.initialValue) || !previous.members.SequenceEqual(variable.members))
                        throw new ArgumentException("Chapters disagree about shared variable declaration: " + variable.id);
                }
                else variables.Add(variable.id, variable);
            }
        foreach (StoryProgram chapter in chapters)
            foreach (StoryInstruction instruction in chapter.instructions)
            {
                instruction.sourceChapterId = chapter.chapterId;
                instruction.sourceNodeId = instruction.nodeId;
                instruction.nodeId = Address(chapter.chapterId, instruction.nodeId);
                foreach (InstructionTarget target in instruction.targets) target.nodeId = Address(chapter.chapterId, target.nodeId);
                if (instruction.parallel is { } parallel) parallel.joinId = Address(chapter.chapterId, parallel.joinId);
                if (instruction.join is { } join) join.forkId = Address(chapter.chapterId, join.forkId);
                if (instruction.call is not { } call) continue;
                string destination = call.chapterId.Length > 0 ? call.chapterId : chapter.chapterId;
                if (!byId.TryGetValue(destination, out StoryProgram? targetChapter) || !targetChapter.entries.Any(entry => entry.id == call.entryId))
                    throw new ArgumentException("A chapter call has no matching public entry: " + destination + "/" + call.entryId);
                call.entryId = Address(destination, call.entryId);
                call.chapterId = string.Empty;
            }
        program = new()
        {
            chapterId = project.startChapter,
            fingerprint = project.buildFingerprint.Length > 0 ? project.buildFingerprint : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                string.Join("\n", chapters.OrderBy(static item => item.chapterId, StringComparer.Ordinal).Select(static item => item.chapterId.Length.ToString(CultureInfo.InvariantCulture) + ":" + item.chapterId + item.fingerprint))))),
            variables = variables.Values.OrderBy(static item => item.id, StringComparer.Ordinal).ToArray(),
            instructions = chapters.SelectMany(static item => item.instructions).ToArray(),
            entries = chapters.SelectMany(chapter => chapter.entries.Select(entry => new Model.Story.StoryEntry { id = Address(chapter.chapterId, entry.id), nodeId = Address(chapter.chapterId, entry.nodeId) })).ToArray()
        };
        startEntry = Address(project.startChapter, project.startEntry);
        if (!program.entries.Any(entry => entry.id == startEntry)) throw new ArgumentException("The initial entry is missing.");
    }

    /// <summary>Gets the linked runtime-only program; callers must copy before retaining it.</summary>
    public StoryProgram program { get; }
    /// <summary>Gets the linked initial entry.</summary>
    public string startEntry { get; }
    /// <summary>Resolves an authored public entry without exposing linked instruction indices.</summary>
    /// <param name="chapter">A declared chapter identity.</param>
    /// <param name="entry">A public entry in that chapter.</param>
    /// <returns>The validated runtime entry address.</returns>
    public string ResolveEntry(
        string chapter,
        string entry
    ) {
        string address = Address(chapter, entry);
        return m_chapters.Contains(chapter) && program.entries.Any(item => item.id == address) ? address : throw new InvalidOperationException("Replay entry is unavailable: " + chapter + "/" + entry);
    }
    private string Address(
        string chapter,
        string local
    ) => m_qualified ? chapter.Length.ToString(CultureInfo.InvariantCulture) + ":" + chapter + local : local;
}
