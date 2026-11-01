using InnoEditor.Settings;

namespace Inno.VisualNovel.Editor.Localization;

/// <summary>Places VN preferences in the native project Settings tree.</summary>
[EditorSettingPath("Editor/Visual Novel")]
public sealed class NovelEditorSettingsPage : EditorSetting
{
    /// <inheritdoc />
    public override string description => "Visual Novel editor preferences. Game language is configured separately.";
}
