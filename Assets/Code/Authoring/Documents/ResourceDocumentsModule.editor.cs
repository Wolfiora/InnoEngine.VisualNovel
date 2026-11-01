using System;
using InnoEngine.Assets;
using InnoEngine.Serialization;
using InnoEditor.Assets;
using InnoEditor.Core;
using InnoEditor.Interactions;
using Inno.VisualNovel.Compiler;

namespace Inno.VisualNovel.Authoring.Documents;

/// <summary>Owns editable native presentation resources through engine documents and History.</summary>
[EditorModule("inno.vn.resource-documents", order: 421)]
public sealed class ResourceDocumentsModule : EditorModule
{
    /// <summary>Identifies reload-neutral resource history operations.</summary>
    public const string historyKind = "inno.vn.resource-edit";
    private readonly NativeDocumentStore m_store;
    private readonly EditorInteractions m_interactions;
    private IDisposable? m_provider;
    /// <summary>Binds native source editing to the current editor generation.</summary>
    /// <param name="serialization">The native serializer owner.</param>
    /// <param name="interactions">The engine document and History coordinator.</param>
    public ResourceDocumentsModule(
        SerializationRegistry serialization,
        EditorInteractions interactions
    ) {
        m_interactions = interactions;
        m_store = new(historyKind, new EditorStorySerialization(serialization), interactions, NativeResourceSources.Accepts,
            static (_, bytes) => bytes.AsSpan().ToArray(), static (path, bytes) => _ = NativeResourceSources.Decode(path, bytes), NativeResourceSources.Save);
    }
    /// <summary>Gets the selected resource document context.</summary>
    public EditorDocumentContext? activeDocument => m_store.activeDocument;
    /// <summary>Opens an existing writable project resource.</summary>
    /// <param name="path">The native resource source identity.</param>
    /// <returns>The shared engine document.</returns>
    public EditorDocumentContext Open(AssetPath path) => m_store.Open(path);
    /// <summary>Creates a resource through official asset import before opening its draft.</summary>
    /// <typeparam name="T">The supported native resource type.</typeparam>
    /// <param name="source">A valid initial declaration.</param>
    /// <returns>The shared engine document.</returns>
    public EditorDocumentContext Create<T>(T source) where T : AssetObject
    {
        AssetPath path = AssetPath.Project("Game/Resources/" + Guid.NewGuid().ToString("N") + NativeResourceSources.Extension<T>());
        if (!EditorAssets.Save(path, source)) throw new InvalidOperationException("Resource import failed; inspect native engine diagnostics.");
        return Open(path);
    }
    /// <summary>Reads a detached native declaration with asset references resolved by the active owner.</summary>
    /// <typeparam name="T">The exact resource type.</typeparam>
    /// <param name="documentId">The shared document identity.</param>
    /// <returns>A detached candidate asset.</returns>
    public T Read<T>(Guid documentId) where T : AssetObject => NativeResourceSources.Decode(m_store.PathOf(documentId), m_store.Read(documentId)) as T
        ?? throw new InvalidOperationException("The resource document has another type.");
    /// <summary>Applies a candidate resource operation through native engine History.</summary>
    /// <typeparam name="T">The exact resource type.</typeparam>
    /// <param name="documentId">The engine document identity.</param>
    /// <param name="label">The visible undo label.</param>
    /// <param name="edit">The synchronous domain operation; never retained in History.</param>
    /// <param name="mergeKey">An optional engine edit gesture.</param>
    public void Edit<T>(
        Guid documentId,
        string label,
        Action<T> edit,
        string? mergeKey = null
    ) where T : AssetObject => m_store.Edit(documentId, label, bytes =>
    {
        T candidate = NativeResourceSources.Decode(m_store.PathOf(documentId), bytes) as T ?? throw new InvalidOperationException("The resource document has another type.");
        edit(candidate); return EditorAssets.EncodeNative(candidate);
    }, mergeKey);
    /// <summary>Reads the native owner-local change stamp for cache invalidation across edits, undo and redo.</summary>
    /// <param name="documentId">The currently open resource document.</param>
    /// <returns>A generation-local revision, never a persisted format version.</returns>
    public long GetRevision(Guid documentId) => m_store.RevisionOf(documentId);
    /// <summary>Inspects an already open source without opening a document or reading authored bytes.</summary>
    /// <param name="path">The native resource source path.</param>
    /// <returns>The generation-local stamp, or null when the source is not open.</returns>
    public NativeDocumentStamp? GetStamp(AssetPath path) => m_store.Stamp(path);
    /// <summary>Inspects an existing document without changing the active resource or selection.</summary>
    /// <param name="documentId">The engine document identity.</param>
    /// <returns>The generation-local stamp, or null after the document closes.</returns>
    public NativeDocumentStamp? GetStamp(Guid documentId) => m_store.Stamp(documentId);
    /// <summary>Reads an already open unsaved resource without opening a document, reading a source file or changing selection.</summary>
    /// <param name="path">The canonical native source path.</param>
    /// <param name="draft">The independently decoded dirty declaration, or null when no dirty document owns that path.</param>
    /// <returns>Whether the shared native document has unsaved authored values.</returns>
    public bool TryReadUnsaved(
        AssetPath path,
        out AssetObject? draft
    ) {
        draft = null;
        if (!m_store.TryReadUnsaved(path, out var bytes)) return false;
        draft = NativeResourceSources.Decode(path, bytes); return true;
    }
    /// <summary>Captures neutral unsaved state for editor generation transfer.</summary>
    /// <returns>Engine-native bytes without live asset objects.</returns>
    public byte[] CaptureReload() => m_store.CaptureReload();
    /// <summary>Restores all candidate drafts before replacing generation-local resource state.</summary>
    /// <param name="bytes">The previous generation's native transfer packet.</param>
    public void RestoreReload(ReadOnlySpan<byte> bytes) => m_store.RestoreReload(bytes);
    /// <summary>Checks whether a resource history operation's document is available.</summary>
    /// <param name="change">The neutral engine operation.</param>
    /// <returns>Current availability without applying changes.</returns>
    public EditorHistoryAvailability QueryHistory(EditorHistoryChange change) => m_store.QueryHistory(change);
    /// <summary>Applies an engine Undo or Redo to neutral resource state.</summary>
    /// <param name="change">The neutral operation.</param>
    /// <param name="direction">Undo or Redo.</param>
    /// <returns>Atomic application status.</returns>
    public EditorHistoryResult ApplyHistory(
        EditorHistoryChange change,
        EditorHistoryDirection direction
    ) => m_store.ApplyHistory(change, direction);
    /// <summary>Merges adjacent resource samples within the same explicit gesture.</summary>
    /// <param name="older">The previous sample.</param>
    /// <param name="newer">The next sample.</param>
    /// <param name="merged">An independently owned merged operation.</param>
    /// <returns>Whether the samples share one continuous edit.</returns>
    public bool TryMergeHistory(
        EditorHistoryChange older,
        EditorHistoryChange newer,
        out EditorHistoryChange? merged
    ) => m_store.TryMergeHistory(older, newer, out merged);
    /// <inheritdoc />
    protected override void OnStart(EditorContext context)
    {
        m_store.Start(context.projectDirectory);
        m_provider = m_interactions.documents.RegisterProvider(new ResourceDocumentProvider(m_store));
    }
    /// <inheritdoc />
    protected override void OnStop(EditorContext context) { m_provider?.Dispose(); m_provider = null; m_store.Clear(); }
}
