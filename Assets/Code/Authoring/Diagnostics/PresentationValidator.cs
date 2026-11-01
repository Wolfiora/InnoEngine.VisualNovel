using System;
using System.Linq;
using Inno.VisualNovel.Authoring.UiEditing;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story;

namespace Inno.VisualNovel.Authoring.Diagnostics;

/// <summary>Checks that authored reading screens expose the controls required by their compiled story instructions.</summary>
public static class PresentationValidator
{
    /// <summary>Rejects missing narrative bindings before a game can become stuck on an uneditable reading state.</summary>
    /// <param name="project">The imported game with explicit compiled chapter and screen roots.</param>
    public static void Validate(NovelProjectAsset project)
    {
        ScreenAsset reading = project.screens.SingleOrDefault(static item => item.role == "reading")?.screen
            ?? throw new InvalidOperationException("The editable reading screen is missing.");
        var elements = ScreenEditService.Flatten(reading).Select(static item => item.element).ToArray();
        var translations = project.localization?.entries.ToDictionary(static item => item.id, StringComparer.Ordinal);
        foreach (var binding in project.screens)
        {
            if (binding.screen is not { } screen) continue;
            ScreenTextValidator.Validate(screen);
            foreach (var element in ScreenEditService.Flatten(screen).Select(static item => item.element).Where(static item => item.richText))
                Translations(screen, element.textId, element.text, binding.role + "/" + element.id);
        }
        foreach (var chapter in project.chapters)
            foreach (var instruction in chapter.program!.instructions)
            {
                string location = chapter.chapterId + "/" + instruction.nodeId + " → " + reading.name;
                if (instruction.dialogue is { } richLine)
                {
                    ScreenTextValidator.ValidateText(reading, richLine.text, location);
                    Translations(reading, Model.Localization.StoryTextKeys.Dialogue(richLine.textId), richLine.text, location);
                }
                if (instruction.dialogue is { voiceCueId.Length: > 0 } line && !project.audio.Any(cue => cue.resourceId == line.voiceCueId
                    && cue.channel == Model.Audio.NovelAudioChannel.Voice && !cue.loop))
                    throw new InvalidOperationException(location + ": assign a declared non-looping Voice cue: " + line.voiceCueId);
                if (instruction.definitionId == StoryNodeIds.dialogue && !elements.Any(static item => item.binding == "dialogue.text"))
                    throw new InvalidOperationException(location + ": bind a reading text element to dialogue.text.");
                if (instruction.dialogue?.presentation == Model.Story.Nodes.Reading.DialoguePresentation.Nvl && !elements.Any(static item => item.binding == "dialogue.pageText"))
                    throw new InvalidOperationException(location + ": bind an NVL page element to dialogue.pageText.");
                if (instruction.definitionId == StoryNodeIds.choice && (!elements.Any(static item => item.repeatBinding == "choices")
                    || !elements.Any(static item => item.action == "choice" && item.argumentBinding == "item.id")))
                    throw new InvalidOperationException(location + ": provide a choices collection and a choice action bound to item.id.");
                if (instruction.definitionId == StoryNodeIds.input && (!elements.Any(static item => item.kind == UiElementKind.Input && item.binding == "input.value")
                    || !elements.Any(static item => item.action == "input.submit")
                    || instruction.input!.allowCancel && !elements.Any(static item => item.action == "input.cancel")))
                    throw new InvalidOperationException(location + ": provide input.value, input.submit and the declared input.cancel action.");
            }

        void Translations(ScreenAsset screen, string id, string source, string location)
        {
            if (translations is null || !translations.TryGetValue(id, out var row) || row.source != source) return;
            foreach (var translation in row.translations.Where(item => item.status != Model.Localization.TranslationStatus.Draft
                && item.sourceFingerprint == Player.Localization.NovelLocalizationService.Fingerprint(source)))
                ScreenTextValidator.ValidateText(screen, translation.text, location + "/" + translation.language);
        }
    }
}
