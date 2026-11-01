using System;
using System.Linq;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Authoring.Diagnostics;

namespace Inno.VisualNovel.Authoring.Variables;

/// <summary>Assigns one explicit shared declaration authority while preserving native identity and reference-free chapter History.</summary>
public static class StoryVariableBindingService
{
    /// <summary>Attaches a persistent library, optionally handing over only exactly equivalent chapter declarations.</summary>
    /// <param name="draft">The chapter transaction candidate.</param>
    /// <param name="table">The explicitly selected canonical native library.</param>
    /// <param name="handOverMatching">Whether the author explicitly allows moving identical defaults and lifetimes to the library.</param>
    public static void Attach(
        StoryDraftState draft,
        VariableTableAsset table,
        bool handOverMatching = false
    ) {
        StoryVariableDeclarations.Validate(table);
        if (table.identity.persistentId == Guid.Empty || table.assetPath.localPath.Length == 0)
            throw new AuthoringValidationException($"请选择已导入的原生共享变量库。");
        if (draft.variableTableId != Guid.Empty && draft.variableTableId != table.identity.persistentId)
            throw new AuthoringValidationException($"更换变量库前，请先解除引用并保留现有声明。");
        var shared = table.variables.ToDictionary(static item => item.id, StringComparer.Ordinal);
        foreach (var local in draft.variables.Where(item => shared.ContainsKey(item.id)))
            if (!handOverMatching || !StoryVariableDeclarations.Equivalent(local, shared[local.id]))
                throw new AuthoringValidationException($"变量归属冲突：{local.id}。仅能在明确选择后移交默认值、作用域与成员完全相同的声明。");
        var retained = draft.variables.Where(item => !shared.ContainsKey(item.id)).Select(VariableValueRules.Copy).ToArray();
        _ = StoryVariableDeclarations.Merge(retained, table);
        draft.variables = retained; draft.variableTableId = table.identity.persistentId; draft.variableTablePath = table.assetPath.ToString();
    }
    /// <summary>Detaches the library only after materializing all shared declarations as independently owned chapter data.</summary>
    /// <param name="draft">The chapter transaction candidate; missing libraries cannot be silently discarded.</param>
    public static void Detach(StoryDraftState draft)
    {
        if (draft.variableTableId == Guid.Empty) return;
        var declarations = draft.GetVariables();
        draft.variables = declarations; draft.variableTableId = Guid.Empty; draft.variableTablePath = string.Empty;
    }
    /// <summary>Explicitly clears an unavailable library identity without deleting authored nodes or guessing lost declaration defaults.</summary>
    /// <param name="draft">The chapter candidate whose missing reference remains recoverable through native History.</param>
    public static void ClearMissing(StoryDraftState draft)
    {
        if (draft.variableTableId == Guid.Empty) return;
        if (draft.ResolveVariableTable() is { isMissing: false }) throw new AuthoringValidationException($"可用变量库应通过解除引用并保留声明处理。");
        draft.variableTableId = Guid.Empty; draft.variableTablePath = string.Empty;
    }
}
