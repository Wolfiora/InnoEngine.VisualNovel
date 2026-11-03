using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Authoring.UiEditing;

/// <summary>Describes implemented shell binding paths and actions for no-code authoring selectors.</summary>
public static class BuiltInUiChoices
{
    /// <summary>Gets typed page-owned paths for ordinary control and layout selectors.</summary>
    /// <param name="variables">The current page's declarations.</param>
    /// <param name="kind">An optional required kind; Number also includes Integer.</param>
    /// <returns>Page-prefixed fields with explicit local or saved lifetime labels.</returns>
    public static IEnumerable<(string id, string name)> PageFields(
        VariableDefinition[] variables,
        NovelValueKind? kind = null
    ) => variables.Where(field => kind is null || field.initialValue.kind == kind || kind == NovelValueKind.Number && field.initialValue.kind == NovelValueKind.Integer)
        .Select(static field => ("page." + field.id, "Page · " + field.name + (field.scope == VariableScope.Game ? " · Saved with game" : " · Current screen only") + " · " + field.id));
    /// <summary>Gets observable numeric fields usable by layout bindings, including positioned dialogue.</summary>
    /// <param name="variables">The game's declared numeric variables.</param>
    /// <returns>Numeric settings, variables and read-only reading geometry.</returns>
    public static IEnumerable<(string id, string name)> Geometry(VariableDefinition[] variables) => NumericValues(variables).Concat(new (string, string)[]
    { ("dialogue.positionX", "Reading · Horizontal position ratio"), ("dialogue.positionY", "Reading · Vertical position ratio"), ("dialogue.widthRatio", "Reading · Panel width ratio") });
    /// <summary>Gets writable Boolean fields for toggle controls.</summary>
    /// <param name="variables">Detached game declarations.</param>
    /// <returns>Writable Boolean bindings, excluding observation-only conditions.</returns>
    public static IEnumerable<(string id, string name)> WritableBooleans(VariableDefinition[] variables) => Conditions(variables).Where(static option => option.id.Length == 0
        || option.id.StartsWith("variables.", System.StringComparison.Ordinal) || option.id is "item.voiceEnabled" or "settings.allowFlashing" or "settings.reducedMotion" or "settings.highContrast" or "settings.skipReadOnly" or "settings.stopVoiceOnAdvance" or "music.repeat" or "music.shuffle");
    /// <summary>Gets writable plain strings for text inputs.</summary>
    /// <param name="variables">Detached game declarations.</param>
    /// <returns>String inputs; enums use typed selectors.</returns>
    public static IEnumerable<(string id, string name)> WritableText(VariableDefinition[] variables) => new (string, string)[] { ("", "Unbound"), ("input.value", "Input · Player text"), ("collections.search", "Collection · Search text") }
        .Concat(variables.Where(static variable => variable.initialValue.kind == NovelValueKind.String).Select(static variable => ("variables." + variable.id, "Variable · " + variable.name + " · " + variable.id)));
    /// <summary>Gets read-only and writable String fields for plain tooltip content.</summary>
    /// <param name="variables">The selected game's typed declarations.</param>
    /// <returns>String paths without Boolean, enum, numeric or list coercion.</returns>
    public static IEnumerable<(string id, string name)> TextValues(VariableDefinition[] variables) => Values([]).Where(option =>
        !Conditions([]).Any(flag => flag.id.Length > 0 && flag.id == option.id)
        && !NumericValues([]).Any(number => number.id.Length > 0 && number.id == option.id)
        && option.id is not ("history.page" or "history.count" or "saves.page" or "collections.page" or "collection.imagePage" or "collection.imageCount" or "item.shortcutKey" or "settings.holdSkipModifiers"))
        .Concat(variables.Where(static variable => variable.initialValue.kind == NovelValueKind.String).Select(static variable => ("variables." + variable.id, "Variable · " + variable.name + " · " + variable.id)));
    /// <summary>Gets implemented writable destinations for typed selectors.</summary>
    /// <param name="variables">Detached game declarations.</param>
    /// <returns>Settings, transient inputs and declared variables.</returns>
    public static IEnumerable<(string id, string name)> SelectorValues(VariableDefinition[] variables) => Values(variables).Where(static option => option.id.Length == 0
        || option.id.StartsWith("variables.", System.StringComparison.Ordinal) || option.id.StartsWith("settings.", System.StringComparison.Ordinal)
        || option.id is "game.theme" or "input.value" or "collections.search" or "music.repeat" or "music.shuffle" or "item.shortcutKey");
    /// <summary>Gets numeric settings and declared numeric variables for sliders and progress displays.</summary>
    /// <param name="variables">Detached game declarations.</param>
    /// <returns>Numeric field paths and author-facing labels.</returns>
    public static IEnumerable<(string id, string name)> Numbers(VariableDefinition[] variables) => Values([]).Where(static option => option.id.Length == 0
        || option.id is "item.voiceVolume" or "settings.textSpeed" or "settings.autoDelay" or "settings.fontScale" or "settings.masterVolume" or "settings.bgmVolume" or "settings.ambienceVolume" or "settings.sfxVolume" or "settings.voiceVolume" or "settings.uiVolume")
        .Concat(variables.Where(static variable => variable.initialValue.kind is NovelValueKind.Number or NovelValueKind.Integer).Select(static variable => ("variables." + variable.id, "Variable · " + variable.name + " · " + variable.id)));
    /// <summary>Gets writable numbers and observation-only numeric fields for progress and layout controls.</summary>
    /// <param name="variables">Detached game declarations.</param>
    /// <returns>Numeric fields that never make timed decisions writable from game UI.</returns>
    public static IEnumerable<(string id, string name)> NumericValues(VariableDefinition[] variables) => Numbers(variables).Concat(new (string, string)[]
    { ("choice.timeRemaining", "Choice · Remaining time"), ("choice.timeRemainingRatio", "Choice · Remaining time ratio"), ("choice.remainingSeconds", "Choice · Remaining whole seconds") });
    /// <summary>Gets literal-free scalar fields, including the selected game's declared variables.</summary>
    /// <param name="variables">Detached shared game declarations.</param>
    /// <returns>Stable field paths and author-facing labels.</returns>
    public static IEnumerable<(string id, string name)> Values(VariableDefinition[] variables) => new (string, string)[]
    {
        ("settings.holdSkipModifiers", "Settings · Hold-to-skip modifiers"),
        ("choice.timeRemaining", "Choice · Remaining time"), ("choice.timeRemainingRatio", "Choice · Remaining time ratio"), ("choice.remainingSeconds", "Choice · Remaining whole seconds"),
        ("", "Unbound · Use authored text"), ("game.title", "Game · Title"), ("dialogue.characterId", "Reading · Current character ID"), ("dialogue.speaker", "Reading · Current speaker"), ("dialogue.text", "Reading · Revealed dialogue"), ("dialogue.pageText", "Reading · Current NVL page"), ("choice.prompt", "Choices · Prompt"),
        ("input.prompt", "Input · Prompt"), ("input.value", "Input · Player text"), ("game.theme", "Interface · Current theme"), ("history.barrierReason", "Rollback · Current barrier reason"), ("settings.language", "Settings · Language"), ("settings.textSpeed", "Settings · Text speed"), ("settings.autoDelay", "Settings · Auto delay"),
        ("settings.fontScale", "Settings · Text scale"), ("settings.masterVolume", "Volume · Master"), ("settings.bgmVolume", "Volume · BGM"), ("settings.ambienceVolume", "Volume · Ambience"),
        ("settings.sfxVolume", "Volume · SFX"), ("settings.voiceVolume", "Volume · Voice"), ("settings.uiVolume", "Volume · UI"), ("game.error", "Diagnostics · Game error"), ("interface.error", "Diagnostics · Custom UI action error"), ("interface.dataError", "Diagnostics · Custom UI data error"), ("confirmation.message", "Confirmation · Message"),
        ("history.page", "History · Current page"), ("history.count", "History · Total entries"), ("profile.error", "Player data · Load or save error"), ("saves.message", "Saves · Result / error"), ("saves.page", "Saves · Current page"), ("collections.search", "Collection · Search text"), ("collections.page", "Collection · Current page"),
        ("collection.title", "Collection details · Name"), ("collection.description", "Collection details · Body"), ("collection.author", "Collection details · Author"), ("collection.variantName", "CG · Current variant name"), ("collection.imagePage", "CG · Current image page"), ("collection.imageCount", "CG · Available image count"), ("music.title", "Music · Current track name"),
        ("item.voiceVolume", "Character voice item · Volume"), ("item.shortcutKey", "Shortcut setting · Key enum"), ("item.text", "List item · Text"), ("item.title", "List item · Title"), ("item.name", "List item · Display name"), ("item.label", "Save item · Slot summary"), ("item.description", "Collection item · Description"), ("item.reason", "Choice · Disabled reason")
    }.Concat(Conditions(variables).Where(static option => option.id.Length > 0)).Concat(variables.Where(static variable => variable.initialValue.kind != NovelValueKind.Boolean)
        .Select(static variable => ("variables." + variable.id, "Variable · " + variable.name + " · " + variable.id)));
    /// <summary>Gets Boolean fields suitable for visibility, toggles and interactivity.</summary>
    /// <param name="variables">Detached shared game declarations.</param>
    /// <returns>Stable Boolean paths and labels.</returns>
    public static IEnumerable<(string id, string name)> Conditions(VariableDefinition[] variables) => new (string, string)[]
    {
        ("settings.highContrast", "Settings · High contrast reading"),
        ("choice.timed", "Choice · Timed decision active"),
        ("reading.advanceVisible", "Reading · Advance surface visible"), ("reading.waitingForAdvance", "Reading · Waiting for player advance"),
        ("", "Unbound · Always enabled"), ("dialogue.hasPortrait", "Reading · Current character has a portrait"), ("dialogue.visible", "Reading · Show dialogue"), ("choice.visible", "Reading · Show choices"), ("input.visible", "Input · Active"), ("input.valid", "Input · Content is valid"),
        ("dialogue.adv", "Reading · ADV layout"), ("dialogue.nvl", "Reading · NVL layout"), ("dialogue.bubble", "Reading · Bubble layout"), ("dialogue.overlay", "Reading · Floating text layout"), ("dialogue.letter", "Reading · Letter layout"),
        ("command.active", "Command · Waiting"), ("command.cancelable", "Command · Can cancel"),
        ("input.canCancel", "Input · Can cancel"), ("history.hasBarrier", "Rollback · Has an irreversible barrier"), ("reading.toolbarVisible", "Reading · Show toolbar"), ("game.active", "Game · Started"), ("game.completed", "Game · Completed"),
        ("game.faulted", "Game · Faulted"), ("game.replay", "Game · Replaying"), ("game.normal", "Game · Main session"), ("stage.empty", "Stage · Empty"), ("history.canRollback", "History · Can roll back"), ("history.previous", "History · Has previous page"), ("history.next", "History · Has next page"), ("history.empty", "History · Empty"), ("history.followLatest", "History · Follow latest page end"), ("item.canRollback", "History item · Has a retained rollback position"),
        ("profile.ready", "Player data · Ready"), ("profile.loading", "Player data · Loading"), ("profile.failed", "Player data · Load or save failed"), ("profile.saving", "Player data · Saving"), ("profile.canUseDefaults", "Player data · Can request defaults"), ("profile.confirmDefaults", "Player data · Confirm defaults"), ("settings.allowFlashing", "Settings · Allow flashing"), ("settings.reducedMotion", "Settings · Reduce motion"), ("settings.skipReadOnly", "Settings · Skip read text only"), ("settings.stopVoiceOnAdvance", "Settings · Stop voice on advance"),
        ("saves.ready", "Saves · Service available"), ("saves.canSave", "Saves · Can save"), ("saves.canContinue", "Saves · Has a compatible save to continue"), ("saves.failed", "Saves · Operation failed"), ("saves.previous", "Saves · Has previous page"), ("saves.next", "Saves · Has next page"),
        ("saves.available", "Saves · Host connected"), ("saves.busy", "Saves · Busy"),
        ("collections.empty", "Collection · No search results"), ("collections.previous", "Collection · Has previous page"), ("collections.next", "Collection · Has next page"),
        ("collection.hasImage", "CG · Has available images"), ("collection.imagePrevious", "CG · Has previous unlocked variant"), ("collection.imageNext", "CG · Has next unlocked variant"), ("music.available", "Music · Can play"),
        ("music.paused", "Music · Paused"), ("music.repeat", "Music · Repeat track"), ("music.shuffle", "Music · Shuffle"),
        ("item.voiceEnabled", "Character voice item · Enabled"), ("item.enabled", "Choice · Enabled"), ("item.hasReason", "Choice · Has disabled reason"), ("item.voiced", "History item · Has voice"), ("item.selected", "List item · Selected / current"), ("item.unlocked", "Collection item · Unlocked"),
        ("item.canSave", "Save item · Can save"), ("item.canLoad", "Save item · Can load"), ("item.canDelete", "Save item · Can delete")
    }.Concat(variables.Where(static variable => variable.initialValue.kind == NovelValueKind.Boolean).Select(static variable => ("variables." + variable.id, "Variable · " + variable.name + " · " + variable.id)));
    /// <summary>Gets implemented repeated collections.</summary>
    public static IEnumerable<(string id, string name)> Rows => new (string, string)[]
    { ("", "No repetition"), ("settings.navigationShortcuts", "Menu navigation settings"), ("settings.shortcuts", "Reading shortcut settings"), ("characters.voices", "Character voice settings"), ("choices", "Current story choices"), ("history", "Reading history"), ("checkpoints", "Retained named checkpoints"), ("languages", "Available game languages"), ("themes", "Game theme"), ("saves.slots", "Save slots in current category"), ("collections.items", "Collection items in current category") };
    /// <summary>Gets implemented selector collections with typed value rows.</summary>
    public static IEnumerable<(string id, string name)> SelectorRows => new (string, string)[] { ("", "Use static options below"), ("settings.navigationKeys", "Available menu navigation keys"), ("settings.shortcutKeys", "Available reading shortcut keys"), ("settings.holdSkipChoices", "Available hold-to-skip modifiers"), ("languages", "Available game languages"), ("themes", "Game theme"), ("checkpoints", "Retained named checkpoints") };
    /// <summary>Gets typed image fields without storing textures in narrative values.</summary>
    public static IEnumerable<(string id, string name)> Images => new (string, string)[]
    { ("", "Unbound · Use image asset"), ("dialogue.portrait", "Reading · Current character portrait"), ("item.thumbnail", "Save item · Screenshot at the same boundary"), ("item.image", "Collection item · Unlocked image"), ("collection.image", "Collection details · Image"), ("music.image", "Music · Current track cover") };
    /// <summary>Gets row arguments used by implemented actions.</summary>
    public static IEnumerable<(string id, string name)> Arguments => new (string, string)[]
    { ("", "Unbound · Use fixed argument"), ("item.id", "List item · Stable ID"), ("item.index", "History item · Index"), ("item.key", "Save item · Slot ID") };
    /// <summary>Gets implemented system page roles.</summary>
    public static IEnumerable<(string id, string name)> Pages => new (string, string)[]
    { ("title", "Title"), ("reading", "Reading"), ("pause", "Pause"), ("settings", "Settings"), ("history", "History"), ("credits", "Credits"), ("save", "Save"), ("load", "Load"), ("confirm", "Confirm"),
        ("extras", "Gallery categories"), ("gallery", "CG Gallery"), ("music", "Music Gallery"), ("replays", "Replays"), ("endings", "Endings"), ("glossary", "Glossary"), ("characters", "Character profiles"), ("collectionDetail", "Collection details") };
    /// <summary>Gets actions whose dispatch paths are implemented by the production game view.</summary>
    public static IEnumerable<(string id, string name)> Actions => new (string, string)[]
    {
        ("keyboard.reset", "Settings · Reset reading shortcuts"),
        ("image.zoomIn", "Image viewport · Zoom in"), ("image.zoomOut", "Image viewport · Zoom out"), ("image.reset", "Image viewport · Fit"),
        ("collection.image.previous", "CG · Previous unlocked variant"), ("collection.image.next", "CG · Next unlocked variant"),
        ("page.reset", "Page state · Reset fields"), ("page.toggle", "Page state · Toggle Boolean field"),
        ("profile.retry", "Player data · Retry load or save"), ("profile.defaults", "Player data · Request defaults"),
        ("profile.defaults.confirm", "Player data · Confirm defaults"), ("profile.defaults.cancel", "Player data · Cancel defaults"),
        ("command.cancel", "Extension · Cancel current command"), ("checkpoint.rollback", "Rollback · Restore named checkpoint"),
        ("", "No action"), ("newGame", "Game · New game"), ("advance", "Reading · Advance"), ("choice", "Reading · Select choice item"), ("input.submit", "Input · Confirm"), ("input.cancel", "Input · Cancel"),
        ("theme", "Interface · Switch theme"), ("language", "Settings · Switch language item"), ("open", "Interface · Open screen"), ("resume", "Interface · Resume reading"), ("back", "Interface · Back"), ("title", "Interface · Request return to title"),
        ("automatic", "Reading · Toggle auto"), ("skip", "Reading · Toggle skip"), ("hide", "Reading · Hide UI"), ("rollback", "Reading · Roll back"), ("history.voice", "History · Replay entry voice"), ("history.rollback", "History · Restore entry position"), ("history.previous", "History · Previous page"), ("history.next", "History · Next page"),
        ("save.write", "Save item · Save"), ("save.load", "Save item · Request load"), ("save.delete", "Save item · Request deletion"), ("slots.previous", "Saves · Previous page"), ("slots.next", "Saves · Next page"),
        ("slots.manual", "Saves · Manual category"), ("slots.automatic", "Saves · Automatic category"), ("slots.quick", "Saves · Quick category"), ("continue", "Game · Continue latest save"), ("quickSave", "Saves · Quick save"), ("quickLoad", "Saves · Request quick load"),
        ("confirm.accept", "Confirmation · Accept original action"), ("confirm.cancel", "Confirmation · Cancel original action"), ("collection.select", "Collection item · View / play / replay"),
        ("collections.previous", "Collection · Previous page"), ("collections.next", "Collection · Next page"), ("music.previous", "Music · Previous track"), ("music.next", "Music · Next track"), ("music.pause", "Music · Pause / resume"), ("replay.exit", "Replay · Exit")
    };
}
