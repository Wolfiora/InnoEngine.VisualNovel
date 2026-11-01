using InnoEngine.Serialization;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Authoring.StoryEditing;

[GenerateSerializationConverter]
internal sealed class StoryClipboardState : ISerializable
{
    [SerializableProperty] public string chapterId { get; set; } = string.Empty;
    [SerializableProperty] public StoryGraph graph { get; set; } = new();
    [SerializableProperty] public VariableDefinition[] variables { get; set; } = [];
}
