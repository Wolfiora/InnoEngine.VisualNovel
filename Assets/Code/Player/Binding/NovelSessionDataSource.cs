using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Values;
using Inno.VisualNovel.Narrative.Execution;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.UI.Binding;
using Inno.VisualNovel.Model.Audio;
using Inno.VisualNovel.Model.Text;
using System.Globalization;

namespace Inno.VisualNovel.Player.Binding;

/// <summary>Exposes the current session through typed screen bindings.</summary>
public sealed class NovelSessionDataSource(NovelSession session) : UiDataSource
{
    private readonly KeyboardSettingsDataSource m_keyboard = new(() => session.preferences, (id, text) => session.localization.Resolve(id, text));
    /// <inheritdoc />
    public override Inno.VisualNovel.UI.Text.UiRichText? ReadRichText(
        string path,
        UiDataRow? row
    ) => session.interactiveStory?.status != StoryStatus.Dialogue ? null : path switch
    {
        "dialogue.text" => new(session.interactiveStory.dialogueDocument, session.interactiveStory.visibleGraphemes),
        "dialogue.pageText" => new(session.interactiveStory.readingPage, session.interactiveStory.visiblePageGraphemes),
        "dialogue.speaker" when session.currentCharacter is { } character => Speaker(character.nameColor),
        _ => null
    };
    private Inno.VisualNovel.UI.Text.UiRichText Speaker(uint color)
    {
        var document = NovelTextMarkup.Parse("[color=#" + color.ToString("X8", CultureInfo.InvariantCulture) + "]" + NovelTextMarkup.Escape(session.interactiveStory!.speaker) + "[/color]");
        return new(document, document.elements.Count);
    }
    /// <inheritdoc />
    public override InnoEngine.Rendering.TextureAsset? ReadImage(
        string path,
        UiDataRow? row
    ) => path == "dialogue.portrait" ? session.currentCharacter?.portrait : null;
    /// <inheritdoc />
    public override int GetTextLimit(
        string path,
        int authoredLimit
    ) => path == "input.value" && session.interactiveStory?.status == StoryStatus.Input ? Math.Min(authoredLimit, session.interactiveStory.inputMaximumLength) : authoredLimit;
    /// <inheritdoc />
    public override bool TryRead(string path, out NovelValue value)
    {
        if (m_keyboard.TryRead(path, out value)) return true;
        if (AudioChannel(path) is { } channel) { value = NovelValue.Number(session.preferences.GetVolume(channel)); return true; }
        value = path switch
        {
            "game.title" => NovelValue.String(session.title),
            "game.theme" => NovelValue.String(session.themeId),
            "dialogue.speaker" => NovelValue.String(session.interactiveStory?.speaker ?? string.Empty),
            "dialogue.characterId" => NovelValue.String(session.interactiveStory?.characterId ?? string.Empty),
            "dialogue.hasPortrait" => NovelValue.Boolean(session.currentCharacter?.portrait is not null),
            "dialogue.text" => NovelValue.String(session.interactiveStory?.visibleText ?? string.Empty),
            "dialogue.pageText" => NovelValue.String(session.interactiveStory?.readingPage.Prefix(session.interactiveStory.visiblePageGraphemes) ?? string.Empty),
            "dialogue.adv" => NovelValue.Boolean(Layout(Model.Story.Nodes.Reading.DialoguePresentation.Adv)),
            "dialogue.nvl" => NovelValue.Boolean(Layout(Model.Story.Nodes.Reading.DialoguePresentation.Nvl)),
            "dialogue.bubble" => NovelValue.Boolean(Layout(Model.Story.Nodes.Reading.DialoguePresentation.Bubble)),
            "dialogue.overlay" => NovelValue.Boolean(Layout(Model.Story.Nodes.Reading.DialoguePresentation.Overlay)),
            "dialogue.letter" => NovelValue.Boolean(Layout(Model.Story.Nodes.Reading.DialoguePresentation.Letter)),
            "dialogue.positionX" => NovelValue.Number(Math.Min(session.interactiveStory?.dialoguePlacement.x ?? 0, 1 - (session.interactiveStory?.dialoguePlacement.width ?? 1))),
            "dialogue.positionY" => NovelValue.Number(session.interactiveStory?.dialoguePlacement.y ?? 0),
            "dialogue.widthRatio" => NovelValue.Number(session.interactiveStory?.dialoguePlacement.width ?? 1),
            "dialogue.visible" => NovelValue.Boolean(session.interactiveStory?.status == StoryStatus.Dialogue && !session.hidden),
            "reading.advanceVisible" => NovelValue.Boolean(!session.hidden && session.interactiveStory?.status is (StoryStatus.Dialogue or StoryStatus.AdvanceWait)),
            "reading.waitingForAdvance" => NovelValue.Boolean(!session.hidden && session.interactiveStory?.status == StoryStatus.AdvanceWait),
            "choice.visible" => NovelValue.Boolean(session.interactiveStory?.status == StoryStatus.Choice),
            "choice.timed" => NovelValue.Boolean(session.interactiveStory?.isTimedChoice ?? false),
            "choice.timeRemaining" => NovelValue.Number(session.interactiveStory?.choiceTimeRemaining ?? 0),
            "choice.timeRemainingRatio" => NovelValue.Number(session.interactiveStory?.choiceTimeRemainingRatio ?? 0),
            "choice.remainingSeconds" => NovelValue.Integer(WholeSeconds(session.interactiveStory?.choiceTimeRemaining ?? 0)),
            "input.visible" => NovelValue.Boolean(session.interactiveStory?.status == StoryStatus.Input),
            "input.canCancel" => NovelValue.Boolean(session.interactiveStory?.canCancelInput ?? false),
            "input.valid" => NovelValue.Boolean(session.interactiveStory?.isInputValid ?? false),
            "input.prompt" => NovelValue.String(session.interactiveStory?.inputPrompt ?? string.Empty),
            "input.value" => NovelValue.String(session.interactiveStory?.inputText ?? string.Empty),
            "reading.toolbarVisible" => NovelValue.Boolean(session.interactiveStory?.status != StoryStatus.Input),
            "game.completed" => NovelValue.Boolean(session.interactiveStory?.status == StoryStatus.Completed),
            "game.faulted" => NovelValue.Boolean(session.interactiveStory?.status == StoryStatus.Faulted),
            "game.error" => NovelValue.String(session.interactiveStory?.error ?? string.Empty),
            "game.active" => NovelValue.Boolean(session.interactiveStory is not null),
            "game.replay" => NovelValue.Boolean(session.isReplay),
            "game.normal" => NovelValue.Boolean(!session.isReplay),
            "command.active" => NovelValue.Boolean(session.interactiveStory?.status == StoryStatus.Command),
            "command.cancelable" => NovelValue.Boolean(session.interactiveStory?.canCancelCommand ?? false),
            "stage.empty" => NovelValue.Boolean(!session.stage.hasContent),
            "history.canRollback" => NovelValue.Boolean(session.canRollback),
            "history.barrierReason" => NovelValue.String(session.interactiveStory?.rollbackBarrierReason ?? string.Empty),
            "history.hasBarrier" => NovelValue.Boolean(session.interactiveStory?.rollbackBarrierReason.Length > 0),
            "settings.textSpeed" => NovelValue.Number(session.preferences.textSpeed),
            "settings.language" => NovelValue.String(session.localization.language),
            "choice.prompt" => NovelValue.String(session.interactiveStory?.choicePrompt ?? string.Empty),
            "settings.autoDelay" => NovelValue.Number(session.preferences.autoDelay),
            "settings.fontScale" => NovelValue.Number(session.preferences.fontScale),
            "settings.masterVolume" => NovelValue.Number(session.preferences.masterVolume),
            "settings.reducedMotion" => NovelValue.Boolean(session.preferences.reducedMotion),
            "settings.highContrast" => NovelValue.Boolean(session.preferences.highContrast),
            "settings.allowFlashing" => NovelValue.Boolean(session.preferences.allowFlashing),
            "settings.skipReadOnly" => NovelValue.Boolean(session.preferences.skipReadOnly),
            "settings.stopVoiceOnAdvance" => NovelValue.Boolean(session.preferences.stopVoiceOnAdvance),
            _ => NovelValue.String(string.Empty)
        };
        if (path.StartsWith("variables.", StringComparison.Ordinal) && session.interactiveStory is not null)
        {
            value = session.interactiveStory.GetVariable(path[10..]);
            return true;
        }
        return path is "reading.advanceVisible" or "reading.waitingForAdvance" or "choice.timed" or "choice.timeRemaining" or "choice.timeRemainingRatio" or "choice.remainingSeconds"
            or "dialogue.characterId" or "dialogue.hasPortrait" or "history.barrierReason" or "history.hasBarrier" or "game.theme" or "dialogue.pageText" or "dialogue.adv" or "dialogue.nvl" or "dialogue.bubble" or "dialogue.overlay" or "dialogue.letter" or "dialogue.positionX" or "dialogue.positionY" or "dialogue.widthRatio"
            or "command.active" or "command.cancelable" or "game.title" or "dialogue.speaker" or "dialogue.text" or "dialogue.visible" or "choice.visible" or "game.completed" or "game.faulted"
            or "game.error" or "game.active" or "game.replay" or "game.normal" or "stage.empty" or "history.canRollback" or "settings.textSpeed" or "settings.autoDelay" or "settings.fontScale" or "settings.masterVolume"
            or "settings.reducedMotion" or "settings.highContrast" or "settings.allowFlashing" or "settings.skipReadOnly" or "settings.stopVoiceOnAdvance" or "settings.language" or "choice.prompt" or "input.visible" or "input.canCancel" or "input.valid" or "input.prompt" or "input.value" or "reading.toolbarVisible";

        bool Layout(Model.Story.Nodes.Reading.DialoguePresentation mode) => session.interactiveStory?.status == StoryStatus.Dialogue && session.interactiveStory.presentation == mode && !session.hidden;
        static long WholeSeconds(double seconds) => seconds >= long.MaxValue ? long.MaxValue : (long)Math.Ceiling(seconds);
    }

