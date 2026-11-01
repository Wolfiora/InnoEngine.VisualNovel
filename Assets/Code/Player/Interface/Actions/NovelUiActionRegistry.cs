using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Interface.Actions;
using Inno.VisualNovel.Model.Extensions;

namespace Inno.VisualNovel.Player.Interface.Actions;

/// <summary>Owns explicit current-generation UI action factories without static discovery or persistent delegates.</summary>
public sealed class NovelUiActionRegistry
{
    private sealed record Registration(UiActionDefinition definition, string fingerprint, string implementation, string factoryCode, Func<NovelUiAction> factory);
    private readonly Dictionary<string, Registration> m_actions = new(StringComparer.Ordinal);
    private bool m_frozen;
    /// <summary>Registers a side-effect-free factory for one stable action, validating and freezing its neutral schema.</summary>
    /// <param name="factory">Creates an independently disposable handler.</param>
    public void Register(Func<NovelUiAction> factory)
    {
        if (m_frozen) throw new InvalidOperationException("The UI action registry is a frozen session snapshot.");
        ArgumentNullException.ThrowIfNull(factory);
        using var prototype = factory() ?? throw new InvalidOperationException("The UI action factory returned no handler.");
        var definition = UiActionContract.Copy(prototype.definition); string fingerprint = UiActionContract.Fingerprint(definition);
        if (!m_actions.TryAdd(definition.id, new(definition, fingerprint, NovelImplementationFingerprint.TypeCode(prototype.GetType()), NovelImplementationFingerprint.FactoryCode(factory), factory)))
            throw new InvalidOperationException("UI action identity is already registered: " + definition.id);
    }
    /// <summary>Gets independently editable native action schemas for publishing a catalog.</summary>
    public IReadOnlyList<UiActionDefinition> definitions => m_actions.Values.Select(static registration => UiActionContract.Copy(registration.definition)).ToArray();
    /// <summary>Gets this owner's exact action schemas, implementation modules and registered factory bindings.</summary>
    public string codeFingerprint => NovelImplementationFingerprint.Combine(m_actions.Values.Select(static item => NovelImplementationFingerprint.Combine(
        [item.definition.id, item.fingerprint, item.implementation, item.factoryCode])));
    /// <summary>Freezes registrations for a single generation without retaining process-wide state.</summary>
    /// <returns>An immutable registry snapshot.</returns>
    public NovelUiActionRegistry Snapshot()
    {
        if (m_frozen) return this;
        var snapshot = new NovelUiActionRegistry { m_frozen = true };
        foreach (var registration in m_actions) snapshot.m_actions.Add(registration.Key, registration.Value);
        return snapshot;
    }
    internal NovelUiAction Create(UiActionDefinition definition)
    {
        string fingerprint = UiActionContract.Fingerprint(definition);
        if (!m_actions.TryGetValue(definition.id, out var registration)) throw new InvalidOperationException("UI action implementation is missing: " + definition.id);
        if (registration.fingerprint != fingerprint) throw new InvalidOperationException("UI action metadata differs from its implementation: " + definition.id);
        var handler = registration.factory() ?? throw new InvalidOperationException("The UI action factory returned no handler.");
        try
        {
            if (UiActionContract.Fingerprint(handler.definition) != fingerprint) throw new InvalidOperationException("The UI action factory changed its contract: " + definition.id);
            if (NovelImplementationFingerprint.TypeCode(handler.GetType()) != registration.implementation) throw new InvalidOperationException("The UI action factory changed its implementation: " + definition.id);
            return handler;
        }
        catch { handler.Dispose(); throw; }
    }
}
