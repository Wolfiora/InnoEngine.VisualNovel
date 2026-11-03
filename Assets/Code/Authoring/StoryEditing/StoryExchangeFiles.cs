using System;
using System.IO;
using System.Text;
using System.Linq;
using InnoEngine.Graphs;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Authoring.ScriptProjection;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Authoring.Diagnostics;

namespace Inno.VisualNovel.Authoring.StoryEditing;

/// <summary>Reads explicitly selected external dialogue exchanges without changing their source files.</summary>
public static class StoryExchangeFiles
{
    /// <summary>Reads strict UTF-8 CSV, TSV or plain text with an explicit input size budget.</summary>
    /// <param name="path">The author-selected source file.</param>
    /// <returns>The exchange text for a separate review operation.</returns>
    public static string Read(string path)
    {
        if (new FileInfo(path).Length > 32 * 1024 * 1024) throw new AuthoringValidationException($"The script file exceeds the 32 MiB read budget.");
        return File.ReadAllText(path, new UTF8Encoding(false, true));
    }
    /// <summary>Exports a finite proofreading draft retaining original nodes and explicit branch targets.</summary>
    /// <param name="projectDirectory">The current editor project; output remains within its artifacts directory.</param>
    /// <param name="draft">The current unsaved chapter.</param>
    /// <param name="codec">The document generation's native settings decoder.</param>
    /// <param name="documentId">The shared document identity used for a safe export filename.</param>
    /// <param name="characters">Optional default speaker names from the reviewed game.</param>
    /// <returns>The project-owned UTF-8 proofreading file; the native graph remains authoritative.</returns>
    public static string ExportReading(
        string projectDirectory,
        StoryDraftState draft,
        StoryNodeDataCodec codec,
        Guid documentId,
        CharacterAsset[]? characters = null
    ) {
        string root = Path.GetFullPath(projectDirectory), directory = Path.GetFullPath(Path.Combine(root, "artifacts", "Exports"));
        if (!directory.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new AuthoringValidationException($"The reading-script export must remain inside the current project.");
        var output = new StringBuilder(); output.AppendLine(draft.title).AppendLine("Reading draft: each node appears once. Targets are stable node IDs; loops are not expanded. Authored variable placeholders are retained.");
        foreach (var row in ScriptProjectionBuilder.Build(draft))
        {
            var node = draft.graph.document.FindNode(new(row.nodeId))!;
            output.AppendLine().Append('[').Append(row.nodeId).Append("] ").AppendLine(node.definitionId);
            if (node.definitionId == StoryNodeIds.dialogue)
            {
                var line = codec.Read<DialogueNode>(node); string speaker = line.speaker.Length > 0 ? line.speaker : characters?.FirstOrDefault(character => character.resourceId == line.characterId)?.displayName ?? "";
                output.Append(speaker.Length > 0 ? speaker + "：" : "Narration: ").AppendLine(line.text).Append("Text ID: ").AppendLine(line.textId);
                if (line.voiceCueId.Length > 0) output.Append("Voice: ").AppendLine(line.voiceCueId);
            }
            else if (node.definitionId == StoryNodeIds.choice)
            {
                var choice = codec.Read<ChoiceNode>(node); output.AppendLine(choice.prompt);
                foreach (var option in choice.options) output.Append("- ").Append(option.text).Append(" [").Append(option.id).AppendLine("]");
            }
            foreach (var edge in row.targets) output.Append("  ").Append(edge.output.portId.value).Append(" → ").AppendLine(edge.input.nodeId.value);
        }
        Directory.CreateDirectory(directory); string path = Path.Combine(directory, "Reading-" + documentId.ToString("N") + ".txt");
        File.WriteAllText(path, output.ToString(), new UTF8Encoding(false)); return path;
    }
}