    /// <inheritdoc />
    public override bool TryWrite(string path, NovelValue value)
    {
        if (m_keyboard.TryWrite(path, value)) return true;
        if (path == "input.value" && value.kind == NovelValueKind.String) return session.interactiveStory?.SetInputText(value.text) ?? false;
        if (AudioChannel(path) is { } channel && Numeric(value, 0, 1)) { session.preferences.SetVolume(channel, value.number); return true; }
        if (path.StartsWith("variables.", StringComparison.Ordinal) && session.interactiveStory is not null)
        { session.interactiveStory.SetVariable(path[10..], value); return true; }
        switch (path)
        {
            case "game.theme" when value.kind == NovelValueKind.String: session.SetTheme(value.text); return true;
            case "settings.language" when value.kind == NovelValueKind.String: session.SetLanguage(value.text); return true;
            case "settings.textSpeed" when Numeric(value, 0.1, 5): session.preferences.textSpeed = value.number; return true;
            case "settings.autoDelay" when Numeric(value, 0.1, 10): session.preferences.autoDelay = value.number; return true;
            case "settings.fontScale" when Numeric(value, 0.75, 2): session.preferences.fontScale = value.number; return true;
            case "settings.masterVolume" when Numeric(value, 0, 1): session.preferences.masterVolume = value.number; return true;
            case "settings.reducedMotion" when value.kind == NovelValueKind.Boolean:
                session.preferences.reducedMotion = value.boolean; session.stage.Tick(0, session.preferences.reducedMotion, session.preferences.allowFlashing); return true;
            case "settings.allowFlashing" when value.kind == NovelValueKind.Boolean:
                session.preferences.allowFlashing = value.boolean; session.stage.Tick(0, session.preferences.reducedMotion, session.preferences.allowFlashing); return true;
            case "settings.skipReadOnly" when value.kind == NovelValueKind.Boolean: session.preferences.skipReadOnly = value.boolean; return true;
            case "settings.highContrast" when value.kind == NovelValueKind.Boolean: session.preferences.highContrast = value.boolean; return true;
            case "settings.stopVoiceOnAdvance" when value.kind == NovelValueKind.Boolean: session.preferences.stopVoiceOnAdvance = value.boolean; return true;
            default: return false;
        }
    }
    /// <inheritdoc />
    public override bool TryWrite(
        string path,
        NovelValue value,
        UiDataRow? row
    ) {
        if (path == "item.shortcutKey") return m_keyboard.TryWrite(path, value, row);
        if (path is not ("item.voiceVolume" or "item.voiceEnabled")) return base.TryWrite(path, value, row);
        if (row is null || !session.characterNames.Any(character => character.id == row.key)) return false;
        var policy = session.preferences.GetCharacterVoice(row.key);
        if (path == "item.voiceVolume" && Numeric(value, 0, 1)) policy.volume = value.number;
        else if (path == "item.voiceEnabled" && value.kind == NovelValueKind.Boolean) policy.enabled = value.boolean;
        else return false;
        session.preferences.SetCharacterVoice(policy); session.audio?.SetCharacterMix(session.preferences.characterVoices); return true;
    }

