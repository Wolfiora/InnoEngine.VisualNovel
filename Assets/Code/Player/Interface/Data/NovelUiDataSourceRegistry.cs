using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Interface.Data;
using Inno.VisualNovel.UI.Binding;
using Inno.VisualNovel.Model.Extensions;

namespace Inno.VisualNovel.Player.Interface.Data;

/// <summary>Owns explicit current-generation factories for ordinary typed UiDataSource implementations.</summary>
public sealed class NovelUiDataSourceRegistry
{
    private sealed record Registration(UiDataSourceDefinition definition, string fingerprint, string implementation, string factoryCode, Func<NovelUiDataSourceContext, UiDataSource> factory);
    private readonly Dictionary<string, Registration> m_sources = new(StringComparer.Ordinal);
    private bool m_frozen;
    /// <summary>Registers one exact schema and a factory whose durable state derives from the supplied native context.</summary>
    /// <param name="definition">The complete typed metadata published to authoring.</param>
    /// <param name="factory">Creates an independently owned source; IDisposable is released with its view.</param>
    /// <typeparam name="TSource">The concrete implementation whose published code participates in save identity.</typeparam>
    public void Register<TSource>(
        UiDataSourceDefinition definition,
        Func<NovelUiDataSourceContext, TSource> factory
    ) where TSource : UiDataSource {
        if (m_frozen) throw new InvalidOperationException("The UI source registry is a frozen generation snapshot.");
        ArgumentNullException.ThrowIfNull(factory); string fingerprint = UiDataSourceContract.Fingerprint(definition);
        if (typeof(TSource).IsAbstract || typeof(TSource).IsInterface) throw new ArgumentException("Register a concrete UI data source implementation.", nameof(factory));
        if (!m_sources.TryAdd(definition.id, new(UiDataSourceContract.Copy(definition), fingerprint, NovelImplementationFingerprint.TypeCode(typeof(TSource)),
            NovelImplementationFingerprint.FactoryCode(factory), context => factory(context)))) throw new InvalidOperationException("UI source is already registered: " + definition.id);
    }
    /// <summary>Gets detached metadata suitable for publishing native catalogs.</summary>
    public IReadOnlyList<UiDataSourceDefinition> definitions => m_sources.Values.Select(static item => UiDataSourceContract.Copy(item.definition)).ToArray();
    /// <summary>Gets this owner's exact source schemas, concrete implementation modules and factory bindings.</summary>
    public string codeFingerprint => NovelImplementationFingerprint.Combine(m_sources.Values.Select(static item => NovelImplementationFingerprint.Combine(
        [item.definition.id, item.fingerprint, item.implementation, item.factoryCode])));
    /// <summary>Freezes factories and metadata for an owner without creating a process-wide registry.</summary>
    /// <returns>The immutable generation snapshot.</returns>
    public NovelUiDataSourceRegistry Snapshot()
    {
        if (m_frozen) return this;
        var result = new NovelUiDataSourceRegistry { m_frozen = true };
        foreach (var item in m_sources) result.m_sources.Add(item.Key, item.Value);
        return result;
    }
    internal UiDataSource Create(
        UiDataSourceDefinition definition,
        NovelUiDataSourceContext context
    ) {
        if (!m_sources.TryGetValue(definition.id, out var registration)) throw new InvalidOperationException("UI data source implementation is missing: " + definition.id);
        if (registration.fingerprint != UiDataSourceContract.Fingerprint(definition)) throw new InvalidOperationException("UI data source metadata differs from its registration: " + definition.id);
        var source = registration.factory(context) ?? throw new InvalidOperationException("UI source factory returned no implementation: " + definition.id);
        if (NovelImplementationFingerprint.TypeCode(source.GetType()) != registration.implementation)
        {
            (source as IDisposable)?.Dispose(); throw new InvalidOperationException("UI source factory changed its concrete implementation: " + definition.id);
        }
        return source;
    }
}
