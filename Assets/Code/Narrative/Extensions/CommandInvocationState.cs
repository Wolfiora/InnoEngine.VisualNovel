using InnoEngine.Serialization;
using Inno.VisualNovel.Narrative.Values;

namespace Inno.VisualNovel.Narrative.Extensions;

/// <summary>Captures an invocation's stable identity, entry arguments and neutral continuation.</summary>
[GenerateSerializationConverter]
public sealed class CommandInvocationState : ISerializable
{
    /// <summary>Gets or sets the owning compiled instruction identity.</summary>
    [SerializableProperty] public string nodeId { get; set; } = string.Empty;
    /// <summary>Gets or sets the exact command contract digest.</summary>
    [SerializableProperty] public string contract { get; set; } = string.Empty;
    /// <summary>Gets or sets arguments resolved once at entry, independently of later global changes.</summary>
    [SerializableProperty] public VariableState[] arguments { get; set; } = [];
    /// <summary>Gets or sets the implementation-independent execution payload.</summary>
    [SerializableProperty] public NovelCommandState state { get; set; } = new();
}
