using InnoEditor.Interactions;

namespace Inno.VisualNovel.Authoring.Documents;

internal sealed class ResourceDocumentProvider(NativeDocumentStore store) : EditorDocumentProvider
{
    public override string id => "inno.vn.resource-document";
    public override bool CanOpen(string assetPath) => store.CanOpen(assetPath);
    public override void Open(EditorDocumentContext context) => store.OpenSource(context);
    public override bool Save(EditorDocumentContext context) => store.SaveSource(context);
    public override bool Revert(EditorDocumentContext context) => store.RevertSource(context);
    public override void Close(EditorDocumentContext context) => store.CloseSource(context);
}
