using System;
using System.Linq;
using Inno.Core.Serialization;
using Inno.Rendering.Assets;
using Inno.Rendering;
using Inno.Text;
using Inno.VisualNovel.Authoring.LocalizationEditing;
using Inno.VisualNovel.Authoring.Templates;
using Inno.VisualNovel.Authoring.UiEditing;
using Inno.VisualNovel.Authoring.VoiceManagement;
using Inno.VisualNovel.Compiler;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Model.Localization;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Story.Nodes.Reading;
using Inno.VisualNovel.Player.Binding;
using Inno.VisualNovel.Player.Sessions;
using Inno.VisualNovel.Player.Shell;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class CharacterPresentationContracts
{
    internal static void NamesPortraitsAndRestore(
        StoryNodeDataCodec codec,
        StoryCompiler compiler,
        SerializationRegistry native
    ) {
        var draft = FirstStoryTemplate.Create(codec); var node = draft.graph.document.FindNode(new("hello"))!;
        var line = codec.Read<DialogueNode>(node); line.characterId = "guide"; line.speaker = ""; codec.Write(node, line);
        var next = codec.Read<DialogueNode>(draft.graph.document.FindNode(new("sea"))!); next.characterId = "guide"; next.speaker = "{player}"; codec.Write(draft.graph.document.FindNode(new("sea"))!, next);
        var chapter = draft.ToAsset(); chapter.program = compiler.Compile(chapter).program!;
        var portrait = new TextureAsset(320, 400, TextureColorSpace.Srgb, "png");
        var character = new CharacterAsset { resourceId = "guide", displayName = "澄", aliases = ["Cheng"], portrait = portrait, nameColor = 0xA9D5F0FF };
        var font = new FontAsset(); var game = new NovelProjectAsset { chapters = [chapter], startChapter = chapter.chapterId, characters = [character],
            screens = ShellThemeTemplates.roles.Select(role => new NovelScreenBinding { role = role, screen = ShellScreenTemplates.Create(role, font, "Clear") }).ToArray() };
        var texts = new LocalizationTableAsset { languages = [new(), new() { id = "en", name = "English" }] };
        LocalizationEditService.Synchronize(texts, LocalizationExtractionService.Extract(game));
        Check(texts.entries.Any(row => row.id == StoryTextKeys.CharacterName("guide")) && !texts.entries.Any(row => row.id == StoryTextKeys.Speaker(line.textId)), "Linked names were extracted per line instead of by stable character.");
        LocalizationEditService.SetTranslation(texts, StoryTextKeys.CharacterName("guide"), "en", "Cheng [guide]", TranslationStatus.Reviewed); game.localization = texts;
        using var session = new NovelSession(game); using var view = new NovelGameView(game, session, static (_, _) => (120, 40));
        character.displayName = "Changed source"; character.aliases[0] = "Changed alias"; character.portrait = null; session.StartNew();
        Check(session.story!.speaker == "澄" && session.currentCharacter?.aliases.Single() == "Cheng", "The running session retained editable character metadata.");
        var data = new NovelSessionDataSource(session); var rich = data.ReadRichText("dialogue.speaker", null)!;
        Check(rich.document.runs.Any(run => run.style.color == 0xA9D5F0FF) && ReferenceEquals(data.ReadImage("dialogue.portrait", null), portrait), "Speaker tint and portrait did not resolve through the shared game data source.");
        Check(view.BuildFrame().items.Any(item => item.id == "dialogue-portrait" && ReferenceEquals(item.image, portrait)), "The actual editable ADV page did not include its bound portrait.");
        var voiceRow = data.ReadRows("characters.voices").Single();
        Check(data.TryWrite("item.voiceVolume", Model.Values.NovelValue.Number(0.35), voiceRow) && data.TryWrite("item.voiceEnabled", Model.Values.NovelValue.Boolean(false), voiceRow)
            && session.preferences.GetCharacterVoice("guide") is { volume: 0.35, enabled: false }, "Visual character voice settings did not update their owner-local player preferences.");
        var saved = native.Deserialize<NovelSessionState>(native.Serialize(session.Capture()));
        session.Advance(); session.Advance(); session.Choose("sea"); Check(session.story.speaker == "旅人", "An explicit dynamic speaker override lost precedence over the character default.");
        session.Restore(saved); session.SetLanguage("en");
        Check(session.story!.speaker == "Cheng [guide]" && data.ReadRichText("dialogue.speaker", null)!.document.text == "Cheng [guide]", "Character-name localization was lost or interpreted literal brackets as text markup.");
        session.StartNew(); session.Advance(); session.Advance(); session.Choose("forest");
        Check(session.currentCharacter is null && !view.BuildFrame().items.Any(static item => item.id == "dialogue-portrait"), "Unlinked narration retained the previous speaker portrait.");
        line.characterId = "missing"; codec.Write(node, line); var source = draft.ToAsset(); source.program = compiler.Compile(source).program!;
        using var missing = new NovelSession(new() { chapters = [source], startChapter = source.chapterId }); missing.StartNew();
        Check(missing.story!.error.Contains("missing", StringComparison.Ordinal), "A missing linked speaker silently became narration.");
    }
    internal static void RecordingAndValidation(
        StoryNodeDataCodec codec,
        StoryCompiler compiler
    ) {
        var draft = FirstStoryTemplate.Create(codec); var node = draft.graph.document.FindNode(new("hello"))!;
        var line = codec.Read<DialogueNode>(node); line.characterId = "guide"; line.speaker = ""; codec.Write(node, line);
        var game = new NovelProjectAsset { characters = [new() { resourceId = "guide", displayName = "澄" }] };
        var service = new VoiceBatchService(codec); var review = service.Preview(draft, game, []);
        Check(review.rows.Single(row => row.node == "hello").speaker == "澄" && service.Export(draft, review).Contains("澄", StringComparison.Ordinal), "The recording workflow exported a blank linked name.");
        game.characters[0].displayName = "New name";
        Reject(() => VoiceBatchService.ValidateCharacters(game, review), "A recording review accepted changed character names.");
        line.characterId = new string('x', 129); codec.Write(node, line);
        Check(!compiler.Compile(draft.ToAsset()).success, "Unbounded linked character identities compiled.");
        Reject(() => CharacterDefinitionRules.Validate(new() { aliases = ["same", "SAME"] }), "Ambiguous aliases were accepted.");
        Check(BuiltInUiChoices.Images.Any(static pair => pair.id == "dialogue.portrait"), "The portrait field is not available to the visual UI designer.");
    }
    private static void Reject(
        Action operation,
        string message
    ) { try { operation(); } catch (InvalidOperationException) { return; } throw new InvalidOperationException(message); }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}
