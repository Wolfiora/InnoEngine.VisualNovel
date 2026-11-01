using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Narrative.Values;

namespace Inno.VisualNovel.Player.Interface;

/// <summary>Owns typed UI fields separately from narrative variables, global progress and transient control focus.</summary>
public sealed class NovelPageStateStore
{
    private readonly Dictionary<string, VariableDefinition[]> m_definitions;
    private Dictionary<string, Page> m_pages;
    private string m_activeRole = string.Empty;

    /// <summary>Freezes the union of fallback and theme page schemas, rejecting conflicting declarations of the same field.</summary>
    /// <param name="screens">Explicit role/page bindings from the game and its themes.</param>
    public NovelPageStateStore(IEnumerable<NovelScreenBinding> screens)
    {
        ArgumentNullException.ThrowIfNull(screens);
        var groups = new Dictionary<string, Dictionary<string, VariableDefinition>>(StringComparer.Ordinal);
        foreach (var binding in screens)
        {
            if (string.IsNullOrWhiteSpace(binding.role) || binding.role.Length > 128 || binding.screen is not { isMissing: false } screen)
                throw new InvalidOperationException("Page state requires a bounded shell role and an available screen.");
            UiStateRules.Validate(screen.stateVariables);
            if (!groups.TryGetValue(binding.role, out var fields))
            {
                if (groups.Count >= 256) throw new InvalidOperationException("Page state is limited to 256 shell roles.");
                fields = new(StringComparer.Ordinal); groups.Add(binding.role, fields);
            }
            foreach (var field in screen.stateVariables)
            {
                if (fields.TryGetValue(field.id, out var previous))
                {
                    if (!UiStateRules.Equivalent(previous, field)) throw new InvalidOperationException("Theme page state conflicts with its role contract: " + binding.role + "/" + field.id);
                }
                else fields.Add(field.id, VariableValueRules.Copy(field));
            }
            if (fields.Count > 256) throw new InvalidOperationException("Theme variants exceed the 256-field page budget: " + binding.role);
        }
        if (groups.Sum(static group => group.Value.Count) > 4096) throw new InvalidOperationException("A game supports at most 4096 page state fields.");
        m_definitions = groups.ToDictionary(static group => group.Key, static group => group.Value.Values.ToArray(), StringComparer.Ordinal);
        m_pages = CreatePages();
    }

    private NovelPageStateStore(Dictionary<string, VariableDefinition[]> definitions)
    {
        m_definitions = definitions;
        m_pages = CreatePages();
    }

    /// <summary>Resets the visited page's Local fields when navigation changes the active shell role.</summary>
    /// <param name="role">The newly displayed role; logic-only hosts may omit its visual page.</param>
    public void Enter(string role)
    {
        if (m_activeRole == role) return;
        if (m_pages.TryGetValue(m_activeRole, out var previous)) previous.ResetLocal();
        if (m_pages.TryGetValue(role, out var next)) next.ResetLocal();
        m_activeRole = role;
    }

    /// <summary>Reads a detached page field without exposing mutable store internals.</summary>
    /// <param name="role">The owning shell role.</param>
    /// <param name="id">The page-local field identity, without the page. binding prefix.</param>
    /// <param name="value">Receives a defensive value copy.</param>
    /// <returns>Whether the field is declared.</returns>
    public bool TryRead(
        string role,
        string id,
        out NovelValue value
    ) {
        if (m_pages.TryGetValue(role, out var page) && page.Find(id) is { } store) { value = store.Get(id); return true; }
        value = NovelValue.Boolean(false); return false;
    }

    /// <summary>Validates a page control write using the same typed variable store as narrative scopes.</summary>
    /// <param name="role">The owning shell role.</param>
    /// <param name="id">The declared local field identity.</param>
    /// <param name="value">A complete bounded value of the declared type.</param>
    /// <returns>Whether the declared field accepted the value.</returns>
    public bool TryWrite(
        string role,
        string id,
        NovelValue value
    ) {
        if (!m_pages.TryGetValue(role, out var page) || page.Find(id) is not { } store) return false;
        try { UiStateRules.ValidateValue(value); store.Set(id, value); return true; }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException) { return false; }
    }

    /// <summary>Restores one declared field to its authored default without touching narrative or other page state.</summary>
    /// <param name="role">The owning shell role.</param>
    /// <param name="id">The exact field identity.</param>
    public void Reset(
        string role,
        string id
    ) {
        var definition = m_definitions.GetValueOrDefault(role)?.SingleOrDefault(field => field.id == id)
            ?? throw new InvalidOperationException("The page reset target is undeclared: " + role + "/" + id);
        m_pages[role].Find(id)!.Set(id, definition.initialValue);
    }

    /// <summary>Resets every page field for a fresh game, retaining its frozen declarations.</summary>
    public void ResetAll() { m_pages = CreatePages(); m_activeRole = string.Empty; }

    /// <summary>Captures only Game-scoped UI values, ordered by shell role and field identity.</summary>
    /// <returns>Detached native DTOs without control focus, popup or transient Local fields.</returns>
    public UiPageStateSnapshot[] Capture() => m_pages.Where(static item => item.Value.hasGameFields).OrderBy(static item => item.Key, StringComparer.Ordinal)
        .Select(static item => new UiPageStateSnapshot { role = item.Key, values = item.Value.game.Capture() }).ToArray();

    /// <summary>Prepares an independent exact-schema candidate, leaving the active store unchanged on any error.</summary>
    /// <param name="snapshot">All persistent roles and fields from this exact content build.</param>
    /// <returns>A validated candidate with transient values reset to authored defaults.</returns>
    public NovelPageStateStore Fork(UiPageStateSnapshot[] snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var candidate = new NovelPageStateStore(m_definitions);
        if (snapshot.Length != candidate.m_pages.Count(static item => item.Value.hasGameFields))
            throw new InvalidOperationException("Page state snapshot does not match the current game's persistent roles.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var page in snapshot)
        {
            if (page is null || page.values is null || page.values.Length > 256 || page.values.Any(static value => value is null)
                || !seen.Add(page.role) || !candidate.m_pages.TryGetValue(page.role, out var destination) || !destination.hasGameFields)
                throw new InvalidOperationException("Page state snapshot contains a duplicate or undeclared persistent role.");
            foreach (var value in page.values) UiStateRules.ValidateValue(value.value);
            destination.game.Restore(page.values);
        }
        return candidate;
    }

    private Dictionary<string, Page> CreatePages() => m_definitions.ToDictionary(static item => item.Key, static item => new Page(item.Value), StringComparer.Ordinal);

    private sealed class Page(VariableDefinition[] fields)
    {
        private VariableStore m_local = new(fields.Where(static field => field.scope == VariableScope.Local));
        internal VariableStore game { get; } = new(fields.Where(static field => field.scope == VariableScope.Game));
        internal bool hasGameFields { get; } = fields.Any(static field => field.scope == VariableScope.Game);
        internal VariableStore? Find(string id) => game.Contains(id) ? game : m_local.Contains(id) ? m_local : null;
        internal void ResetLocal() => m_local = new(fields.Where(static field => field.scope == VariableScope.Local));
    }
}
