using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Extensions;
using Inno.VisualNovel.Model.Interface.Actions;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.UI.Binding;
using Inno.VisualNovel.UI.Input;

namespace Inno.VisualNovel.Player.Interface.Actions;

internal sealed class NovelUiActionHost : IDisposable
{
    private readonly Dictionary<string, UiActionDefinition> m_definitions;
    private readonly Dictionary<string, NovelUiAction> m_queries = new(StringComparer.Ordinal);
    private readonly NovelUiActionRegistry m_registry;
    private readonly NovelSession m_session;
    private readonly NovelUiActionContext m_queryContext, m_writeContext;
    internal string error { get; private set; } = string.Empty;
    internal NovelUiActionHost(
        UiActionDefinition[] definitions,
        NovelUiActionRegistry? registry,
        NovelSession session,
        UiDataSource source,
        Action<string> openPage
    ) {
        UiActionContract.ValidateCatalog(definitions);
        m_definitions = definitions.ToDictionary(static definition => definition.id, UiActionContract.Copy, StringComparer.Ordinal);
        m_registry = (registry ?? new()).Snapshot(); m_session = session;
        m_queryContext = new(source, openPage, session.isPreview, true); m_writeContext = new(source, openPage, session.isPreview, false);
    }
    internal bool CanInvoke(UiActionRequest request)
    {
        if (!request.action.StartsWith("custom.", StringComparison.Ordinal)) return true;
        try
        {
            var definition = Definition(request.action); GuardPreview(definition);
            return Query(definition).CanExecute(m_queryContext, Argument(definition, request));
        }
        catch (Exception failure) when (failure is not OutOfMemoryException) { Fail(request, failure); return false; }
    }
    internal bool IsSelected(UiActionRequest request)
    {
        if (!request.action.StartsWith("custom.", StringComparison.Ordinal)) return false;
        try
        {
            var definition = Definition(request.action); GuardPreview(definition);
            return Query(definition).IsSelected(m_queryContext, Argument(definition, request));
        }
        catch (Exception failure) when (failure is not OutOfMemoryException) { Fail(request, failure); return false; }
    }
    internal void Invoke(UiActionRequest request)
    {
        try
        {
            var definition = Definition(request.action); GuardPreview(definition);
            var value = Argument(definition, request);
            if (!Query(definition).CanExecute(m_queryContext, value.Copy())) return;
            using var handler = m_registry.Create(definition);
            if (!definition.capabilities.HasFlag(CommandCapabilities.Rollback)) m_session.story?.BlockRollback("UI action: " + definition.id);
            handler.Execute(m_writeContext, value); error = string.Empty;
        }
        catch (Exception failure) when (failure is not OutOfMemoryException) { Fail(request, failure); }
    }
    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var handler in m_queries.Values)
            try { handler.Dispose(); } catch (Exception failure) when (failure is not OutOfMemoryException) { error = failure.Message; }
        m_queries.Clear();
    }
    private UiActionDefinition Definition(string id) => m_definitions.TryGetValue(id, out var definition) ? definition
        : throw new InvalidOperationException("The UI action has no declared native schema: " + id);
    private NovelUiAction Query(UiActionDefinition definition)
    {
        if (!m_queries.TryGetValue(definition.id, out var handler)) { handler = m_registry.Create(definition); m_queries.Add(definition.id, handler); }
        return handler;
    }
    private void GuardPreview(UiActionDefinition definition)
    {
        if (m_session.isPreview && (!definition.capabilities.HasFlag(CommandCapabilities.Preview) || definition.capabilities.HasFlag(CommandCapabilities.ExternalEffects)))
            throw new InvalidOperationException("The UI action is disabled in preview: " + definition.id);
    }
    private void Fail(
        UiActionRequest request,
        Exception error
    ) => this.error = request.action + (request.ownerId.Length > 0 ? " @ " + request.ownerId : "") + ": " + error.Message;
    private static Model.Values.NovelValue Argument(
        UiActionDefinition definition,
        UiActionRequest request
    ) {
        if (request.binding.Length > 0 && request.value is null) throw new InvalidOperationException("UI action argument binding is unavailable: " + request.binding);
        return UiActionContract.ResolveArgument(definition, request.argument, request.value);
    }
}
