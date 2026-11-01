using InnoEngine.Assets;
using InnoEngine.Audio;
using InnoEngine.Reflection;
using InnoEngine.Rendering;
using InnoEngine.Serialization;
using InnoEngine.Text;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Story;
using Inno.VisualNovel.Model.Stages;
using Inno.VisualNovel.Model.Characters;
using Inno.VisualNovel.Model.Audio;
using Inno.VisualNovel.Model.Performance;
using Inno.VisualNovel.Model.Localization;
using Inno.VisualNovel.Model.Collections;
using Inno.VisualNovel.Model.Interface.Themes;

namespace Inno.VisualNovel.Model.Project;

/// <summary>Maps one game's content and editable system pages without replacing the engine project format.</summary>
[StableTypeId("fd0c7316-14a5-4d89-8492-bfd0a0b9f120")]
public sealed class NovelProjectAsset : AssetObject
{
    /// <summary>Gets or sets the stable game identity.</summary>
    [SerializableProperty] public string gameId { get; set; } = "novel";
    /// <summary>Gets or sets the importer-computed exact content closure fingerprint.</summary>
    [SerializableProperty(PropertyVisibility.Hide)] public string buildFingerprint { get; set; } = string.Empty;
    /// <summary>Gets or sets the displayed game title.</summary>
    [SerializableProperty] public string title { get; set; } = "我的视觉小说";
    /// <summary>Gets or sets game translations, independent of editor language.</summary>
    [SerializableProperty] public LocalizationTableAsset? localization { get; set; }
    /// <summary>Gets or sets the game's unlockable art, music, replay and reference library.</summary>
    [SerializableProperty] public CollectionAsset? collections { get; set; }
    /// <summary>Gets or sets explicit extension schemas available to visual command authoring.</summary>
    [SerializableProperty] public Model.Extensions.NovelCommandDefinition[] commandDefinitions { get; set; } = [];
    /// <summary>Gets or sets neutral custom UI action schemas; implementations are supplied by a generation-owned host registry.</summary>
    [SerializableProperty] public Model.Interface.Actions.UiActionDefinition[] uiActions { get; set; } = [];
    /// <summary>Gets or sets the exact typed custom binding metadata consumed by this game.</summary>
    [SerializableProperty] public Model.Interface.Data.UiDataSourceDefinition[] uiDataSources { get; set; } = [];
    /// <summary>Gets or sets explicit native dependency roots used by dynamic extension references.</summary>
    [SerializableProperty] public AssetObject[] additionalResources { get; set; } = [];
    /// <summary>Gets or sets the initially selected game language.</summary>
    [SerializableProperty] public string defaultLanguage { get; set; } = "zh-CN";
    /// <summary>Gets or sets the game's chapters in authoring order.</summary>
    [SerializableProperty] public StoryAsset[] chapters { get; set; } = [];
    /// <summary>Gets or sets the explicit scenery resource library.</summary>
    [SerializableProperty] public StageAsset[] stages { get; set; } = [];
    /// <summary>Gets or sets the explicit layered-character library.</summary>
    [SerializableProperty] public CharacterAsset[] characters { get; set; } = [];
    /// <summary>Gets or sets the explicit audio cue library.</summary>
    [SerializableProperty] public AudioCueAsset[] audio { get; set; } = [];
    /// <summary>Gets or sets explicitly packaged reusable stage performances.</summary>
    [SerializableProperty] public PerformanceClipAsset[] performances { get; set; } = [];
    /// <summary>Gets or sets explicitly packaged deterministic particle presets.</summary>
    [SerializableProperty] public VisualEffectAsset[] particles { get; set; } = [];
    /// <summary>Gets or sets explicitly packaged screen-operation presets.</summary>
    [SerializableProperty] public VisualEffectAsset[] screenEffects { get; set; } = [];
    /// <summary>Gets or sets explicitly packaged image-transition presets.</summary>
    [SerializableProperty] public VisualEffectAsset[] transitions { get; set; } = [];
    /// <summary>Gets or sets the native premultiplied composition and post-processing material.</summary>
    [SerializableProperty] public MaterialAsset? effectsMaterial { get; set; }
    /// <summary>Gets or sets initial scenery, empty for an empty stage.</summary>
    [SerializableProperty] public string initialStage { get; set; } = string.Empty;
    /// <summary>Gets or sets the initial chapter identity.</summary>
    [SerializableProperty] public string startChapter { get; set; } = string.Empty;
    /// <summary>Gets or sets the public entry in the initial chapter.</summary>
    [SerializableProperty] public string startEntry { get; set; } = "start";
    /// <summary>Gets or sets editable screens selected by stable shell role.</summary>
    [SerializableProperty] public NovelScreenBinding[] screens { get; set; } = [];
    /// <summary>Gets or sets explicit editable runtime themes and their native page dependencies.</summary>
    [SerializableProperty] public ThemeAsset[] themes { get; set; } = [];
    /// <summary>Gets or sets the initially selected theme, empty for ordinary literal pages.</summary>
    [SerializableProperty] public string defaultTheme { get; set; } = string.Empty;
    /// <summary>Gets or sets the default imported font.</summary>
    [SerializableProperty] public FontAsset? font { get; set; }
    /// <summary>Gets or sets the native VN composition material.</summary>
    [SerializableProperty] public MaterialAsset? material { get; set; }
    /// <summary>Gets or sets optional title ambience.</summary>
    [SerializableProperty] public AudioClipAsset? titleAudio { get; set; }
    /// <summary>Gets or sets maximum retained interactive checkpoints.</summary>
    [SerializableProperty] public int rollbackLimit { get; set; } = 100;
    /// <summary>Gets or sets whether entering an interactive decision requests a rotating automatic save.</summary>
    [SerializableProperty] public bool autoSaveOnChoices { get; set; } = true;
    /// <summary>Gets or sets the independent manual save capacity used by the native component host.</summary>
    [SerializableProperty] public int manualSaveSlots { get; set; } = 100;
    /// <summary>Gets or sets the rotating automatic save capacity; the dedicated quick slot remains separate.</summary>
    [SerializableProperty] public int automaticSaveSlots { get; set; } = 3;
    /// <summary>Gets or sets the design canvas width.</summary>
    [SerializableProperty] public float width { get; set; } = 1920;
    /// <summary>Gets or sets the design canvas height.</summary>
    [SerializableProperty] public float height { get; set; } = 1080;
}
