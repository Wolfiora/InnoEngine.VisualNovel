using System;
using System.IO;
using System.Security.Cryptography;
using InnoEngine.Assets;
using InnoEngine.Serialization;
using InnoEditor.Assets;
using InnoEditor.Core;
using InnoEditor.Interactions;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Story;

namespace Inno.VisualNovel.Authoring.Documents;

/// <summary>Owns chapter semantics over the shared neutral native document and History mechanism.</summary>
[EditorModule("inno.vn.documents", order: 420)]
public sealed class StoryDocumentsModule : EditorModule
{
    /// <summary>Identifies the reload-safe story edit protocol.</summary>
    public const string historyKind = "inno.vn.story-edit";
    private readonly IStorySerialization m_serialization;
    private readonly NativeDocumentStore m_store;
    private readonly EditorInteractions m_interactions;
    private IDisposable? m_providerRegistration;

    /// <summary>Creates a chapter owner bound to one editor generation.</summary>
    /// <param name="serialization">The host-owned native serializer.</param>
    /// <param name="interactions">The engine document and History coordinator.</param>
    public StoryDocumentsModule(
        SerializationRegistry serialization,
        EditorInteractions interactions
    ) {
        m_serialization = new EditorStorySerialization(serialization);
        m_interactions = interactions;
        codec = new(m_serialization); edits = new(codec); clipboard = new(m_serialization, codec);
        notes = new(m_serialization); search = new(codec, notes);
        imports = new(m_serialization, codec);
        compiler = new(m_serialization);
        reorder = new(m_serialization, codec, compiler);
        deletion = new(edits, codec);
        quickInsert = new(edits, codec);
        m_store = new(historyKind, m_serialization, interactions,
            static path => path.localPath.EndsWith(".ivnstory", StringComparison.OrdinalIgnoreCase),
            (_, bytes) => m_serialization.Serialize(StoryDraftState.FromAsset(EditorAssets.DecodeNative<StoryAsset>(bytes))),
            (path, bytes) => _ = m_serialization.Deserialize<StoryDraftState>(bytes),
            (path, bytes) => EditorAssets.Save(path, m_serialization.Deserialize<StoryDraftState>(bytes).ToAsset()));
    }
    /// <summary>Gets the generation-owned native node settings codec.</summary>
    public StoryNodeDataCodec codec { get; }
    /// <summary>Gets the generation-owned production compiler shared by document validation and preview construction.</summary>
    public StoryCompiler compiler { get; }
    /// <summary>Gets the shared graph semantic edit service.</summary>
    public StoryEditService edits { get; }
    /// <summary>Gets compiler-checked, explicitly reviewed card moves shared by script and graph authoring.</summary>
    public StoryReorderService reorder { get; }
    /// <summary>Gets exact group deletion reviews and explicit boundary reconnection shared by authoring views.</summary>
    public StoryDeleteService deletion { get; }
    /// <summary>Gets instruction creation and complete choice or parallel continuation templates shared by both primary views.</summary>
    public StoryQuickInsertService quickInsert { get; }
    /// <summary>Gets the author annotation editor shared by graph and script documents.</summary>
    public StoryNotesEditService notes { get; }
    /// <summary>Gets on-demand chapter text, resource and bookmark search.</summary>
    public StorySearchService search { get; }
    /// <summary>Gets reviewed external dialogue exchange over the same native chapter and graph edits.</summary>
    public StoryImportService imports { get; }
    /// <summary>Gets the neutral selection copy and identity-remapping service.</summary>
    public StoryClipboardService clipboard { get; }
    /// <summary>Gets or sets the selected engine document identity.</summary>
    public Guid activeDocumentId { get => m_store.activeDocumentId; set => m_store.activeDocumentId = value; }
    /// <summary>Gets the selected engine document context.</summary>
    public EditorDocumentContext? activeDocument => m_store.activeDocument;
    /// <summary>Creates an editable branching chapter through official native asset import.</summary>
    /// <returns>The shared engine document.</returns>
    public EditorDocumentContext CreateFirstStory()
    {
        string local = "Game/Stories/FirstStory.ivnstory";
        for (int suffix = 2; File.Exists(m_store.Source(AssetPath.Project(local))); suffix++) local = $"Game/Stories/FirstStory{suffix}.ivnstory";
        if (!EditorAssets.Save(AssetPath.Project(local), FirstStoryTemplate.Create(codec).ToAsset())) throw new InvalidOperationException("Story import failed; inspect native engine diagnostics.");
        return Open(AssetPath.Project(local));
    }
    /// <summary>Opens the authoritative source instead of its stripped runtime program.</summary>
    /// <param name="path">The writable project chapter.</param>
    /// <returns>The shared engine document.</returns>
    public EditorDocumentContext Open(AssetPath path) => m_store.Open(path);
    /// <summary>Reads a detached draft shared by script, graph and preview views.</summary>
    /// <param name="documentId">The engine document identity.</param>
    /// <returns>A generation-local detached draft.</returns>
    public StoryDraftState Read(Guid documentId) => m_serialization.Deserialize<StoryDraftState>(m_store.Read(documentId));
    /// <summary>Reads an already open unsaved chapter without opening another document or changing selection.</summary>
    /// <param name="path">The canonical chapter source path.</param>
    /// <param name="draft">The detached dirty graph, or null when the path has no unsaved native document.</param>
    /// <returns>Whether unsaved graph content was captured.</returns>
    public bool TryReadUnsaved(
        AssetPath path,
        out StoryDraftState? draft
    ) {
        draft = null;
        if (!m_store.TryReadUnsaved(path, out var bytes)) return false;
        draft = m_serialization.Deserialize<StoryDraftState>(bytes); return true;
    }
    /// <summary>Reads an open draft or native chapter source without opening another document or changing selection.</summary>
    /// <param name="path">The writable project's authored chapter path.</param>
    /// <returns>An independent snapshot including dirty edits when the document is already open.</returns>
    public StoryDocumentSnapshot Inspect(AssetPath path)
    {
        var snapshot = m_store.Inspect(path);
        return new(path, m_serialization.Deserialize<StoryDraftState>(snapshot.bytes), snapshot.documentId, snapshot.unsaved, Convert.ToHexString(SHA256.HashData(snapshot.bytes)));
    }
    /// <summary>Reads an owner-local change stamp for view caches; it resets when the document generation is restored.</summary>
    /// <param name="documentId">The currently open native document.</param>
    /// <returns>A stamp changed by edits, undo, redo and source reverts, never a persistence schema version.</returns>
    public long GetRevision(Guid documentId) => m_store.RevisionOf(documentId);
    /// <summary>Inspects an already open source without opening a document or reading authored bytes.</summary>
    /// <param name="path">The native chapter source path.</param>
    /// <returns>The generation-local stamp, or null when the source is not open.</returns>
    public NativeDocumentStamp? GetStamp(AssetPath path) => m_store.Stamp(path);
    /// <summary>Inspects an existing document without changing the active chapter or selection.</summary>
    /// <param name="documentId">The engine document identity.</param>
    /// <returns>The generation-local stamp, or null after the document closes.</returns>
    public NativeDocumentStamp? GetStamp(Guid documentId) => m_store.Stamp(documentId);
    /// <summary>Applies a candidate semantic edit through neutral engine History.</summary>
    /// <param name="documentId">The engine document identity.</param>
    /// <param name="label">The visible Undo label.</param>
    /// <param name="edit">The synchronous semantic mutation; never retained in History.</param>
    /// <param name="mergeKey">An optional active engine gesture.</param>
    public void Edit(
        Guid documentId,
        string label,
        Action<StoryDraftState> edit,
        string? mergeKey = null
    ) {
        ArgumentNullException.ThrowIfNull(edit);
        m_store.Edit(documentId, label, bytes =>
        {
            StoryDraftState draft = m_serialization.Deserialize<StoryDraftState>(bytes);
            edit(draft); return m_serialization.Serialize(draft);
        }, mergeKey);
    }
    /// <summary>Compiles an unsaved draft using the production compiler.</summary>
    /// <param name="documentId">The engine document identity.</param>
    /// <returns>The executable program or source-addressed diagnostics.</returns>
    public StoryCompilationResult Compile(Guid documentId) => compiler.Compile(Read(documentId).ToAsset());
    /// <summary>Captures neutral unsaved documents for assembly generation transfer.</summary>
    /// <returns>Engine-native bytes without converter instances or CLR types.</returns>
    public byte[] CaptureReload() => m_store.CaptureReload();
    /// <summary>Validates all transferred drafts before adopting their current generation state.</summary>
    /// <param name="bytes">The neutral reload transfer.</param>
    public void RestoreReload(ReadOnlySpan<byte> bytes) => m_store.RestoreReload(bytes);
    /// <summary>Reports whether a neutral operation's owning document is available.</summary>
    /// <param name="change">The engine History operation.</param>
    /// <returns>Current availability without mutation.</returns>
    public EditorHistoryAvailability QueryHistory(EditorHistoryChange change) => m_store.QueryHistory(change);
    /// <summary>Applies an engine Undo or Redo atomically.</summary>
    /// <param name="change">The neutral operation.</param>
    /// <param name="direction">Undo or Redo.</param>
    /// <returns>Atomic application status.</returns>
    public EditorHistoryResult ApplyHistory(
        EditorHistoryChange change,
        EditorHistoryDirection direction
    ) => m_store.ApplyHistory(change, direction);
    /// <summary>Merges adjacent samples only inside one explicit shared gesture.</summary>
    /// <param name="older">The previous operation.</param>
    /// <param name="newer">The next operation.</param>
    /// <param name="merged">The independently owned combined operation.</param>
    /// <returns>Whether the operations form one uninterrupted edit.</returns>
    public bool TryMergeHistory(
        EditorHistoryChange older,
        EditorHistoryChange newer,
        out EditorHistoryChange? merged
    ) => m_store.TryMergeHistory(older, newer, out merged);
    /// <inheritdoc />
    protected override void OnStart(EditorContext context)
    {
        m_store.Start(context.projectDirectory);
        m_providerRegistration = m_interactions.documents.RegisterProvider(new StoryDocumentProvider(this));
    }
    /// <inheritdoc />
    protected override void OnStop(EditorContext context) { m_providerRegistration?.Dispose(); m_providerRegistration = null; m_store.Clear(); }
    internal void OpenSource(EditorDocumentContext document) => m_store.OpenSource(document);
    internal bool SaveSource(EditorDocumentContext document) => m_store.SaveSource(document);
    internal bool RevertSource(EditorDocumentContext document) => m_store.RevertSource(document);
    internal void CloseSource(EditorDocumentContext document) => m_store.CloseSource(document);
}
