using System;
using System.Linq;
using System.Numerics;
using InnoEngine.Rendering;
using InnoEditor.Core;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using Inno.VisualNovel.Authoring.CollectionEditing;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Compiler.Validation;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Editor.Presentation;
using Inno.VisualNovel.Model.Audio;
using Inno.VisualNovel.Model.Collections;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Authoring.ProjectEditing;

namespace Inno.VisualNovel.Editor.Collections;

/// <summary>Edits native galleries, replay entries, typed rules and explicit replay initial values through engine History.</summary>
[EditorPanel("inno.vn.collections", "VN Collections", order: 459, defaultOpen: false, menuPath: "Authoring")]
public sealed class CollectionPanel : EditorPanel, IEditorPanelReloadState
{
    private readonly ResourceDocumentsModule m_documents;
    private readonly EditorInteractions m_interactions;
    private readonly ResourceDocumentToolbar m_toolbar;
    private readonly StoryInspectorFields m_fields;
    private readonly ExpressionEditorView m_expressions;
    private readonly NovelValueView m_values;
    private readonly CollectionVariantsView m_variants;
    private NovelProjectAsset? m_project;
    private string m_selected = "", m_newId = "cg.first", m_search = "", m_message = "", m_remove = "", m_initial = "";
    private CollectionKind m_kind;
    private int m_page;
    /// <summary>Binds resource documents and canonical asset selection to the host's History.</summary>
    /// <param name="documents">The native authoring document owner.</param>
    /// <param name="interactions">The editor selection and history services.</param>
    public CollectionPanel(
        ResourceDocumentsModule documents,
        EditorInteractions interactions
    ) {
        m_documents = documents; m_interactions = interactions; m_fields = new(interactions);
        m_toolbar = new(documents, interactions, m_fields.Finish); m_expressions = new(m_fields); m_values = new(m_fields); m_variants = new(m_fields, interactions);
    }
    /// <inheritdoc />
    public override Vector2 initialSize => new(1040, 820);
    /// <inheritdoc />
    protected override void OnDraw(EditorContext context)
    {
        m_fields.BeginFrame();
        try { Draw(); }
        catch (Exception failure) when (failure is not OutOfMemoryException) { m_message = (failure).Message; }
        finally { m_fields.EndFrame(); }
        if (m_message.Length > 0) EditorUiSession.current.widgets.Hint(m_message);
    }
    /// <inheritdoc />
    public ReadOnlyMemory<byte> CaptureReloadState() { m_fields.Finish(); return m_documents.CaptureReload(); }
    /// <inheritdoc />
    public void RestoreReloadState(ReadOnlyMemory<byte> state) => m_documents.RestoreReload(state.Span);
    /// <inheritdoc />
    protected override void OnDetach(EditorContext context) { m_fields.Finish(); m_project = null; }
    private void Draw()
    {
        if (!m_toolbar.Draw("Collection library", ".ivncollection", static () => new CollectionAsset(), out CollectionAsset? asset) || asset is null) return;
        if (NovelResourceSelection.Get<NovelProjectAsset>(m_interactions) is { } project && ImGui.Button("Use variables and resources from the selected game")) { m_fields.Finish(); m_project = project; }
        ImGui.Text("Reference check: " + (m_project?.title ?? "Select a game, then use the button above. Constant-only rules can be edited without a game."));
        m_fields.Text("collection/new-id", "New entry ID", ref m_newId); m_fields.SelectEnum("collection/new-kind", "Category", ref m_kind);
        if (ImGui.Button("Add collection entry"))
        {
            m_fields.Finish(); m_documents.Edit<CollectionAsset>(m_toolbar.documentId, "Add collection entry", candidate => CollectionEditService.Add(candidate, m_newId, m_kind));
            m_selected = m_newId; m_message = "Entry IDs remain stable. Unlock entries with the story's unlock instruction or the conditions below.";
        }
        if (m_fields.Text("collection/search", "Search name / ID", ref m_search)) m_page = 0;
        var filtered = asset.entries.Where(item => item.id.Contains(m_search, StringComparison.OrdinalIgnoreCase) || item.title.Contains(m_search, StringComparison.OrdinalIgnoreCase)).ToArray();
        int pages = Math.Max(1, (filtered.Length + 39) / 40); m_page = Math.Min(m_page, pages - 1);
        if (ImGui.Button("Previous page##collection")) m_page = Math.Max(0, m_page - 1);
        ImGui.SameLine(); ImGui.Text((m_page + 1) + " / " + pages); ImGui.SameLine();
        if (ImGui.Button("Next page##collection")) m_page = Math.Min(pages - 1, m_page + 1);
        foreach (var entry in filtered.Skip(m_page * 40).Take(40))
            if (ImGui.Selectable(entry.kind + " · " + entry.title + " · " + entry.id, m_selected == entry.id)) { m_fields.Finish(); m_selected = entry.id; m_remove = ""; }
        if (asset.entries.SingleOrDefault(item => item.id == m_selected) is not { } source) return;
        var selected = CollectionEntryCopy.Copy(source);
        ImGui.Separator(); ImGui.Text("Stable ID: " + selected.id);
        var kind = selected.kind; string title = selected.title, description = selected.description, author = selected.author;
        bool changed = m_fields.SelectEnum(selected.id + "/kind", "Category", ref kind); selected.kind = kind;
        changed |= m_fields.Text(selected.id + "/title", "Name", ref title); selected.title = title;
        changed |= m_fields.MultilineText(selected.id + "/description", "Description / body", ref description); selected.description = description;
        changed |= m_fields.Text(selected.id + "/author", "Author / composer", ref author); selected.author = author;
        bool initial = selected.initiallyUnlocked; changed |= m_fields.Toggle(selected.id + "/initial", "Unlocked initially", ref initial); selected.initiallyUnlocked = initial;
        ImGui.Text("Image: " + (selected.image?.name ?? "Unassigned"));
        if (NovelResourceSelection.Get<TextureAsset>(m_interactions) is { } image && ImGui.Button("Use selected image")) { m_fields.Finish(); selected.image = image; changed = true; }
        ImGui.SameLine(); if (selected.image is not null && ImGui.Button("Remove image")) { m_fields.Finish(); selected.image = null; changed = true; }
        VariableDefinition[] declarations = m_project is null ? [] : NovelProjectVariables.Read(m_project);
        changed |= m_variants.Draw(asset, selected);
        if (selected.kind == CollectionKind.Music)
        {
            string cue = selected.musicCueId;
            changed |= m_project is null ? m_fields.Text(selected.id + "/cue", "Music cue ID", ref cue) : m_fields.Select(selected.id + "/cue", "Music", ref cue,
                m_project.audio.Where(static audio => audio.channel is NovelAudioChannel.Bgm or NovelAudioChannel.Ambience).Select(static audio => (audio.resourceId, audio.title)));
            selected.musicCueId = cue;
        }
        if (selected.kind == CollectionKind.Replay) changed |= Replay(selected, declarations);
        if (ImGui.CollapsingHeader("Automatic unlock condition##" + selected.id))
        {
            changed |= m_expressions.Draw(selected.id + "/rule", selected.unlockCondition, declarations.Where(static variable => variable.scope != VariableScope.Local).ToArray());
            try { CollectionConditionValidator.Validate(selected.unlockCondition, declarations.Where(static variable => variable.scope != VariableScope.Local)); }
            catch (InvalidOperationException failure) { EditorUiSession.current.widgets.Hint((failure).Message); }
        }
        if (changed) m_documents.Edit<CollectionAsset>(m_toolbar.documentId, "Edit collection entry", candidate => CollectionEditService.Replace(candidate, selected), m_fields.mergeKey);
        if (ImGui.Button("Delete this entry…")) { m_fields.Finish(); m_remove = selected.id; }
        if (m_remove != selected.id) return;
        EditorUiSession.current.widgets.Hint("Deleting this entry leaves story unlock instructions referring to its ID unresolved. Game import identifies these references. Its global unlock identity will also become unavailable.");
        if (ImGui.Button("Confirm deletion of collection entry")) { m_documents.Edit<CollectionAsset>(m_toolbar.documentId, "Delete collection entry", candidate => CollectionEditService.Remove(candidate, selected.id)); m_selected = m_remove = ""; }
        ImGui.SameLine(); if (ImGui.Button("Cancel deletion")) m_remove = "";
    }
    private bool Replay(
        CollectionEntry entry,
        VariableDefinition[] variables
    ) {
        string chapter = entry.chapterId, target = entry.entryId;
        bool changed = m_project is null ? m_fields.Text(entry.id + "/chapter", "Replay chapter ID", ref chapter)
            : m_fields.Select(entry.id + "/chapter", "Replay chapter", ref chapter, m_project.chapters.Select(static item => (item.chapterId, item.title)));
        changed |= m_project is null ? m_fields.Text(entry.id + "/entry", "Public entry ID", ref target)
            : m_fields.Select(entry.id + "/entry", "Public entry", ref target, m_project.chapters.SingleOrDefault(item => item.chapterId == chapter)?.program?.entries.Select(static item => (item.id, item.id)) ?? []);
        entry.chapterId = chapter; entry.entryId = target;
        m_fields.Select(entry.id + "/initial-variable", "Explicit initial variables", ref m_initial, variables.Where(static variable => variable.scope != VariableScope.Global).Select(static variable => (variable.id, variable.name + " · " + variable.id)));
        if (ImGui.Button("Add or reset this initial variable##" + entry.id) && variables.SingleOrDefault(variable => variable.id == m_initial && variable.scope != VariableScope.Global) is { } definition)
        { m_fields.Finish(); entry.initialValues = [.. entry.initialValues.Where(item => item.id != m_initial), new() { id = m_initial, value = definition.initialValue.Copy() }]; changed = true; }
        string remove = "";
        foreach (var initial in entry.initialValues)
        {
            ImGui.Text(initial.id); var value = initial.value;
            changed |= m_values.Draw(entry.id + "/initial/" + initial.id, ref value, false, variables.SingleOrDefault(variable => variable.id == initial.id)?.members); initial.value = value;
            if (ImGui.Button("Remove this initial value##" + initial.id)) { m_fields.Finish(); remove = initial.id; changed = true; }
        }
        if (remove.Length > 0) entry.initialValues = entry.initialValues.Where(item => item.id != remove).ToArray();
        EditorUiSession.current.widgets.Hint("Replays start with the game's default stage and these explicit values. They do not reconstruct an assumed past route or write state back to the main game.");
        return changed;
    }
}
