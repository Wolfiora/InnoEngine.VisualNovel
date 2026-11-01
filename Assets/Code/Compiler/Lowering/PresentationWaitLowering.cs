using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Programs;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Story.Nodes.Stage;

namespace Inno.VisualNovel.Compiler.Lowering;

internal static class PresentationWaitLowering
{
    internal static IReadOnlyList<StoryInstruction> Expand(IReadOnlyList<StoryInstruction> source)
    {
        var ids = source.Select(static item => item.nodeId).ToHashSet(StringComparer.Ordinal); var result = new List<StoryInstruction>();
        foreach (var instruction in source)
        {
            result.Add(instruction);
            bool screen = instruction.screenEffect is { operation: ScreenEffectOperation.Fade, waitForCompletion: true };
            bool transition = instruction.transition is { operation: TransitionOperation.Begin, waitForCompletion: true };
            if (!screen && !transition) continue;
            string prefix = "inno.vn.generated.wait/" + instruction.nodeId.Length + ":" + instruction.nodeId, id = prefix; int suffix = 0;
            while (!ids.Add(id)) id = prefix + "/" + ++suffix;
            var wait = new StoryInstruction { nodeId = id, sourceChapterId = instruction.sourceChapterId, sourceNodeId = instruction.sourceNodeId,
                definitionId = instruction.definitionId, targets = instruction.targets };
            if (screen)
            {
                wait.screenEffect = new() { operation = ScreenEffectOperation.Wait, instanceId = instruction.screenEffect!.instanceId };
                instruction.screenEffect.waitForCompletion = false;
            }
            else { wait.transition = new() { operation = TransitionOperation.Wait }; instruction.transition!.waitForCompletion = false; }
            instruction.targets = [new() { port = "next", nodeId = id }]; result.Add(wait);
        }
        return result;
    }
}
