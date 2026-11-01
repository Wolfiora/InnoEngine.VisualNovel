using System.Collections.Generic;
using Inno.VisualNovel.Authoring.LocalizationEditing;
using Inno.VisualNovel.Model.Localization;
using Inno.VisualNovel.Model.Project;

namespace Inno.VisualNovel.Authoring.Templates;

/// <summary>Creates editable English translations for known starter content without translating arbitrary authored text.</summary>
public static class StarterLocalizationTemplate
{
    /// <summary>Extracts current source identities and fills only the explicitly supplied starter translations.</summary>
    /// <param name="project">The template's unsaved game with compiled chapters and authored screens.</param>
    /// <returns>A detached native translation table ready to save as a project asset.</returns>
    public static LocalizationTableAsset Create(NovelProjectAsset project)
    {
        var table = new LocalizationTableAsset { title = "游戏翻译 · Game text", languages = [new() { id = "zh-CN", name = "简体中文" }, new() { id = "en", name = "English" }] };
        LocalizationEditService.Synchronize(table, LocalizationExtractionService.Extract(project));
        var english = English();
        foreach (var row in table.entries)
            if (english.TryGetValue(row.source, out string? translated)) LocalizationEditService.SetTranslation(table, row.id, "en", translated, TranslationStatus.Reviewed);
        return table;
    }
    private static IReadOnlyDictionary<string, string> English() => new Dictionary<string, string>
    {
        ["未设置"] = "Unassigned", ["快捷键组合已被占用或不能用于此操作。请先更换另一项，或选择未设置。"] = "This combination is occupied or unavailable for this action. Change the other action first, or choose Unassigned.",
        ["阅读快捷键"] = "Reading shortcuts", ["恢复默认快捷键"] = "Reset shortcuts", ["快速读档"] = "Quick load", ["回滚阅读"] = "Rollback reading",
        ["打开历史记录"] = "Open history", ["隐藏阅读界面"] = "Hide reading interface", ["切换快进"] = "Toggle skipping", ["切换自动播放"] = "Toggle automatic reading",
        ["按住快进组合"] = "Hold-to-skip modifiers",
        ["输入以筛选"] = "Type to filter", ["没有匹配项"] = "No matches", ["绑定拒绝此值"] = "The binding rejected this value",
        ["剩余秒数"] = "Seconds left",
        ["菜单导航键"] = "Menu navigation", ["确认操作"] = "Confirm action", ["下一项"] = "Next item", ["上一项"] = "Previous item",
        ["点击或按确认继续"] = "Click or confirm to continue",
        ["增加数值 / 向右"] = "Increase / Right", ["减少数值 / 向左"] = "Decrease / Left", ["向上翻页"] = "Page up", ["向下翻页"] = "Page down", ["返回 / 取消"] = "Back / Cancel",
        ["可重设阅读和菜单组合；相同组合不能分配多项。输入文字时不触发字母导航键。Tab、Shift+Tab 和文字编辑按键保持可用；按住快进可修改或关闭。"] = "Remap reading and menu chords; enabled combinations must be unique. Printable navigation keys remain text while typing. Tab, Shift+Tab and text-editing keys stay available. Remap or disable hold-to-skip here.",
        ["按阅读速度和语音结束时间自动推进；再次操作可关闭。"] = "Advance using reading speed and voice completion. Activate again to turn off.",
        ["点击切换快进；按住快进组合可在设置中调整。默认在未读台词、选项和脚本屏障前停止。"] = "Toggle skipping; configure hold-to-skip in Settings. By default, stop at unread text, choices and script barriers.",
        ["隐藏阅读界面；下一次推进操作恢复界面。"] = "Hide the reading interface. The next advance restores it.",
        ["恢复到保留的阅读检查点；已读和全局解锁不会撤销。"] = "Restore a retained reading checkpoint. Read history and global unlocks remain.",
        ["读取当前内容构建中最近的有效存档。"] = "Load the newest valid save for this content build.",
        ["删除此槽位前需要确认。"] = "Confirmation is required before deleting this slot.",
        ["上一张"] = "Previous image", ["下一张"] = "Next image", ["放大"] = "Zoom in", ["缩小"] = "Zoom out", ["适应"] = "Fit image",
        ["滚轮缩放 · 拖动查看 · Enter 复位"] = "Scroll to zoom · Drag to pan · Enter to fit",
        [""] = "", ["启用此角色语音"] = "Enable this character's voice", ["树"] = "Shu", ["我的视觉小说"] = "My Visual Novel", ["VISUAL NOVEL"] = "VISUAL NOVEL",
        ["正在读取玩家设置与全局进度…"] = "Loading preferences and progress...", ["重新读取"] = "Retry loading", ["使用默认值…"] = "Use defaults...",
        ["继续后将从默认进度开始，后续保存可能替换无法读取的数据。"] = "Continuing starts with default progress. Future saves may replace the unreadable data.",
        ["确认使用默认值"] = "Confirm defaults", ["返回重试"] = "Keep data and retry", ["重试保存玩家数据"] = "Retry saving player data",
        ["每一个选择，都将通向新的故事。"] = "Every choice opens a new story.", ["开始新故事"] = "New game", ["继续故事"] = "Continue", ["读取故事"] = "Load game",
        ["阅读设置"] = "Settings", ["制作人员"] = "Credits", ["菜单"] = "Menu", ["历史"] = "History", ["自动"] = "Auto", ["快进"] = "Skip", ["隐藏"] = "Hide", ["回退"] = "Rollback",
        ["本段故事已经结束。感谢阅读。"] = "This story has ended. Thank you for reading.", ["退出回想"] = "Exit replay", ["确认"] = "Confirm", ["取消"] = "Cancel",
        ["故事暂停"] = "Paused", ["阅读历史"] = "History", ["保存故事"] = "Save game", ["请确认"] = "Confirm action", ["继续阅读"] = "Resume", ["返回标题"] = "Return to title",
        ["手动存档"] = "Manual", ["自动存档"] = "Autosaves", ["快速存档"] = "Quick save", ["写入槽位"] = "Save", ["读取"] = "Load", ["删除…"] = "Delete...",
        ["上一页"] = "Previous", ["下一页"] = "Next", ["返回"] = "Back", ["重播语音"] = "Replay voice", ["回到这句"] = "Return to this line",
        ["界面主题"] = "Interface theme", ["请稍候…"] = "Please wait...", ["取消当前指令"] = "Cancel operation", ["游戏语言"] = "Language", ["文字速度"] = "Text speed", ["自动等待秒数"] = "Auto delay (seconds)", ["字体大小"] = "Text size", ["总音量"] = "Master volume", ["背景音乐"] = "Music",
        ["环境音"] = "Ambience", ["音效"] = "Sound effects", ["语音"] = "Voice", ["界面声音"] = "UI sounds", ["仅快进已读内容"] = "Skip read text only",
        ["减少动态效果"] = "Reduce motion", ["高对比阅读"] = "High contrast", ["允许闪烁效果"] = "Allow flashes", ["推进对白时停止语音"] = "Stop voice on advance",
        ["鉴赏与资料"] = "Extras", ["CG 鉴赏"] = "Art gallery", ["音乐鉴赏"] = "Music room", ["回想重播"] = "Replay scenes", ["结局记录"] = "Endings", ["词条"] = "Glossary", ["角色资料"] = "Characters",
        ["搜索已解锁内容"] = "Search unlocked items", ["这里还没有内容。在 VN 收藏中添加条目并关联到游戏。"] = "No items yet. Add entries in VN Collections and attach the library to your game.",
        ["上一首"] = "Previous track", ["暂停 / 继续"] = "Pause / Resume", ["下一首"] = "Next track", ["单曲循环"] = "Repeat track", ["随机顺序"] = "Shuffle", ["未解锁"] = "Locked",
        ["澄"] = "Cheng", ["你好，{player}。从这里开始写下你的故事。"] = "Hello, {player}. Your story starts here.", ["你想走向哪里？"] = "Where would you like to go?",
        ["去看海"] = "Visit the sea", ["沿林间小径前行"] = "Follow the forest path", ["海风送来了远方的消息。"] = "The sea breeze carries a message from far away.", ["树叶间的光点，像还没写完的句子。"] = "Light between the leaves feels like an unfinished sentence.",
        ["晨光"] = "Dawn", ["序章回想"] = "Opening scene", ["从明确的序章入口重新体验故事，不影响正在游玩的进度。"] = "Replay the opening from its explicit entry without changing your game progress.",
        ["视觉小说"] = "Visual novel", ["以文字、画面与声音共同讲述故事。你可以在 VN 收藏中编辑这个词条。"] = "A story told through text, images and sound. Edit this entry in VN Collections.",
        ["使用 InnoEngine.VisualNovel 制作\n\n在界面设计器中编辑这一页，写下你的创作团队。\n\n字体：Noto Sans CJK · SIL Open Font License 1.1"] = "Made with InnoEngine.VisualNovel\n\nEdit this page in the screen designer to credit your team.\n\nFont: Noto Sans CJK · SIL Open Font License 1.1"
    };
}
