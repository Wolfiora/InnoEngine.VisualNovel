using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Extensions;
using Inno.VisualNovel.Model.Story.Nodes.Extensions;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Narrative.Values;

namespace Inno.VisualNovel.Narrative.Extensions;

internal sealed class NovelCommandExecution : IDisposable
{
    private readonly NovelCommand m_command;
    private readonly CommandNode m_node;
    private readonly string m_nodeId;
    private readonly VariableState[] m_arguments;
    private readonly NovelCommandContext m_context;
    private NovelCommandState m_state;
    private bool m_closed;
    internal NovelValue? result => m_context.result?.Copy();
    internal bool canSave => m_node.definition.capabilities.HasFlag(CommandCapabilities.Save);
    internal bool canCancel => m_node.definition.capabilities.HasFlag(CommandCapabilities.Cancel);
    internal NovelCommandExecution(
        NovelCommandRegistry registry,
        CommandNode node,
        string nodeId,
        Func<string, NovelValue> variables,
        bool preview,
        CommandInvocationState? restored = null
    ) {
        m_node = CommandContract.Copy(node); m_nodeId = nodeId;
        var values = new Dictionary<string, NovelValue>(StringComparer.Ordinal);
        if (restored is not null && (restored.nodeId != nodeId || restored.contract != CommandContract.Fingerprint(node.definition)
            || !node.definition.capabilities.HasFlag(CommandCapabilities.Save))) throw new InvalidOperationException("Command snapshot cannot resume this invocation.");
        foreach (var parameter in node.definition.parameters)
        {
            NovelValue value = restored is null
                ? node.arguments.FirstOrDefault(argument => argument.id == parameter.id) is { } argument ? new ExpressionEvaluator(variables).Evaluate(argument.value) : parameter.defaultValue.Copy()
                : restored.arguments.SingleOrDefault(argument => argument.id == parameter.id)?.value.Copy() ?? throw new InvalidOperationException("Restored command argument is missing: " + parameter.id);
            parameter.Validate(value); values.Add(parameter.id, value);
        }
        if (restored is not null && restored.arguments.Length != values.Count) throw new InvalidOperationException("Restored command arguments differ from their contract.");
        m_arguments = values.Select(static pair => new VariableState { id = pair.Key, value = pair.Value.Copy() }).ToArray();
        m_context = new(variables, values); m_state = restored?.state.Copy() ?? new();
        m_command = registry.Create(node.definition, preview);
        try { if (restored is not null) m_command.PrepareRestore(m_state.Copy()); }
        catch { m_command.Dispose(); throw; }
    }
    internal bool Tick(double seconds)
    {
        ObjectDisposedException.ThrowIf(m_closed, this);
        m_context.Enter();
        try
        {
            m_state.elapsed += seconds; bool complete = m_command.Execute(m_context, m_state, seconds); m_state.started = true; _ = m_state.Copy();
            if (complete && m_node.definition.hasResult)
                new CommandParameter { id = "result", defaultValue = m_node.definition.resultDefault, members = m_node.definition.resultMembers }.Validate(result ?? throw new InvalidOperationException("Command completed without its required typed result."));
            return complete;
        }
        finally { m_context.Leave(); }
    }
    internal CommandInvocationState Capture()
    {
        if (!canSave) throw new InvalidOperationException("Saving is unavailable while this command is active: " + m_node.definition.id);
        return new() { nodeId = m_nodeId, contract = CommandContract.Fingerprint(m_node.definition), arguments = m_arguments.Select(static item => new VariableState { id = item.id, value = item.value.Copy() }).ToArray(), state = m_state.Copy() };
    }
    internal void Complete() { if (m_closed) return; m_closed = true; m_command.Dispose(); }
    public void Dispose()
    {
        if (m_closed) return; m_closed = true;
        try { m_command.Cancel(m_state.Copy()); }
        finally { m_command.Dispose(); }
    }
}
