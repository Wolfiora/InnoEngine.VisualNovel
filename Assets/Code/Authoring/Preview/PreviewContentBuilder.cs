using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Authoring.Preview;

/// <summary>Compiles a draft into an isolated project shell while preserving the authored graph and imported context.</summary>
public static class PreviewContentBuilder
{
    /// <summary>Builds production-consumable content without mutating canonical assets or the open document.</summary>
    /// <param name="context">The explicit project's stage, audio, screens, variables and extensions.</param>
    /// <param name="draft">The document being previewed.</param>
    /// <param name="compiler">The same compiler used by import and publishing.</param>
    /// <param name="test">Explicit preview semantics and preconditions.</param>
    /// <param name="variableTable">Optional captured shared declarations owned by the same preview preparation.</param>
    /// <returns>An in-memory project and its resolved public entry.</returns>
    public static (NovelProjectAsset project, string chapter, string entry) Build(
        NovelProjectAsset context,
        StoryDraftState draft,
        StoryCompiler compiler,
        NovelPreviewCaseAsset test,
        VariableTableAsset? variableTable = null
    ) {
        PreviewCaseRules.Validate(test);
        bool include = test.mode == PreviewRunMode.Local || context.chapters.Any(item => item.chapterId == draft.chapterId);
        if (!include && test.mode == PreviewRunMode.Route) throw new InvalidOperationException("The route target chapter is not part of the chosen game. Use local preview or associate it with the game first.");
        StoryAsset chapter = draft.ToAsset(); string entry = test.entryId;
        if (variableTable is not null)
        {
            if (draft.variableTableId == Guid.Empty) throw new InvalidOperationException("A preview cannot substitute a library for an unassigned chapter.");
            chapter.variableTable = variableTable;
        }
        if (test.mode == PreviewRunMode.Local)
        {
            entry = "inno.preview.local";
            if (chapter.entries.Any(item => item.id == entry)) throw new InvalidOperationException("The reserved preview entry conflicts with an authored entry.");
            chapter.entries = [.. chapter.entries, new() { id = entry, nodeId = test.nodeId }];
        }
        if (include) Compile(chapter, compiler);
        if (test.mode == PreviewRunMode.Route && !chapter.program!.instructions.Any(item => item.nodeId == test.nodeId
            && (item.definitionId is StoryNodeIds.dialogue or StoryNodeIds.choice or StoryNodeIds.input || item.wait is { waitForInput: true })))
            throw new InvalidOperationException("Route preview requires a dialogue, choice, player input or explicit advance-wait target. Use local preview for an immediate presentation node.");
        string startChapter = test.mode == PreviewRunMode.Local ? chapter.chapterId : test.chapterId.Length > 0 ? test.chapterId : context.startChapter;
        if (test.mode != PreviewRunMode.Local && test.entryId.Length == 0) entry = context.startEntry;
        var project = NovelProjectCopy.Copy(context);
        project.gameId = context.gameId + "/preview";
        project.startChapter = startChapter; project.startEntry = entry;
        project.chapters = include ? [.. context.chapters.Where(item => item.chapterId != chapter.chapterId), chapter] : context.chapters.ToArray();
        project.autoSaveOnChoices = false;
        project.buildFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(context.buildFingerprint + "\n" + startChapter + "\n" + entry + "\n"
            + string.Join("\n", project.chapters.OrderBy(static item => item.chapterId, StringComparer.Ordinal).Select(static item => item.program?.fingerprint ?? "")))));
        return (project, startChapter, entry);
    }
    internal static StoryAsset Compile(
        StoryAsset chapter,
        StoryCompiler compiler
    ) {
        StoryCompilationResult compiled = compiler.Compile(chapter);
        if (!compiled.success) throw new InvalidOperationException(string.Join("\n", compiled.diagnostics.Where(static item => item.severity == InnoEngine.Diagnostics.DiagnosticSeverity.Error)
            .Select(item => item.code + " [" + chapter.chapterId + "/" + item.nodeId + "]: " + item.message)));
        chapter.program = compiled.program; return chapter;
    }
}
