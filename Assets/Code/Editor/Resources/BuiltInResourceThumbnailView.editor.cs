using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using InnoEditor.ImGui;
using InnoEditor.Rendering;
using InnoEngine.Assets;
using InnoEngine.Rendering;
using Inno.VisualNovel.Authoring.BuiltInResources;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Authoring.VoiceManagement;
using Inno.VisualNovel.Editor.Presentation;
using Inno.VisualNovel.Editor.ScreenDesigner;
using Inno.VisualNovel.Integration.Rendering;
using Inno.VisualNovel.Model.Audio;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Interface.Themes;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.UI.Documents;

namespace Inno.VisualNovel.Editor.Resources;

internal sealed class BuiltInResourceThumbnailView : IDisposable
{
    private readonly BuiltInResourceEntry m_entry;
    private readonly IEditorPreviewService m_previews;
    private readonly NovelPreviewCanvas m_canvas;
    private AssetObject? m_asset;
    private long m_version;
    private BuiltInStagePreview? m_stage;
    private UiDocumentInstance? m_page;
    private readonly ScreenPreviewDataSource m_data = new();
    private IReadOnlyList<WaveformPeak>? m_peaks;
    private string m_error = string.Empty;
    internal BuiltInResourceThumbnailView(
        BuiltInResourceEntry entry,
        IEditorPreviewService previews
    ) { m_entry = entry; m_previews = previews; m_canvas = new(previews); }
    internal void Draw(Vector2 size)
    {
        try
        {
            if (m_asset is null && m_error.Length == 0 || m_asset is not null && Assets.Load<AssetObject>(m_asset.identity.persistentId).contentVersion != m_version) Prepare();
            if (m_error.Length > 0) { ImGui.Dummy(size); return; }
            if (m_asset is TextureAsset texture)
            {
                float factor = Math.Min(size.X / Math.Max(1, texture.width), size.Y / Math.Max(1, texture.height));
                Vector2 imageSize = new(texture.width * factor, texture.height * factor);
                if (m_previews.TryGetTexture(texture, out var handle)) m_previews.Draw(handle, imageSize); else ImGui.Dummy(imageSize);
            }
            else if (m_peaks is not null) Waveform(size);
            else if (m_stage is not null)
            {
                double time = m_entry.kind == BuiltInResourceKind.Particle ? 1.5 : Math.Min(2, m_stage.duration / 2);
                m_canvas.Draw(new(1920, 1080, []), size, m_stage.Evaluate(time));
            }
            else if (m_page is not null) m_canvas.Draw(m_page.BuildFrame(m_data), size);
            else ImGui.Dummy(size);
        }
        catch (Exception error) when (error is not OutOfMemoryException) { m_error = error.Message; ImGui.Dummy(size); }
    }
    internal string error => m_error;
    private void Prepare()
    {
        m_asset = BuiltInResourceLoader.Load(m_entry); m_version = m_asset.contentVersion;
        m_stage = null; m_page = null; m_peaks = null; m_error = string.Empty;
        if (m_asset is StageAsset or CharacterAsset or PerformanceClipAsset or VisualEffectAsset)
            m_stage = new(m_asset, BuiltInNovelAssets.Load<StageAsset>("BuiltIn/Backgrounds/Classroom.ivnstage"),
                BuiltInNovelAssets.Load<CharacterAsset>("BuiltIn/Characters/Cheng/Cheng.ivncharacter"), BuiltInNovelAssets.Load<StageAsset>("BuiltIn/Backgrounds/Seafront.ivnstage"));
        else if (m_asset is AudioCueAsset { clip: { } clip }) m_peaks = NativeWaveformSource.Read(clip).BuildPeaks(64);
        else
        {
            var theme = m_asset as ThemeAsset;
            var screen = theme?.screens.FirstOrDefault(static item => item.role == "title")?.screen ?? m_asset as ScreenAsset;
            if (screen is not null)
            {
                m_data.SetPage(screen.stateVariables);
                m_page = new(screen, NovelPresentationFrame.Measure, NovelPresentationFrame.MeasureRichText, theme);
            }
        }
    }
    private void Waveform(Vector2 size)
    {
        Vector2 origin = ImGui.GetCursorScreenPosition(); var draw = ImGui.GetDrawList();
        uint color = ImGui.ToPackedColor(new(.5f, .8f, 1, 1));
        draw.Rectangle(origin, origin + size, color, 0, 1);
        for (int index = 0; index < m_peaks!.Count; index++)
        {
            var peak = m_peaks[index]; float x = origin.X + (index + .5f) * size.X / m_peaks.Count;
            draw.Line(new(x, origin.Y + (.5f - peak.maximum * .45f) * size.Y), new(x, origin.Y + (.5f - peak.minimum * .45f) * size.Y), color, 1);
        }
        ImGui.Dummy(size);
    }
    public void Dispose() { m_canvas.Dispose(); m_asset = null; m_stage = null; m_page = null; m_peaks = null; }
}
