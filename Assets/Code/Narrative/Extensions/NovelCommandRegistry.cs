using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Extensions;

namespace Inno.VisualNovel.Narrative.Extensions;

/// <summary>Owns explicit generation-local factories without static caches or runtime reflection discovery.</summary>
public sealed class NovelCommandRegistry
{
    private sealed record Registration(NovelCommandDefinition definition, string fingerprint, string implementation, string factoryCode, Func<NovelCommand> factory);
    private readonly Dictionary<string, Registration> m_items = new(StringComparer.Ordinal);
    private bool m_frozen;
    /// <summary>Registers a side-effect-free factory and rejects duplicate stable identities.</summary>
    /// <param name="factory">Creates an independent command handler for every invocation.</param>
    public void Register(Func<NovelCommand> factory)
    {
        if (m_frozen) throw new InvalidOperationException("This registry is a session-owned frozen snapshot.");
        ArgumentNullException.ThrowIfNull(factory);
        using NovelCommand prototype = factory() ?? throw new InvalidOperationException("The command factory returned no handler.");
        var definition = CommandContract.Copy(prototype.definition); string fingerprint = CommandContract.Fingerprint(definition);
        if (!m_items.TryAdd(definition.id, new(definition, fingerprint, NovelImplementationFingerprint.TypeCode(prototype.GetType()), NovelImplementationFingerprint.FactoryCode(factory), factory)))
            throw new InvalidOperationException("Command identity is already registered: " + definition.id);
    }
    /// <summary>Gets detached schemas suitable for copying into native game authoring data.</summary>
    public IReadOnlyList<NovelCommandDefinition> definitions => m_items.Values.Select(static item => CommandContract.Copy(item.definition)).ToArray();
    /// <summary>Gets the exact schemas, implementation modules and factory bindings included in this owner's save identity.</summary>
    public string codeFingerprint => NovelImplementationFingerprint.Combine(m_items.Values.Select(static item => NovelImplementationFingerprint.Combine(
        [item.definition.id, item.fingerprint, item.implementation, item.factoryCode])));
    /// <summary>Creates an immutable session snapshot that retains factories only for the current generation.</summary>
    /// <returns>A frozen owner-local registry.</returns>
    public NovelCommandRegistry Snapshot()
    {
        if (m_frozen) return this;
        var result = new NovelCommandRegistry { m_frozen = true };
        foreach (var pair in m_items) result.m_items.Add(pair.Key, pair.Value);
        return result;
    }
    internal NovelCommand Create(
        NovelCommandDefinition definition,
        bool preview
    ) {
        string fingerprint = CommandContract.Fingerprint(definition);
        if (!m_items.TryGetValue(definition.id, out var item)) throw new InvalidOperationException("Missing command implementation: " + definition.id + ". Neutral node data was retained.");
        if (item.fingerprint != fingerprint) throw new InvalidOperationException("Command schema differs from the registered implementation: " + definition.id);
        if (preview && (!definition.capabilities.HasFlag(CommandCapabilities.Preview) || definition.capabilities.HasFlag(CommandCapabilities.ExternalEffects)))
            throw new InvalidOperationException("This command is disabled in preview: " + definition.id);
        NovelCommand candidate = item.factory() ?? throw new InvalidOperationException("The command factory returned no handler: " + definition.id);
        try
        {
            if (CommandContract.Fingerprint(candidate.definition) != fingerprint) throw new InvalidOperationException("Command factory produced a different contract: " + definition.id);
            if (NovelImplementationFingerprint.TypeCode(candidate.GetType()) != item.implementation) throw new InvalidOperationException("Command factory changed its implementation: " + definition.id);
            return candidate;
        }
        catch { candidate.Dispose(); throw; }
    }
}
