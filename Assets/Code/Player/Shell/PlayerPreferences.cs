using InnoEngine.Serialization;
using System;
using System.Linq;
using Inno.VisualNovel.Model.Audio;
using System.Collections.Generic;
using InnoEngine.Input;
using Inno.VisualNovel.Player.Shell.Keyboard;

namespace Inno.VisualNovel.Player.Shell;

/// <summary>Stores player-owned reading preferences separately from story state.</summary>
[GenerateSerializationConverter]
public sealed class PlayerPreferences : ISerializable
{
    /// <summary>Gets or sets the preferred game language; empty selects the game's default.</summary>
    [SerializableProperty] public string language { get; set; } = string.Empty;
    private double m_textSpeed = 1;
    private double m_autoDelay = 1.5;
    private double m_fontScale = 1;
    private double m_master = 0.8;
    private double[] m_channels = [1, 1, 1, 1, 1];
    private CharacterVoicePreference[] m_characterVoices = [];
    private NovelKeyboardBinding[] m_keyboardShortcuts = NovelKeyboardShortcuts.CreateDefaults();
    private NovelKeyboardBinding[] m_navigationShortcuts = NovelNavigationShortcuts.CreateDefaults();
    private KeyModifier m_holdSkipModifiers = KeyModifier.Control;
    /// <summary>Gets or sets the exact modifier hold used for skipping; None explicitly disables hold-to-skip.</summary>
    [SerializableProperty] public KeyModifier holdSkipModifiers
    {
        get => m_holdSkipModifiers;
        set { if (!NovelKeyboardShortcuts.modifiers.Contains(value)) throw new ArgumentException("Select a supported skip-hold modifier combination.", nameof(value)); m_holdSkipModifiers = value; }
    }
    /// <summary>Gets or sets independent validated reading shortcuts; essential UI navigation is never rebound by this table.</summary>
    [SerializableProperty] public NovelKeyboardBinding[] keyboardShortcuts
    {
        get => m_keyboardShortcuts.Select(static item => item.Copy()).ToArray();
        set { NovelKeyboardShortcuts.Validate(value); m_keyboardShortcuts = value.Select(static item => item.Copy()).ToArray(); }
    }
    /// <summary>Gets or sets a detached navigation table; complete cross-table validation occurs on candidate copying or an explicit mapping edit.</summary>
    [SerializableProperty] public NovelKeyboardBinding[] navigationShortcuts
    {
        get => m_navigationShortcuts.Select(static item => item.Copy()).ToArray();
        set { NovelNavigationShortcuts.Validate(value); m_navigationShortcuts = value.Select(static item => item.Copy()).ToArray(); }
    }
    /// <summary>Changes one mapping atomically, rejecting occupied or navigation-reserved keys.</summary>
    /// <param name="action">The supported stable reading action.</param>
    /// <param name="key">A supported letter/function key, or Unknown to disable the action.</param>
    /// <param name="modifiers">The exact supported modifier combination, defaulting to an unmodified key.</param>
    public void SetKeyboardShortcut(
        string action,
        KeyCode key,
        KeyModifier modifiers = KeyModifier.None
    ) {
        var candidate = keyboardShortcuts;
        var mapping = candidate.SingleOrDefault(item => item.action == action) ?? throw new ArgumentException("Unsupported reading shortcut: " + action, nameof(action));
        mapping.key = key; mapping.modifiers = modifiers;
        NovelKeyboardBindings.Distinct(candidate, m_navigationShortcuts); keyboardShortcuts = candidate;
    }
    /// <summary>Changes one menu mapping atomically, rejecting conflicts with both menu and reading actions.</summary>
    /// <param name="action">The supported navigation action.</param>
    /// <param name="key">A supported key, or Unknown to disable the action.</param>
    /// <param name="modifiers">Exact supported modifiers.</param>
    public void SetNavigationShortcut(
        string action,
        KeyCode key,
        KeyModifier modifiers = KeyModifier.None
    ) {
        var candidate = navigationShortcuts;
        var mapping = candidate.SingleOrDefault(item => item.action == action) ?? throw new ArgumentException("Unsupported navigation shortcut: " + action, nameof(action));
        mapping.key = key; mapping.modifiers = modifiers;
        NovelKeyboardBindings.Distinct(m_keyboardShortcuts, candidate); navigationShortcuts = candidate;
    }
    /// <summary>Resolves at most one pressed menu action independently of physical text-editing keys.</summary>
    /// <param name="pressed">Viewport-local key presses.</param>
    /// <param name="modifiers">Exact current modifiers.</param>
    /// <returns>One supported action in stable priority, or empty.</returns>
    public string ResolveNavigationShortcut(
        IReadOnlyCollection<KeyCode> pressed,
        KeyModifier modifiers = KeyModifier.None
    ) => NovelNavigationShortcuts.Resolve(pressed, m_navigationShortcuts, modifiers);
    /// <summary>Resolves one pressed reading shortcut without exposing mutable preference storage.</summary>
    /// <param name="pressed">Current viewport-local key presses.</param>
    /// <param name="modifiers">Current viewport-local modifier state; extra modifiers do not match.</param>
    /// <returns>A supported action in stable priority, or empty.</returns>
    public string ResolveKeyboardShortcut(
        IReadOnlyCollection<KeyCode> pressed,
        KeyModifier modifiers = KeyModifier.None
    ) => NovelKeyboardShortcuts.Resolve(pressed, m_keyboardShortcuts, modifiers);
    /// <summary>Restores the complete default mapping as one preference operation.</summary>
    public void ResetKeyboardShortcuts() { m_keyboardShortcuts = NovelKeyboardShortcuts.CreateDefaults(); m_navigationShortcuts = NovelNavigationShortcuts.CreateDefaults(); m_holdSkipModifiers = KeyModifier.Control; }
    /// <summary>Gets or sets detached character voice policies independently of story rollback.</summary>
    [SerializableProperty] public CharacterVoicePreference[] characterVoices
    {
        get => m_characterVoices.Select(CopyVoice).ToArray();
        set
        {
            if (value.Length > 1024 || value.Any(static item => string.IsNullOrWhiteSpace(item.characterId) || item.characterId.Length > 128 || !double.IsFinite(item.volume) || item.volume is < 0 or > 1)
                || value.Select(static item => item.characterId).Distinct(StringComparer.Ordinal).Count() != value.Length)
                throw new ArgumentException("Character voice preferences require up to 1024 unique identities and finite zero-to-one gains.");
            m_characterVoices = value.Select(CopyVoice).ToArray();
        }
    }
    /// <summary>Reads a detached character policy, defaulting to audible full volume.</summary>
    /// <param name="characterId">The stable character resource identity.</param>
    /// <returns>A detached policy safe to edit before applying.</returns>
    public CharacterVoicePreference GetCharacterVoice(string characterId) => m_characterVoices.FirstOrDefault(item => item.characterId == characterId) is { } policy
        ? CopyVoice(policy) : new() { characterId = characterId };
    /// <summary>Applies one validated character voice policy without exposing mutable settings storage.</summary>
    /// <param name="policy">The complete proposed character policy.</param>
    public void SetCharacterVoice(CharacterVoicePreference policy) => characterVoices = [.. m_characterVoices.Where(item => item.characterId != policy.characterId), policy];
    private static CharacterVoicePreference CopyVoice(CharacterVoicePreference source) => new() { characterId = source.characterId, volume = source.volume, enabled = source.enabled };
    /// <summary>Gets or sets reveal speed multiplier.</summary>
    [SerializableProperty] public double textSpeed { get => m_textSpeed; set => m_textSpeed = Bounded(value, 0.1, 5, nameof(textSpeed)); }
    /// <summary>Gets or sets auto-mode base delay.</summary>
    [SerializableProperty] public double autoDelay { get => m_autoDelay; set => m_autoDelay = Bounded(value, 0.1, 10, nameof(autoDelay)); }
    /// <summary>Gets or sets whether skipping stops at unread content.</summary>
    [SerializableProperty] public bool skipReadOnly { get; set; } = true;
    /// <summary>Gets or sets text scale.</summary>
    [SerializableProperty] public double fontScale { get => m_fontScale; set => m_fontScale = Bounded(value, 0.5, 4, nameof(fontScale)); }
    /// <summary>Gets or sets reduced motion.</summary>
    [SerializableProperty] public bool reducedMotion { get; set; }
    /// <summary>Gets or sets an independent accessible paint palette without replacing the author's theme.</summary>
    [SerializableProperty] public bool highContrast { get; set; }
    /// <summary>Gets or sets whether authored full-frame flashes are permitted.</summary>
    [SerializableProperty] public bool allowFlashing { get; set; } = true;
    /// <summary>Gets or sets whether advancing a completed line stops its voice immediately.</summary>
    [SerializableProperty] public bool stopVoiceOnAdvance { get; set; } = true;
    /// <summary>Gets or sets master audio gain.</summary>
    [SerializableProperty] public double masterVolume { get => m_master; set => m_master = Bounded(value, 0, 1, nameof(masterVolume)); }
    /// <summary>Gets or sets gains in Bgm, Ambience, Sfx, Voice and Ui order.</summary>
    [SerializableProperty] public double[] channelVolumes
    {
        get => m_channels.ToArray();
        set
        {
            if (value.Length != 5 || value.Any(static gain => !double.IsFinite(gain) || gain is < 0 or > 1)) throw new ArgumentException("Audio preferences require exactly five finite gains between zero and one.");
            m_channels = value.ToArray();
        }
    }
    /// <summary>Creates a validated detached copy for persistence and candidate application.</summary>
    /// <returns>Independent player preferences without shared gain storage.</returns>
    public PlayerPreferences Copy()
    {
        NovelKeyboardBindings.Distinct(m_keyboardShortcuts, m_navigationShortcuts);
        return new() { language = language, textSpeed = textSpeed, autoDelay = autoDelay, fontScale = fontScale, skipReadOnly = skipReadOnly,
            reducedMotion = reducedMotion, highContrast = highContrast, allowFlashing = allowFlashing, stopVoiceOnAdvance = stopVoiceOnAdvance, masterVolume = masterVolume, channelVolumes = channelVolumes,
            characterVoices = characterVoices, keyboardShortcuts = keyboardShortcuts, navigationShortcuts = navigationShortcuts, holdSkipModifiers = holdSkipModifiers };
    }
    /// <summary>Reads one independent category gain.</summary>
    /// <param name="channel">The semantic audio category.</param>
    /// <returns>A validated linear gain.</returns>
    public double GetVolume(NovelAudioChannel channel) => Enum.IsDefined(channel) ? m_channels[(int)channel] : throw new ArgumentOutOfRangeException(nameof(channel));
    /// <summary>Changes a category gain without exposing mutable settings arrays.</summary>
    /// <param name="channel">The semantic audio category.</param>
    /// <param name="gain">The finite zero-to-one gain.</param>
    public void SetVolume(
        NovelAudioChannel channel,
        double gain
    ) {
        if (!Enum.IsDefined(channel)) throw new ArgumentOutOfRangeException(nameof(channel));
        m_channels[(int)channel] = Bounded(gain, 0, 1, nameof(gain));
    }
    private static double Bounded(
        double value,
        double minimum,
        double maximum,
        string name
    ) => double.IsFinite(value) && value >= minimum && value <= maximum ? value : throw new ArgumentOutOfRangeException(name, "A finite preference within its declared range is required.");
}
