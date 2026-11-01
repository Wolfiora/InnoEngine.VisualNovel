using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Authoring.Diagnostics;

namespace Inno.VisualNovel.Authoring.Preview;

internal sealed class PreviewRouteRecorder(
    NovelPreviewCaseAsset origin,
    long revision
)
{
    private readonly List<string> m_choices = [];
    private readonly List<PreviewInput> m_inputs = [];
    private long m_sequence;
    private StoryMachine? m_machine;
    internal string error { get; private set; } = string.Empty;
    internal void Invalidate(string reason) => error = reason;
    internal void Observe(NovelSession session)
    {
        if (error.Length > 0) return;
        if (session.stateRevision != revision) { Invalidate("预览状态已被替换，请重新运行后录制路线。"); return; }
        StoryMachine? story = session.story;
        if (!ReferenceEquals(story, m_machine)) { m_machine = story; m_sequence = 0; }
        if (story?.lastInteractionResult is not { } result || result.sequence == m_sequence) return;
        if (result.sequence != m_sequence + 1) { Invalidate("交互记录存在未观察的步骤，请从入口重新录制。"); return; }
        m_sequence = result.sequence;
        if (m_choices.Count + m_inputs.Count >= 4096) { Invalidate("路线记录超过 4096 次交互，请拆分用例。"); return; }
        if (result.kind == StoryInteractionKind.Choice) m_choices.Add(result.value);
        else m_inputs.Add(new() { chapterId = result.chapterId, nodeId = result.nodeId, text = result.value, cancel = result.kind == StoryInteractionKind.InputCancelled });
    }
    internal NovelPreviewCaseAsset Capture(
        NovelSession session,
        string targetChapter
    ) {
        Observe(session);
        if (error.Length > 0) throw new InvalidOperationException(error);
        if (origin.mode == PreviewRunMode.Local) throw new AuthoringValidationException($"从真实入口运行后再录制路线；局部预览可以保存变量与舞台初值。");
        var story = session.interactiveStory!;
        if (story.sourceChapterId != targetChapter || story.status is not (StoryStatus.Dialogue or StoryStatus.Choice or StoryStatus.Input or StoryStatus.AdvanceWait))
            throw new AuthoringValidationException($"在当前章节的对白、选项、输入或等待推进边界保存路线目标。");
        return new()
        {
            title = origin.title, mode = PreviewRunMode.Route, chapterId = origin.chapterId, entryId = origin.entryId, nodeId = story.sourceNodeId,
            targetInteraction = session.story!.interactionSequence,
            initialValues = origin.initialValues.Select(static item => new Narrative.Values.VariableState { id = item.id, value = item.value.Copy() }).ToArray(),
            useStageSnapshot = origin.useStageSnapshot, stage = origin.useStageSnapshot ? session.stage.Fork(origin.stage).Capture() : new(),
            choices = m_choices.ToArray(), inputs = m_inputs.Select(static input => new PreviewInput { chapterId = input.chapterId, nodeId = input.nodeId, text = input.text, cancel = input.cancel }).ToArray(),
            maximumSeconds = origin.maximumSeconds, stepSeconds = origin.stepSeconds,
            breakpoints = origin.breakpoints.Select(static item => item.Copy()).ToArray()
        };
    }
}
