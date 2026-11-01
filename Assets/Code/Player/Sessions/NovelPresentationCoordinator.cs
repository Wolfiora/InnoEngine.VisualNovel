using System;
using System.Collections.Generic;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using Inno.VisualNovel.Model.Story.Nodes.Audio;
using Inno.VisualNovel.Model.Story.Nodes.Interface;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Player.Audio;
using Inno.VisualNovel.Player.Collections;
using Inno.VisualNovel.Stage.Composition;

namespace Inno.VisualNovel.Player.Sessions;

internal sealed class NovelPresentationCoordinator(
    IReadOnlyDictionary<string, StageAsset> stages,
    IReadOnlyDictionary<string, CharacterAsset> characters,
    Action<TransitionNode, StageController> transition,
    Action<SystemNode> system,
    float designWidth,
    float designHeight
)
{
    internal void Pump(
        StoryMachine? story,
        StageController stage,
        NovelAudioDirector? audio,
        NovelCollectionService collections
    )
    {
        if (story is null || story.debugPaused) return;
        try
        {
            for (int budget = 512; budget > 0; budget--)
            {
                if (story.debugPaused) return;
                ApplyTaskCleanup(story, stage, audio);
                bool progressed = false;
                foreach (StoryMachine actor in story.PresentationTasks())
                {
                    if (story.debugPaused) return;
                    try { progressed |= TryPresent(actor, stage, audio, collections); }
                    catch (Exception failure) when (failure is not OutOfMemoryException)
                    { throw new InvalidOperationException(actor.sourceChapterId + "/" + actor.sourceNodeId + ": " + failure.Message, failure); }
                }
                story.SynchronizeTasks(); ApplyTaskCleanup(story, stage, audio);
                if (!progressed) return;
            }
            throw new InvalidOperationException("Presentation instruction budget exceeded; add an explicit wait to the loop.");
        }
        catch (Exception failure) when (failure is not OutOfMemoryException) { story.Fail(failure.Message); ApplyTaskCleanup(story, stage, audio); }
    }

    private bool TryPresent(
        StoryMachine actor,
        StageController stage,
        NovelAudioDirector? audio,
        NovelCollectionService collections
    )
    {
        if (actor.pendingSystem is { } action)
        {
            system(action);
            bool suspend = SystemNodeRules.Suspends(action.operation);
            actor.CompletePresentation(suspend); return !suspend;
        }
        if (actor.pendingUnlock is { } unlock) collections.Unlock(unlock);
        else if (actor.pendingScreenEffect is { } effect)
        {
            if (effect.operation == ScreenEffectOperation.Wait) { if (stage.screenEffects.IsActive(effect.instanceId)) return false; }
            else stage.ApplyScreenEffect(effect);
        }
        else if (actor.pendingTransition is { } boundary)
        {
            if (boundary.operation == TransitionOperation.Wait) { if (stage.transitions.active) return false; }
            else transition(boundary, stage);
        }
        else if (actor.pendingPerformance is { } performance)
        {
            if (performance.operation == PerformanceOperation.Wait)
            { if (stage.performances.IsActive(performance.instanceId)) return false; }
            else if (performance.operation == PerformanceOperation.WaitMarker)
            { if (!stage.HasReachedPerformanceMarker(performance.instanceId, performance.markerId)) return false; }
            else
            {
                if (performance.operation == PerformanceOperation.Play && stage.performances.HasChannel(performance.clipId, Model.Performance.PerformanceProperty.Volume) && audio is null)
                    throw new InvalidOperationException("Audio volume performances require this session's audio output adapter.");
                stage.ApplyPerformance(performance); NovelAudioPerformance.Apply(stage, audio);
            }
        }
        else if (actor.pendingAudio is { } sound)
        {
            NovelAudioDirector director = audio ?? throw new InvalidOperationException("This session has no audio output adapter.");
            NovelAudioPerformance.Apply(stage, director);
            if (sound.operation == AudioOperation.Wait) { if (director.IsActive(sound.slot)) return false; }
            else director.Apply(sound);
        }
        else if (actor.pendingStage is { operation: StageOperation.WaitParticles } waiting)
        { if (stage.particles.IsActive(waiting.target)) return false; }
        else if (actor.pendingStage is { } request) switch (request.operation)
        {
            case StageOperation.SetStage: stage.SetStage(stages[request.resourceId]); break;
            case StageOperation.ShowCharacter: stage.ShowCharacter(request.target, characters[request.resourceId], request.pose, request.expression, request.transform, request.order); break;
            case StageOperation.HideCharacter: stage.HideCharacter(request.target); break;
            case StageOperation.BeginSpeaking: stage.SetCharacterSpeaking(request.target, true); break;
            case StageOperation.EndSpeaking: stage.SetCharacterSpeaking(request.target, false); break;
            case StageOperation.SetCamera: stage.SetCamera(request.transform.x, request.transform.y, request.transform.scaleX, request.transform.rotation); break;
            case StageOperation.FollowCamera: stage.FollowCamera(request.target, request.transform, designWidth, designHeight); break;
            case StageOperation.StopCameraFollow: stage.StopCameraFollow(); break;
            case StageOperation.StartParticles: stage.particles.Start(request.target, request.resourceId, request.seed, request.transform.x, request.transform.y); break;
            case StageOperation.StopParticles: stage.particles.Stop(request.target); break;
            case StageOperation.RemoveParticles: stage.particles.Remove(request.target); break;
            default: throw new InvalidOperationException("The requested stage operation is unavailable.");
        }
        else throw new InvalidOperationException("Presentation request is unavailable.");
        actor.CompletePresentation(); return true;
    }

    private static void ApplyTaskCleanup(
        StoryMachine? story,
        StageController stage,
        NovelAudioDirector? audio
    )
    {
        if (story is null) return;
        foreach (var resource in story.DrainTaskCleanup())
            switch (resource.kind)
            {
                case "audio": audio?.CancelSlot(resource.id); break;
                case "performance": stage.ApplyPerformance(new() { operation = PerformanceOperation.Cancel, instanceId = resource.id }); NovelAudioPerformance.Apply(stage, audio); break;
                case "particles": stage.particles.Remove(resource.id); break;
                case "effect": stage.ApplyScreenEffect(new() { operation = ScreenEffectOperation.Remove, instanceId = resource.id }); break;
                case "transition": stage.transitions.Complete(); break;
                case "speaking": if (stage.IsCharacterShown(resource.id)) stage.SetCharacterSpeaking(resource.id, false); break;
                case "camera-follow": if (stage.cameraFollowOwner == resource.id) stage.StopCameraFollow(); break;
            }
    }
}
