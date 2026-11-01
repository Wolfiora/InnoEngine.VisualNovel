using System;
using System.Linq;
using System.Numerics;
using Inno.Core.Serialization;
using Inno.Rendering.Assets;
using Inno.Rendering;
using Inno.VisualNovel.Authoring.CharacterEditing;
using Inno.VisualNovel.Authoring.StoryEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Flow;
using Inno.VisualNovel.Model.Story.Nodes.Stage;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Stage.Composition;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class CharacterAnimationContracts
{
    internal static void PureFramesAndBreathing(SerializationRegistry native)
    {
        var character = Definition(); var a = new StageController(character.textures); var b = new StageController(character.textures);
        var placement = new Model.Stages.StageTransform { x = 100, y = 200, scaleX = -1, rotation = 30 };
        a.ShowCharacter("lead", character, "", "", placement); b.ShowCharacter("lead", character, "", "", placement);
        a.SetCharacterSpeaking("lead", true); b.SetCharacterSpeaking("lead", true);
        a.Tick(0.1); a.Tick(0.2); b.Tick(0.3);
        var left = a.BuildFrame(1920, 1080); var right = b.BuildFrame(1920, 1080);
        Check(left.items.Zip(right.items).All(pair => pair.First.texture!.identity.persistentId == pair.Second.texture!.identity.persistentId
            && pair.First.topLeft == pair.Second.topLeft), "Character animation depended on render subdivision or floating-point frame boundaries.");
        Check(Item(a, "sequence").texture!.identity.persistentId == character.textures[1].identity.persistentId
            && Item(a, "mouth").texture!.identity.persistentId == character.textures[2].identity.persistentId,
            "Sequence cycle or explicit mouth timing selected the wrong frame.");
        StageState at = a.Capture(); at.time = 1; at.characters[0].breathingPhase = 0; a.Restore(at);
        Check(Item(a, "eyes").texture!.identity.persistentId == character.textures[1].identity.persistentId, "A blink did not begin after its authored idle interval.");
        var breathing = Item(a, "body"); var neutral = a.Capture(); neutral.characters[0].breathing.enabled = false; a.Restore(neutral);
        var still = Item(a, "body");
        Check(Vector2.Distance(breathing.bottomLeft, still.bottomLeft) < 0.001 && Vector2.Distance(breathing.topLeft, still.topLeft) > 1,
            "Breathing moved its anchored foot or ignored mirrored rotated character coordinates.");
        a.Restore(at); a.Tick(0.05); Check(Item(a, "eyes").texture!.identity.persistentId == character.textures[2].identity.persistentId, "Blink frame durations were not honored.");
        a.Tick(0.05); Check(Item(a, "eyes").texture!.identity.persistentId == character.textures[0].identity.persistentId, "Completed blink did not return to the current pose image.");
        StageState saved = native.Deserialize<StageState>(native.Serialize(a.Capture())); var before = a.BuildFrame(1920, 1080);
        a.Tick(5); a.Restore(saved);
        Check(a.BuildFrame(1920, 1080).items.Zip(before.items).All(pair => pair.First.texture!.identity.persistentId == pair.Second.texture!.identity.persistentId && pair.First.topLeft == pair.Second.topLeft),
            "Native animation state did not restore exact images and breathing phase.");
        character.poses[0].parts.Single(static part => part.id == "sequence").animation.frames[0].texture = 3;
        character.breathing.amplitude = 0.1;
        a.Restore(at);
        Check(a.Capture().characters[0].breathing.amplitude == 0.02 && a.Capture().characters[0].parts.Single(static part => part.layer.EndsWith("/sequence", StringComparison.Ordinal)).frames[0].texture != character.textures[3].identity.persistentId,
            "A running stage retained aliases to editable character declarations.");
        a.Tick(0, true);
        Check(Item(a, "eyes").texture!.identity.persistentId == character.textures[0].identity.persistentId
            && Item(a, "mouth").texture!.identity.persistentId != character.textures[0].identity.persistentId,
            "Reduced motion did not distinguish optional blinking from explicitly essential mouth frames.");
        a.SetCharacterSpeaking("lead", false);
        Check(Item(a, "mouth").texture!.identity.persistentId == character.textures[0].identity.persistentId, "End-speaking retained an animated mouth frame.");
        a.HideCharacter("lead"); Check(!a.IsCharacterShown("lead") && a.Capture().characters.Length == 0, "Hidden characters retained animation owners.");
    }

    internal static void AtomicValidationAndEditing()
    {
        var character = Definition(); var stage = new StageController(character.textures); stage.ShowCharacter("lead", character, "", "", new());
        var source = stage.Capture(); var before = Item(stage, "mouth").texture!.identity.persistentId;
        source.characters[0].parts[0].frames[0].texture = Guid.NewGuid(); Reject(() => stage.Restore(source));
        Check(Item(stage, "mouth").texture!.identity.persistentId == before, "Rejected animation images partially replaced the active composition.");
        source = stage.Capture(); source.characters[0].parts = [source.characters[0].parts[0], source.characters[0].parts[0].Copy()]; Reject(() => stage.Restore(source));
        source = stage.Capture(); source.characters = []; Reject(() => stage.Restore(source));
        Reject(() => stage.ShowCharacter("lead/child", character, "", "", new()));
        Check(stage.IsCharacterShown("lead"), "Invalid nested instance names removed an existing owner.");
        var original = character.poses[0].parts.Single(static part => part.id == "sequence");
        string copied = CharacterEditService.DuplicatePose(character, character.defaultPose);
        var duplicate = character.poses.Single(pose => pose.id == copied).parts.Single(static part => part.id == "sequence");
        original.animation.frames[0].seconds = 0.7;
        Check(duplicate.animation.frames[0].seconds == 0.1, "Pose duplication aliased frame timing.");
        CharacterAnimationEditService.MoveFrame(duplicate.animation, 0, 1); Check(duplicate.animation.frames[0].texture == 2, "Moving frames lost ordered sequence identity.");
        CharacterAnimationEditService.RemoveFrame(duplicate.animation, 1); CharacterAnimationEditService.RemoveFrame(duplicate.animation, 0);
        Check(duplicate.animation.mode == CharacterAnimationMode.None && duplicate.animation.frames.Length == 0, "Removing the final frame left an invalid active animation.");
        CharacterAnimationEditService.AddFrame(character, duplicate, 1); Check(duplicate.animation.mode == CharacterAnimationMode.Sequence, "Adding a frame did not create a usable sequence.");
        original.animation.frames[0].seconds = double.NaN; Reject(() => CharacterAnimationRules.Validate(character));
    }

    internal static void NarrativeMarkersAndCancellation(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var character = Definition(); var edits = new StoryEditService(codec); var draft = FirstStoryTemplate.Create(codec);
        var show = edits.Add(draft, "opening", StoryNodeIds.stage, new StageNode { operation = StageOperation.ShowCharacter, resourceId = character.resourceId, target = "lead" }, new(0, 300));
        var talk = edits.Add(draft, "opening", StoryNodeIds.stage, new StageNode { operation = StageOperation.BeginSpeaking, target = "lead" }, new(0, 400));
        var wait = edits.Add(draft, "opening", StoryNodeIds.wait, new WaitNode { seconds = 0.05 }, new(0, 500));
        var stop = edits.Add(draft, "opening", StoryNodeIds.stage, new StageNode { operation = StageOperation.EndSpeaking, target = "lead" }, new(0, 600));
        edits.Connect(draft, new(new("entry"), new("next")), show); edits.Connect(draft, new(show, new("next")), talk); edits.Connect(draft, new(talk, new("next")), wait); edits.Connect(draft, new(wait, new("next")), new("hello"));
        edits.Insert(draft, draft.graph.document.edges.Single(edge => edge.output.nodeId.value == "hello").id, stop);
        var chapter = draft.ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        using var session = new NovelSession(new NovelProjectAsset { chapters = [chapter], startChapter = chapter.chapterId, characters = [character] }); session.StartNew(); session.Tick(0.02);
        var saved = native.Deserialize<NovelSessionState>(native.Serialize(session.Capture()));
        session.Tick(0.1); session.Restore(saved);
        Check(session.stage.Capture().characters.Single().speaking && session.story!.status == StoryStatus.Timer, "Native session restoration lost authored speaking markers or timing.");
        session.Tick(0.03); session.Advance(); session.Advance();
        Check(!session.stage.Capture().characters.Single().speaking && session.story!.status == StoryStatus.Choice, "End-speaking marker did not pass through the production presentation coordinator.");
        Check(session.Rollback() && session.stage.Capture().characters.Single().speaking, "Rollback did not restore the complete animated dialogue boundary.");

        draft = FirstStoryTemplate.Create(codec); var fork = edits.AddParallel(draft, "opening", new(0, 300));
        var parallel = codec.Read<ParallelNode>(draft.graph.document.FindNode(fork)!); parallel.waitAny = true; edits.SetSettings(draft, fork, parallel);
        var first = draft.graph.document.edges.Single(edge => edge.output.nodeId == fork && edge.output.portId.value == parallel.branches[0]).input.nodeId;
        var second = draft.graph.document.edges.Single(edge => edge.output.nodeId == fork && edge.output.portId.value == parallel.branches[1]).input.nodeId;
        edits.SetSettings(draft, first, new WaitNode { seconds = 0.05 }); edits.SetSettings(draft, second, new WaitNode { seconds = 10 });
        show = edits.Add(draft, "opening", StoryNodeIds.stage, new StageNode { operation = StageOperation.ShowCharacter, resourceId = character.resourceId, target = "lead" }, new(0, 400));
        talk = edits.Add(draft, "opening", StoryNodeIds.stage, new StageNode { operation = StageOperation.BeginSpeaking, target = "lead" }, new(0, 500));
        edits.Connect(draft, new(new("entry"), new("next")), show); edits.Connect(draft, new(show, new("next")), fork);
        edits.Connect(draft, new(fork, new(parallel.branches[1])), talk); edits.Connect(draft, new(talk, new("next")), second);
        edits.Connect(draft, new(new(parallel.joinId), new("next")), new("hello"));
        chapter = draft.ToAsset(); var compiled = compiler.Compile(chapter); Check(compiled.success, string.Join(";", compiled.diagnostics.Select(static item => item.message))); chapter.program = compiled.program;
        using var tasks = new NovelSession(new() { chapters = [chapter], startChapter = chapter.chapterId, characters = [character] }); tasks.StartNew();
        saved = native.Deserialize<NovelSessionState>(native.Serialize(tasks.Capture()));
        Check(tasks.stage.Capture().characters.Single().speaking && saved.narrative.tasks.Any(task => task.execution.ownedResources.Any(static resource => resource.kind == "speaking")),
            "A pending mouth marker lost its task-local cancellation owner.");
        tasks.Tick(0.06); Check(!tasks.stage.Capture().characters.Single().speaking && tasks.story!.status == StoryStatus.Dialogue, "Wait-any cancellation left an unfinished task's mouth animation running.");
        tasks.Restore(saved); tasks.Tick(0.06); Check(!tasks.stage.Capture().characters.Single().speaking, "Restored parallel cancellation lost speaking cleanup.");
    }
    private static CharacterAsset Definition() => new()
    {
        resourceId = "actor", textures = Enumerable.Range(0, 4).Select(static _ => new TextureAsset(1, 1, TextureColorSpace.Srgb, "png")).ToArray(), breathing = new() { enabled = true, amplitude = 0.02, period = 4 },
        poses = [new() { parts = [
            new() { id = "body", texture = 0, width = 100, height = 200, color = 0xFFEEDDFF },
            new() { id = "eyes", texture = 0, width = 20, height = 10, order = 1, transform = new() { x = 10, y = 30 }, animation = new() { mode = CharacterAnimationMode.Blink, blinkInterval = 1, stagger = false,
                frames = [new() { texture = 1, seconds = 0.05 }, new() { texture = 2, seconds = 0.05 }] } },
            new() { id = "mouth", texture = 0, width = 20, height = 10, order = 2, transform = new() { x = 10, y = 60 }, animation = new() { mode = CharacterAnimationMode.Talking, essential = true,
                frames = [new() { texture = 1, seconds = 0.1 }, new() { texture = 2, seconds = 0.1 }] } },
            new() { id = "sequence", texture = 0, width = 10, height = 10, order = 3, transform = new() { x = 60, y = 100 }, animation = new() { mode = CharacterAnimationMode.Sequence, stagger = false,
                frames = [new() { texture = 1, seconds = 0.1 }, new() { texture = 2, seconds = 0.2 }] } }
        ] }], expressions = [new()]
    };
    private static StageDrawItem Item(
        StageController stage,
        string part
    ) => stage.BuildFrame(1920, 1080).items.Single(item => item.id == "character/lead/" + part);
    private static void Reject(Action action)
    {
        try { action(); } catch (Exception error) when (error is InvalidOperationException or ArgumentException) { return; }
        throw new InvalidOperationException("Invalid character animation was accepted.");
    }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}