    /// <inheritdoc />
    public override IReadOnlyList<UiDataRow> ReadRows(string path)
    {
        if (path is "settings.shortcuts" or "settings.shortcutKeys" or "settings.holdSkipChoices" or "settings.navigationShortcuts" or "settings.navigationKeys") return m_keyboard.ReadRows(path);
        if (path == "characters.voices") return session.characterNames.Select(character =>
        {
            var voice = session.preferences.GetCharacterVoice(character.id);
            return new UiDataRow(character.id, new Dictionary<string, NovelValue> { ["id"] = NovelValue.String(character.id), ["name"] = NovelValue.String(character.name),
                ["voiceVolume"] = NovelValue.Number(voice.volume), ["voiceEnabled"] = NovelValue.Boolean(voice.enabled) });
        }).ToArray();
        if (path == "checkpoints") return session.namedCheckpoints.Select(name => new UiDataRow(name, new Dictionary<string, NovelValue>
        { ["id"] = NovelValue.String(name), ["name"] = NovelValue.String(name), ["text"] = NovelValue.String(name), ["value"] = NovelValue.String(name) })).ToArray();
        if (path == "themes") return session.themes.Select(theme => new UiDataRow(theme.id, new Dictionary<string, NovelValue>
        { ["id"] = NovelValue.String(theme.id), ["name"] = NovelValue.String(theme.title), ["text"] = NovelValue.String(theme.title), ["value"] = NovelValue.String(theme.id), ["selected"] = NovelValue.Boolean(theme.id == session.themeId) })).ToArray();
        if (path == "languages") return session.localization.languages.Select(language => new UiDataRow(language.id, new Dictionary<string, NovelValue>
        { ["id"] = NovelValue.String(language.id), ["name"] = NovelValue.String(language.name), ["text"] = NovelValue.String(language.name), ["value"] = NovelValue.String(language.id), ["selected"] = NovelValue.Boolean(language.id == session.localization.language) })).ToArray();
        if (path == "choices") return session.interactiveStory?.choices.Select(option => new UiDataRow(option.id, new Dictionary<string, NovelValue>
        {
            ["id"] = NovelValue.String(option.id), ["text"] = NovelValue.String(option.text),
            ["enabled"] = NovelValue.Boolean(option.enabled), ["reason"] = NovelValue.String(option.disabledReason), ["hasReason"] = NovelValue.Boolean(option.disabledReason.Length > 0), ["selected"] = NovelValue.Boolean(option.selected)
        })).ToArray() ?? [];
        if (path == "history") return session.history.Select((line, index) => new UiDataRow(index.ToString(System.Globalization.CultureInfo.InvariantCulture), new Dictionary<string, NovelValue>
        {
            ["id"] = NovelValue.String(line.nodeId), ["text"] = NovelValue.String(line.speaker + "\n" + line.text),
            ["index"] = NovelValue.String(index.ToString(System.Globalization.CultureInfo.InvariantCulture)), ["voiced"] = NovelValue.Boolean(line.voiceCueId.Length > 0)
        })).ToArray();
        return [];
    }

    private static bool Numeric(NovelValue value, double minimum, double maximum) => value.kind is NovelValueKind.Number or NovelValueKind.Integer && value.number >= minimum && value.number <= maximum;
    private static NovelAudioChannel? AudioChannel(string path) => path switch
    {
        "settings.bgmVolume" => NovelAudioChannel.Bgm, "settings.ambienceVolume" => NovelAudioChannel.Ambience, "settings.sfxVolume" => NovelAudioChannel.Sfx,
        "settings.voiceVolume" => NovelAudioChannel.Voice, "settings.uiVolume" => NovelAudioChannel.Ui, _ => null
    };
}
