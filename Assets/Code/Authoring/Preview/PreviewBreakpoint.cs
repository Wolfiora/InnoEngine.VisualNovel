using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Narrative.Debugging;

namespace Inno.VisualNovel.Authoring.Preview;

/// <summary>Stores a source breakpoint in the native editor-only preview case.</summary>
[GenerateSerializationConverter]
public sealed class PreviewBreakpoint : ISerializable
{
    /// <summary>Gets or sets the authored chapter identity.</summary>
    [SerializableProperty] public string chapter { get; set; } = string.Empty;
    /// <summary>Gets or sets the authored graph node identity.</summary>
    [SerializableProperty] public string node { get; set; } = string.Empty;
    /// <summary>Gets or sets whether this source breakpoint participates in the debug run.</summary>
    [SerializableProperty] public bool enabled { get; set; } = true;
    /// <summary>Gets or sets whether the pure Boolean condition must match.</summary>
    [SerializableProperty] public bool conditional { get; set; }
    /// <summary>Gets or sets the visual condition, ignored when conditional is false.</summary>
    [SerializableProperty] public ExpressionDocument condition { get; set; } = new() { literal = NovelValue.Boolean(true) };
    /// <summary>Creates the immutable runtime debug condition without retaining this editable declaration.</summary>
    /// <returns>A pure stable source breakpoint.</returns>
    public StoryBreakpoint Build() => new(chapter, node, conditional ? condition : null);
    /// <summary>Copies the full native authoring declaration independently of a live debug owner.</summary>
    /// <returns>An independently editable breakpoint.</returns>
    public PreviewBreakpoint Copy() => new() { chapter = chapter, node = node, enabled = enabled, conditional = conditional, condition = ExpressionOperations.Copy(condition) };
}
