using Inno.VisualNovel.Authoring.Documents;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using Inno.VisualNovel.Model.Story.Nodes.Audio;

namespace Inno.VisualNovel.Authoring.Templates;

/// <summary>Creates a complete performance lesson using declared original built-in resources.</summary>
public static class PerformanceStoryTemplate
{
    /// <summary>Builds character composition, music, clip waits, particles, screen effects and a real scenery transition.</summary>
    /// <param name="codec">The native graph settings codec.</param>
    /// <returns>A fresh editable chapter whose dependencies must be attached to the game library.</returns>
    public static StoryDraftState Create(StoryNodeDataCodec codec)
    {
        var b = new StoryTemplateBuilder(codec, "角色、演出与画面效果"); b.Add("entry", StoryNodeIds.entry);
        b.Add("scenery", StoryNodeIds.stage, new StageNode { operation = StageOperation.SetStage, resourceId = "builtin.stage.classroom" });
        b.Add("guide", StoryNodeIds.stage, new StageNode { operation = StageOperation.ShowCharacter, resourceId = "builtin.character.cheng", target = "guide",
            pose = "wave", expression = "smile", transform = new() { x = 420, y = 130 } });
        b.Add("friend", StoryNodeIds.stage, new StageNode { operation = StageOperation.ShowCharacter, resourceId = "builtin.character.shu", target = "friend",
            pose = "idle", expression = "neutral", transform = new() { x = 1020, y = 130 } });
        b.Add("music", StoryNodeIds.audio, new AudioNode { operation = AudioOperation.Play, cueId = "builtin.audio.dawn", slot = "lesson-music" });
        var greeting = TutorialStoryTemplates.Line("角色姿势和表情来自可编辑分层资源。接下来播放一段可保存、可暂停的缩放强调。"); greeting.characterId = "builtin.character.cheng";
        b.Add("hello", StoryNodeIds.dialogue, greeting);
        b.Add("motion", StoryNodeIds.performance, new PerformanceNode { operation = PerformanceOperation.Play, clipId = "builtin.motion.emphasis", instanceId = "lesson-motion", bindings = [new() { slot = "actor", target = "character/guide" }] });
        b.Add("motion-marker", StoryNodeIds.performance, new PerformanceNode { operation = PerformanceOperation.WaitMarker, instanceId = "lesson-motion", markerId = "middle" });
        b.Add("marker-line", StoryNodeIds.dialogue, TutorialStoryTemplates.Line("已经到达演出的中点标记。演出可以在这句对白背后继续，标记等待也能随存档恢复。"));
        b.Add("motion-wait", StoryNodeIds.performance, new PerformanceNode { operation = PerformanceOperation.Wait, instanceId = "lesson-motion" });
        b.Add("particles", StoryNodeIds.stage, new StageNode { operation = StageOperation.StartParticles, resourceId = "builtin.particles.petals", target = "lesson-petals", seed = 17 });
        b.Add("vignette", StoryNodeIds.screenEffect, new ScreenEffectNode { operation = ScreenEffectOperation.Fade, resourceId = "builtin.screen.vignette", instanceId = "lesson-vignette", strength = .4, duration = .4, waitForCompletion = true });
        b.Add("effects-line", StoryNodeIds.dialogue, TutorialStoryTemplates.Line("花瓣使用固定随机种子，暗角使用逻辑时间淡变。可以在这里测试存档与回滚，再对比恢复后的状态。"));
        b.Add("transition", StoryNodeIds.transition, new TransitionNode { operation = TransitionOperation.Begin, resourceId = "builtin.transition.dissolve", nextStage = "builtin.stage.seafront", waitForCompletion = true });
        b.Add("clear-particles", StoryNodeIds.stage, new StageNode { operation = StageOperation.RemoveParticles, target = "lesson-petals" });
        b.Add("clear-effect", StoryNodeIds.screenEffect, new ScreenEffectNode { operation = ScreenEffectOperation.Remove, instanceId = "lesson-vignette" });
        b.Add("finish", StoryNodeIds.dialogue, TutorialStoryTemplates.Line("转场已完成。角色、镜头、音频、粒子与效果都由同一个会话持有；修改原生预设副本即可改变演出。"));
        b.Add("stop-music", StoryNodeIds.audio, new AudioNode { operation = AudioOperation.Stop, slot = "lesson-music" }); b.Add("end", StoryNodeIds.end);
        string[] chain = ["entry", "scenery", "guide", "friend", "music", "hello", "motion", "motion-marker", "marker-line", "motion-wait", "particles", "vignette", "effects-line", "transition", "clear-particles", "clear-effect", "finish", "stop-music", "end"];
        for (int index = 1; index < chain.Length; index++) b.Link(chain[index - 1], chain[index]); return b.Build();
    }
}
