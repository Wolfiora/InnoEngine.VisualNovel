using InnoEditor.ImGui;
using InnoEditor.Settings;

namespace Inno.VisualNovel.Editor.Localization;

/// <summary>Edits the VN authoring language through native staged Settings and shared History.</summary>
[EditorSettingPath(settingPath)]
public sealed class NovelEditorLanguageSetting : EditorSetting
{
    /// <summary>Identifies this native field independently of translated captions.</summary>
    public const string settingPath = "Editor/Visual Novel/Language";
    /// <inheritdoc />
    public override string description => "Editor language only. Apply commits this preference; native Undo and Redo restore it.";
    /// <inheritdoc />
    public override EditorSettingObject defaultValue
    {
        get { var value = new EditorSettingObject(); value.SetAsString("language", "zh-CN"); return value; }
    }
    /// <inheritdoc />
    protected override void OnDraw(EditorSettingObject setting)
    {
        string current = setting.GetAsString("language", "zh-CN") ?? "zh-CN";
        if (!EditorUiSession.current.widgets.BeginBoundedCombo("Language###inno-vn-editor-language", current == "en" ? "English" : "简体中文")) return;
        try
        {
            if (ImGui.Selectable("简体中文###inno-vn-editor-zh", current == "zh-CN")) setting.SetAsString("language", "zh-CN");
            if (ImGui.Selectable("English###inno-vn-editor-en", current == "en")) setting.SetAsString("language", "en");
        }
        finally { EditorUiSession.current.widgets.EndBoundedCombo(); }
    }
}
