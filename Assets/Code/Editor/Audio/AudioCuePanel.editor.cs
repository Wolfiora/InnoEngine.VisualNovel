using System;
using System.Numerics;
using InnoEngine.Audio;
using InnoEditor.Core;
using InnoEditor.ImGui;
using InnoEditor.Interactions;
using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Editor.Inspection;
using Inno.VisualNovel.Editor.Presentation;
using Inno.VisualNovel.Model.Audio;

namespace Inno.VisualNovel.Editor.Audio;

/// <summary>Creates and edits native audio cues over shared resource documents and engine History.</summary>
[EditorPanel("inno.vn.audio", "VN 音频提示 · Audio Cues", order: 456, menuPath: "Authoring")]
public sealed class AudioCuePanel : EditorPanel, IEditorPanelReloadState
{
    private readonly ResourceDocumentsModule m_documents;
    private readonly EditorInteractions m_interactions;
    private readonly ResourceDocumentToolbar m_toolbar;
    private readonly StoryInspectorFields m_fields;
    private readonly WaveformView m_waveform;
    private readonly AudioAuditionView m_audition;
    private string m_message = string.Empty;

    /// <summary>Binds native cue drafts to the editor's resource selection and undo coordinator.</summary>
    /// <param name="documents">The native document owner.</param>
    /// <param name="interactions">The host History and selection service.</param>
    public AudioCuePanel(
        ResourceDocumentsModule documents,
        EditorInteractions interactions
    ) {
        m_documents = documents; m_interactions = interactions; m_fields = new(interactions);
        m_waveform = new(m_fields);
        m_audition = new(m_fields);
        m_toolbar = new(documents, interactions, m_fields.Finish);
    }
    /// <inheritdoc />
    public override Vector2 initialSize => new(740, 640);
    /// <inheritdoc />
    protected override void OnDraw(EditorContext context)
    {
        m_fields.BeginFrame();
        try { Draw(context.projectDirectory); }
        catch (Exception error) when (error is not OutOfMemoryException) { m_message = m_fields.Error(error); }
        finally { m_fields.EndFrame(); }
        if (m_message.Length > 0) EditorUiSession.current.widgets.Hint(m_message);
    }
    /// <inheritdoc />
    public ReadOnlyMemory<byte> CaptureReloadState() { m_fields.Finish(); m_audition.Stop(); return m_documents.CaptureReload(); }
    /// <inheritdoc />
    public void RestoreReloadState(ReadOnlyMemory<byte> state) => m_documents.RestoreReload(state.Span);
    /// <inheritdoc />
    protected override void OnDetach(EditorContext context) { m_fields.Finish(); m_audition.Dispose(); }

    private void Draw(string projectDirectory)
    {
        AudioClipAsset? selected = NovelResourceSelection.Get<AudioClipAsset>(m_interactions);
        Func<AudioCueAsset>? create = selected is null ? null : () => new() { resourceId = "audio-" + Guid.NewGuid().ToString("N"), title = selected.name, clip = selected };
        EditorUiSession.current.widgets.Hint(m_fields.Localize("在 File Browser 选择导入的音频创建提示；选择 .ivnaudio 可打开已有提示。语音请设为 Voice 并关闭循环。"));
        if (!m_toolbar.Draw("音频提示", ".ivnaudio", create, out AudioCueAsset? cue) || cue is null) { m_audition.Stop(); return; }
        string title = cue.title, author = cue.author, id = cue.resourceId;
        double gain = cue.volume, fadeIn = cue.fadeIn, fadeOut = cue.fadeOut;
        bool loop = cue.loop; NovelAudioChannel channel = cue.channel;
        bool changed = m_fields.Text("audio/title", m_fields.Localize("标题"), ref title); cue.title = title;
        changed |= m_fields.Text("audio/author", m_fields.Localize("作者 / 来源"), ref author); cue.author = author;
        changed |= m_fields.Text("audio/id", m_fields.Localize("剧情资源 ID"), ref id); cue.resourceId = id;
        changed |= m_fields.SelectEnum("audio/channel", "混音分类", ref channel); cue.channel = channel;
        changed |= m_fields.Toggle("audio/loop", "循环", ref loop); cue.loop = loop;
        changed |= m_fields.Number("audio/gain", m_fields.Localize("音量"), ref gain, 0, 1); cue.volume = gain;
        changed |= m_fields.Number("audio/fade-in", m_fields.Localize("淡入秒数"), ref fadeIn, 0, 3600); cue.fadeIn = fadeIn;
        changed |= m_fields.Number("audio/fade-out", m_fields.Localize("淡出秒数"), ref fadeOut, 0, 3600); cue.fadeOut = fadeOut;
        ImGui.Text(m_fields.Localize("音频：") + (cue.clip?.name ?? m_fields.Localize("未分配")) + " · " + (cue.clip?.duration.ToString() ?? "—"));
        m_audition.Draw(cue);
        if (selected is not null && ImGui.Button(m_fields.Label("替换为所选音频"))) { m_fields.Finish(); cue.clip = selected; changed = true; }
        if (m_waveform.Draw(projectDirectory, cue.clip) is { } trimmed) { m_fields.Finish(); cue.clip = trimmed; changed = true; }
        if (changed) m_documents.Edit<AudioCueAsset>(m_toolbar.documentId, m_fields.Localize("编辑音频提示"), candidate =>
        { candidate.resourceId = cue.resourceId; candidate.title = cue.title; candidate.author = cue.author; candidate.clip = cue.clip;
            candidate.channel = cue.channel; candidate.volume = cue.volume; candidate.loop = cue.loop; candidate.fadeIn = cue.fadeIn; candidate.fadeOut = cue.fadeOut; }, m_fields.mergeKey);
    }
}
