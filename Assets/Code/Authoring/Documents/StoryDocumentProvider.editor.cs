using System;
using InnoEditor.Interactions;

namespace Inno.VisualNovel.Authoring.Documents;

internal sealed class StoryDocumentProvider(StoryDocumentsModule owner) : EditorDocumentProvider
{
    public override string id => "inno.vn.story-document";
    public override bool CanOpen(string assetPath) => assetPath.EndsWith(".ivnstory", StringComparison.OrdinalIgnoreCase);
    public override void Open(EditorDocumentContext context) => owner.OpenSource(context);
    public override bool Save(EditorDocumentContext context) => owner.SaveSource(context);
    public override bool Revert(EditorDocumentContext context) => owner.RevertSource(context);
    public override void Close(EditorDocumentContext context) => owner.CloseSource(context);
}
