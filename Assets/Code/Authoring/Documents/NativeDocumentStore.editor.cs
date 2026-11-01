using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using InnoEngine.Assets;
using InnoEditor.Interactions;
using Inno.VisualNovel.Authoring.History;
using Inno.VisualNovel.Compiler;

namespace Inno.VisualNovel.Authoring.Documents;

internal sealed class NativeDocumentStore(
    string historyKind,
    IStorySerialization serialization,
    EditorInteractions interactions,
    Func<AssetPath, bool> accepts,
    Func<AssetPath, byte[], byte[]> sourceToDraft,
    Action<AssetPath, byte[]> validateDraft,
    Func<AssetPath, byte[], bool> saveDraft
)
{
    private readonly Dictionary<Guid, NativeDocumentState> m_states = [];
    private string m_project = string.Empty;
    internal Guid activeDocumentId { get; set; }
    internal EditorDocumentContext? activeDocument => interactions.documents.documents.FirstOrDefault(item => item.documentId == activeDocumentId);
    internal void Start(string project) => m_project = project;
    internal void Clear() => m_states.Clear();
    internal bool CanOpen(string path) => accepts(AssetPath.Parse(path));
    internal string Source(AssetPath path)
    {
        if (path.source != AssetSourceId.project || !accepts(path)) throw new InvalidOperationException("Copy installed resources into the project before editing.");
        string root = Path.GetFullPath(Path.Combine(m_project, "Assets")) + Path.DirectorySeparatorChar;
        string resolved = Path.GetFullPath(Path.Combine(root, path.localPath));
        if (!resolved.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Document source is outside the project asset root.");
        return resolved;
    }
    internal EditorDocumentContext Open(AssetPath path)
    {
        _ = Source(path);
        EditorDocumentContext document = interactions.documents.Open(path.ToString());
        activeDocumentId = document.documentId; return document;
    }
    internal byte[] Read(Guid id) => Require(id).current.ToArray();
    internal long RevisionOf(Guid id) => Require(id).revision;
    internal NativeDocumentStamp? Stamp(Guid id)
    {
        if (!m_states.TryGetValue(id, out var state)) return null;
        var document = interactions.documents.documents.FirstOrDefault(item => item.documentId == id);
        return document is null ? null : new(id, state.path, state.revision, document.isDirty);
    }
    internal NativeDocumentStamp? Stamp(AssetPath path)
    {
        var state = m_states.Values.FirstOrDefault(item => AssetPath.Parse(item.path) == path);
        return state is null ? null : Stamp(state.documentId);
    }
    internal bool TryReadUnsaved(
        AssetPath path,
        out byte[] bytes
    ) {
        var open = m_states.Values.FirstOrDefault(state => AssetPath.Parse(state.path) == path);
        if (open is null || open.current.AsSpan().SequenceEqual(open.saved)) { bytes = []; return false; }
        bytes = open.current.ToArray(); return true;
    }
    internal (byte[] bytes, Guid documentId, bool unsaved) Inspect(AssetPath path)
    {
        string source = Source(path);
        var open = m_states.Values.FirstOrDefault(state => AssetPath.Parse(state.path) == path);
        if (open is not null) return (open.current.ToArray(), open.documentId, !open.current.AsSpan().SequenceEqual(open.saved));
        var file = new FileInfo(source);
        if (!file.Exists || file.Length > 64 * 1024 * 1024) throw new IOException("The story source is unavailable or exceeds the 64 MiB search budget: " + path);
        return (sourceToDraft(path, File.ReadAllBytes(source)), Guid.Empty, false);
    }
    internal AssetPath PathOf(Guid id) => AssetPath.Parse(Require(id).path);
    internal void Edit(
        Guid id,
        string label,
        Func<byte[], byte[]> edit,
        string? mergeKey
    ) {
        NativeDocumentState state = Require(id);
        byte[] before = state.current;
        byte[] after = edit(before.ToArray());
        validateDraft(AssetPath.Parse(state.path), after);
        if (before.AsSpan().SequenceEqual(after)) return;
        var change = new EditorHistoryChange(historyKind, EditorHistoryPayload.FromBytes(serialization.Serialize(new DocumentEditRecord { documentId = id, before = before, after = after })), mergeKey);
        SetCurrent(state, after);
        try { interactions.history.RecordApplied(label, change); }
        catch { SetCurrent(state, before); change.Dispose(); throw; }
    }
    internal byte[] CaptureReload() => serialization.Serialize(new NativeDocumentReloadState { activeDocument = activeDocumentId, documents = m_states.Values.ToArray() });
    internal void RestoreReload(ReadOnlySpan<byte> bytes)
    {
        NativeDocumentReloadState candidate = serialization.Deserialize<NativeDocumentReloadState>(bytes);
        var states = new Dictionary<Guid, NativeDocumentState>();
        foreach (NativeDocumentState state in candidate.documents)
        {
            AssetPath path = AssetPath.Parse(state.path);
            _ = Source(path); validateDraft(path, state.current); validateDraft(path, state.saved);
            states.Add(state.documentId, state);
        }
        foreach (var pair in states) m_states[pair.Key] = pair.Value;
        foreach (NativeDocumentState state in states.Values) SetCurrent(state, state.current);
        activeDocumentId = candidate.activeDocument;
    }
    internal EditorHistoryAvailability QueryHistory(EditorHistoryChange change)
    {
        DocumentEditRecord record = serialization.Deserialize<DocumentEditRecord>(change.payload.ReadBytes());
        return m_states.ContainsKey(record.documentId) ? EditorHistoryAvailability.Available() : EditorHistoryAvailability.Unavailable("Reopen the owning document to restore this operation.");
    }
    internal EditorHistoryResult ApplyHistory(
        EditorHistoryChange change,
        EditorHistoryDirection direction
    ) {
        try
        {
            DocumentEditRecord record = serialization.Deserialize<DocumentEditRecord>(change.payload.ReadBytes());
            NativeDocumentState state = Require(record.documentId);
            byte[] expected = direction == EditorHistoryDirection.Undo ? record.after : record.before;
            byte[] target = direction == EditorHistoryDirection.Undo ? record.before : record.after;
            if (!state.current.AsSpan().SequenceEqual(expected)) return EditorHistoryResult.Failure("The current draft no longer matches this operation.");
            validateDraft(AssetPath.Parse(state.path), target); SetCurrent(state, target.ToArray());
            return EditorHistoryResult.Success();
        }
        catch (Exception error) when (error is not OutOfMemoryException) { return EditorHistoryResult.Failure(error.Message); }
    }
    internal bool TryMergeHistory(
        EditorHistoryChange older,
        EditorHistoryChange newer,
        out EditorHistoryChange? merged
    ) {
        merged = null;
        if (older.mergeKey is null || older.mergeKey != newer.mergeKey || older.kind != historyKind || newer.kind != historyKind) return false;
        DocumentEditRecord a = serialization.Deserialize<DocumentEditRecord>(older.payload.ReadBytes());
        DocumentEditRecord b = serialization.Deserialize<DocumentEditRecord>(newer.payload.ReadBytes());
        if (a.documentId != b.documentId || !a.after.AsSpan().SequenceEqual(b.before)) return false;
        merged = new(historyKind, EditorHistoryPayload.FromBytes(serialization.Serialize(new DocumentEditRecord { documentId = a.documentId, before = a.before, after = b.after })), older.mergeKey);
        return true;
    }
    internal void OpenSource(EditorDocumentContext document)
    {
        if (m_states.ContainsKey(document.documentId)) return;
        AssetPath path = AssetPath.Parse(document.assetPath);
        byte[] source = File.ReadAllBytes(Source(path));
        byte[] draft = sourceToDraft(path, source);
        validateDraft(path, draft);
        m_states.Add(document.documentId, new() { documentId = document.documentId, path = document.assetPath, current = draft, saved = draft, sourceHash = Hash(source) });
    }
    internal bool SaveSource(EditorDocumentContext document)
    {
        NativeDocumentState state = Require(document.documentId);
        AssetPath path = AssetPath.Parse(state.path);
        string source = Source(path);
        if (!File.Exists(source) || Hash(File.ReadAllBytes(source)) != state.sourceHash) throw new IOException("Source changed externally. Unsaved changes and history were retained.");
        if (!saveDraft(path, state.current.ToArray())) return false;
        state.saved = state.current.ToArray(); state.sourceHash = Hash(File.ReadAllBytes(source));
        interactions.documents.SetDirty(document.documentId, false); return true;
    }
    internal bool RevertSource(EditorDocumentContext document)
    {
        NativeDocumentState state = Require(document.documentId);
        AssetPath path = AssetPath.Parse(state.path);
        byte[] source = File.ReadAllBytes(Source(path));
        byte[] draft = sourceToDraft(path, source);
        validateDraft(path, draft); state.saved = draft.ToArray(); state.sourceHash = Hash(source); SetCurrent(state, draft); return true;
    }
    internal void CloseSource(EditorDocumentContext document) => m_states.Remove(document.documentId);
    private NativeDocumentState Require(Guid id) => m_states.TryGetValue(id, out NativeDocumentState? state) ? state : throw new InvalidOperationException("The document is unavailable in this generation.");
    private void SetCurrent(
        NativeDocumentState state,
        byte[] bytes
    ) { state.current = bytes; state.revision++; interactions.documents.SetDirty(state.documentId, !bytes.AsSpan().SequenceEqual(state.saved)); }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
