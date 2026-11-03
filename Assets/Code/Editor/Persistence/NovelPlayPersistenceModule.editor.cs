using System;
using InnoEditor.Core;
using InnoEditor.PlayMode;
using InnoEditor.Scene;
using InnoEngine.Serialization;
using Inno.VisualNovel.Authoring.Preview;
using Inno.VisualNovel.Integration.Hosting;
using Inno.VisualNovel.Integration.Services;

namespace Inno.VisualNovel.Editor.Persistence;

/// <summary>Configures native persistence on isolated Play scene components before their first update.</summary>
[EditorModule("inno.vn.play-persistence", order: 230)]
public sealed class NovelPlayPersistenceModule : EditorModule
{
    private readonly IEditorPlayMode m_play;
    private readonly IEditorSceneWorkspace m_scenes;
    private readonly EditorNovelStateCodec m_codec;
    private IDisposable? m_subscription;

    /// <summary>Creates one generation-owned bridge without retaining edit-scene components.</summary>
    /// <param name="play">The host's isolated Play lifecycle.</param>
    /// <param name="scenes">The currently presented native scene world.</param>
    /// <param name="serialization">The current generation's native serializer.</param>
    public NovelPlayPersistenceModule(
        IEditorPlayMode play,
        IEditorSceneWorkspace scenes,
        SerializationRegistry serialization
    ) {
        m_play = play;
        m_scenes = scenes;
        m_codec = new(serialization);
    }

    /// <inheritdoc />
    protected override void OnStart(EditorContext context)
    {
        m_subscription = m_play.ListenStateChanges(Configure);
    }

    /// <inheritdoc />
    protected override void OnStop(EditorContext context)
    {
        m_subscription?.Dispose();
        m_subscription = null;
    }

    private void Configure(EditorPlayModeState state)
    {
        if (state != EditorPlayModeState.Playing || m_scenes.canPersist) return;
        foreach (var scene in m_scenes.scenes)
            foreach (var system in scene.GetSystems())
                if (system is NovelSceneSystem player)
                    if (!player.hasPersistence) player.ConfigurePersistence(new InnoNovelStorage(), m_codec);
    }
}
