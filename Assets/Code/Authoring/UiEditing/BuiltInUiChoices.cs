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
        .Select(static field => ("page." + field.id, "页面 · " + field.name + (field.scope == VariableScope.Game ? " · 随游戏保存" : " · 本次页面") + " · " + field.id));
    /// <summary>Gets observable numeric fields usable by layout bindings, including positioned dialogue.</summary>
    /// <param name="variables">The game's declared numeric variables.</param>
    /// <returns>Numeric settings, variables and read-only reading geometry.</returns>
    public static IEnumerable<(string id, string name)> Geometry(VariableDefinition[] variables) => NumericValues(variables).Concat(new (string, string)[]
    { ("dialogue.positionX", "阅读 · 横向位置比例"), ("dialogue.positionY", "阅读 · 纵向位置比例"), ("dialogue.widthRatio", "阅读 · 面板宽度比例") });
    /// <summary>Gets writable Boolean fields for toggle controls.</summary>
    /// <param name="variables">Detached game declarations.</param>
    /// <returns>Writable Boolean bindings, excluding observation-only conditions.</returns>
    public static IEnumerable<(string id, string name)> WritableBooleans(VariableDefinition[] variables) => Conditions(variables).Where(static option => option.id.Length == 0
        || option.id.StartsWith("variables.", System.StringComparison.Ordinal) || option.id is "item.voiceEnabled" or "settings.allowFlashing" or "settings.reducedMotion" or "settings.highContrast" or "settings.skipReadOnly" or "settings.stopVoiceOnAdvance" or "music.repeat" or "music.shuffle");
    /// <summary>Gets writable plain strings for text inputs.</summary>
    /// <param name="variables">Detached game declarations.</param>
    /// <returns>String inputs; enums use typed selectors.</returns>
    public static IEnumerable<(string id, string name)> WritableText(VariableDefinition[] variables) => new (string, string)[] { ("", "未绑定"), ("input.value", "输入 · 玩家文字"), ("collections.search", "收藏 · 搜索文字") }
        .Concat(variables.Where(static variable => variable.initialValue.kind == NovelValueKind.String).Select(static variable => ("variables." + variable.id, "变量 · " + variable.name + " · " + variable.id)));
    /// <summary>Gets read-only and writable String fields for plain tooltip content.</summary>
    /// <param name="variables">The selected game's typed declarations.</param>
    /// <returns>String paths without Boolean, enum, numeric or list coercion.</returns>
    public static IEnumerable<(string id, string name)> TextValues(VariableDefinition[] variables) => Values([]).Where(option =>
        !Conditions([]).Any(flag => flag.id.Length > 0 && flag.id == option.id)
        && !NumericValues([]).Any(number => number.id.Length > 0 && number.id == option.id)
        && option.id is not ("history.page" or "history.count" or "saves.page" or "collections.page" or "collection.imagePage" or "collection.imageCount" or "item.shortcutKey" or "settings.holdSkipModifiers"))
        .Concat(variables.Where(static variable => variable.initialValue.kind == NovelValueKind.String).Select(static variable => ("variables." + variable.id, "变量 · " + variable.name + " · " + variable.id)));
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
        .Concat(variables.Where(static variable => variable.initialValue.kind is NovelValueKind.Number or NovelValueKind.Integer).Select(static variable => ("variables." + variable.id, "变量 · " + variable.name + " · " + variable.id)));
    /// <summary>Gets writable numbers and observation-only numeric fields for progress and layout controls.</summary>
    /// <param name="variables">Detached game declarations.</param>
    /// <returns>Numeric fields that never make timed decisions writable from game UI.</returns>
    public static IEnumerable<(string id, string name)> NumericValues(VariableDefinition[] variables) => Numbers(variables).Concat(new (string, string)[]
    { ("choice.timeRemaining", "选项 · 剩余时间"), ("choice.timeRemainingRatio", "选项 · 剩余时间比例"), ("choice.remainingSeconds", "选项 · 剩余整秒数") });
    /// <summary>Gets literal-free scalar fields, including the selected game's declared variables.</summary>
    /// <param name="variables">Detached shared game declarations.</param>
    /// <returns>Stable field paths and author-facing labels.</returns>
    public static IEnumerable<(string id, string name)> Values(VariableDefinition[] variables) => new (string, string)[]
    {
        ("settings.holdSkipModifiers", "设置 · 按住快进组合"),
        ("choice.timeRemaining", "选项 · 剩余时间"), ("choice.timeRemainingRatio", "选项 · 剩余时间比例"), ("choice.remainingSeconds", "选项 · 剩余整秒数"),
        ("", "无绑定 · 使用正文"), ("game.title", "游戏 · 标题"), ("dialogue.characterId", "阅读 · 当前角色身份"), ("dialogue.speaker", "阅读 · 当前发言人"), ("dialogue.text", "阅读 · 当前已显示台词"), ("dialogue.pageText", "阅读 · 当前 NVL 页面"), ("choice.prompt", "选项 · 提示"),
        ("input.prompt", "输入 · 提示"), ("input.value", "输入 · 玩家文字"), ("game.theme", "界面 · 当前主题"), ("history.barrierReason", "回滚 · 当前屏障原因"), ("settings.language", "设置 · 语言"), ("settings.textSpeed", "设置 · 文字速度"), ("settings.autoDelay", "设置 · 自动等待"),
        ("settings.fontScale", "设置 · 字体缩放"), ("settings.masterVolume", "音量 · 总音量"), ("settings.bgmVolume", "音量 · 背景音乐"), ("settings.ambienceVolume", "音量 · 环境音"),
        ("settings.sfxVolume", "音量 · 音效"), ("settings.voiceVolume", "音量 · 语音"), ("settings.uiVolume", "音量 · 界面"), ("game.error", "诊断 · 游戏错误"), ("interface.error", "诊断 · 自定义 UI 动作错误"), ("interface.dataError", "诊断 · 自定义 UI 数据错误"), ("confirmation.message", "确认 · 提示文字"),
        ("history.page", "历史 · 当前页"), ("history.count", "历史 · 总条数"), ("profile.error", "玩家数据 · 读取或保存错误"), ("saves.message", "存档 · 结果/错误"), ("saves.page", "存档 · 当前页"), ("collections.search", "收藏 · 搜索文字"), ("collections.page", "收藏 · 当前页"),
        ("collection.title", "收藏详情 · 名称"), ("collection.description", "收藏详情 · 正文"), ("collection.author", "收藏详情 · 作者"), ("collection.variantName", "CG · 当前变体名称"), ("collection.imagePage", "CG · 当前图片页"), ("collection.imageCount", "CG · 可浏览图片数"), ("music.title", "音乐 · 正在播放的名称"),
        ("item.voiceVolume", "角色语音项 · 音量"), ("item.shortcutKey", "快捷键设置项 · 按键枚举"), ("item.text", "列表项 · 文字"), ("item.title", "列表项 · 名称"), ("item.name", "列表项 · 显示名"), ("item.label", "存档项 · 槽位摘要"), ("item.description", "收藏项 · 说明"), ("item.reason", "选项 · 禁用原因")
    }.Concat(Conditions(variables).Where(static option => option.id.Length > 0)).Concat(variables.Where(static variable => variable.initialValue.kind != NovelValueKind.Boolean)
        .Select(static variable => ("variables." + variable.id, "变量 · " + variable.name + " · " + variable.id)));
    /// <summary>Gets Boolean fields suitable for visibility, toggles and interactivity.</summary>
    /// <param name="variables">Detached shared game declarations.</param>
    /// <returns>Stable Boolean paths and labels.</returns>
    public static IEnumerable<(string id, string name)> Conditions(VariableDefinition[] variables) => new (string, string)[]
    {
        ("settings.highContrast", "设置 · 高对比阅读"),
        ("choice.timed", "选项 · 正在限时选择"),
        ("reading.advanceVisible", "阅读 · 可推进区域"), ("reading.waitingForAdvance", "阅读 · 等待玩家推进"),
        ("", "无绑定 · 始终允许"), ("dialogue.hasPortrait", "阅读 · 当前角色有头像"), ("dialogue.visible", "阅读 · 显示对白"), ("choice.visible", "阅读 · 显示选项"), ("input.visible", "输入 · 正在输入"), ("input.valid", "输入 · 内容有效"),
        ("dialogue.adv", "阅读 · ADV 布局"), ("dialogue.nvl", "阅读 · NVL 布局"), ("dialogue.bubble", "阅读 · 气泡布局"), ("dialogue.overlay", "阅读 · 浮字布局"), ("dialogue.letter", "阅读 · 书信布局"),
        ("command.active", "指令 · 正在等待"), ("command.cancelable", "指令 · 允许取消"),
        ("input.canCancel", "输入 · 允许取消"), ("history.hasBarrier", "回滚 · 存在不可逆屏障"), ("reading.toolbarVisible", "阅读 · 显示工具栏"), ("game.active", "游戏 · 已开始"), ("game.completed", "游戏 · 已结束"),
        ("game.faulted", "游戏 · 出错"), ("game.replay", "游戏 · 正在回想"), ("game.normal", "游戏 · 普通会话"), ("stage.empty", "舞台 · 没有内容"), ("history.canRollback", "历史 · 可以回退"), ("history.previous", "历史 · 有上一页"), ("history.next", "历史 · 有下一页"), ("history.empty", "历史 · 没有条目"), ("history.followLatest", "历史 · 跟随最新页面末尾"), ("item.canRollback", "历史项 · 保留了可回滚位置"),
        ("profile.ready", "玩家数据 · 已就绪"), ("profile.loading", "玩家数据 · 正在读取"), ("profile.failed", "玩家数据 · 读取或保存失败"), ("profile.saving", "玩家数据 · 正在保存"), ("profile.canUseDefaults", "玩家数据 · 可以请求默认值"), ("profile.confirmDefaults", "玩家数据 · 确认默认值"), ("settings.allowFlashing", "设置 · 允许闪烁"), ("settings.reducedMotion", "设置 · 减少动态效果"), ("settings.skipReadOnly", "设置 · 仅跳过已读"), ("settings.stopVoiceOnAdvance", "设置 · 推进时停语音"),
        ("saves.ready", "存档 · 服务可用"), ("saves.canSave", "存档 · 可以保存"), ("saves.canContinue", "存档 · 有本构建可继续存档"), ("saves.failed", "存档 · 操作失败"), ("saves.previous", "存档 · 有上一页"), ("saves.next", "存档 · 有下一页"),
        ("saves.available", "存档 · 已接入宿主"), ("saves.busy", "存档 · 正在处理"),
        ("collections.empty", "收藏 · 搜索结果为空"), ("collections.previous", "收藏 · 有上一页"), ("collections.next", "收藏 · 有下一页"),
        ("collection.hasImage", "CG · 有可浏览图片"), ("collection.imagePrevious", "CG · 有上一张已解锁变体"), ("collection.imageNext", "CG · 有下一张已解锁变体"), ("music.available", "音乐 · 可以播放"),
        ("music.paused", "音乐 · 已暂停"), ("music.repeat", "音乐 · 单曲循环"), ("music.shuffle", "音乐 · 随机顺序"),
        ("item.voiceEnabled", "角色语音项 · 启用"), ("item.enabled", "选项 · 可以选择"), ("item.hasReason", "选项 · 有禁用说明"), ("item.voiced", "历史项 · 有语音"), ("item.selected", "列表项 · 已选 / 当前"), ("item.unlocked", "收藏项 · 已解锁"),
        ("item.canSave", "存档项 · 可以保存"), ("item.canLoad", "存档项 · 可以读取"), ("item.canDelete", "存档项 · 可以删除")
    }.Concat(variables.Where(static variable => variable.initialValue.kind == NovelValueKind.Boolean).Select(static variable => ("variables." + variable.id, "变量 · " + variable.name + " · " + variable.id)));
    /// <summary>Gets implemented repeated collections.</summary>
    public static IEnumerable<(string id, string name)> Rows => new (string, string)[]
    { ("", "不重复"), ("settings.navigationShortcuts", "菜单导航键设置"), ("settings.shortcuts", "阅读快捷键设置"), ("characters.voices", "角色语音设置"), ("choices", "当前剧情选项"), ("history", "阅读历史"), ("checkpoints", "保留的命名检查点"), ("languages", "可选游戏语言"), ("themes", "游戏主题"), ("saves.slots", "当前分类存档槽"), ("collections.items", "当前分类收藏条目") };
    /// <summary>Gets implemented selector collections with typed value rows.</summary>
    public static IEnumerable<(string id, string name)> SelectorRows => new (string, string)[] { ("", "使用下方静态选项"), ("settings.navigationKeys", "可选菜单导航键"), ("settings.shortcutKeys", "可选阅读快捷键"), ("settings.holdSkipChoices", "可选按住快进组合"), ("languages", "可选游戏语言"), ("themes", "游戏主题"), ("checkpoints", "保留的命名检查点") };
    /// <summary>Gets typed image fields without storing textures in narrative values.</summary>
    public static IEnumerable<(string id, string name)> Images => new (string, string)[]
    { ("", "无绑定 · 使用图像资源"), ("dialogue.portrait", "阅读 · 当前角色头像"), ("item.thumbnail", "存档项 · 同一边界的截图"), ("item.image", "收藏项 · 已解锁图片"), ("collection.image", "收藏详情 · 图片"), ("music.image", "音乐 · 当前曲目封面") };
    /// <summary>Gets row arguments used by implemented actions.</summary>
    public static IEnumerable<(string id, string name)> Arguments => new (string, string)[]
    { ("", "无绑定 · 使用固定参数"), ("item.id", "列表项 · 稳定身份"), ("item.index", "历史项 · 索引"), ("item.key", "存档项 · 槽位身份") };
    /// <summary>Gets implemented system page roles.</summary>
    public static IEnumerable<(string id, string name)> Pages => new (string, string)[]
    { ("title", "标题"), ("reading", "阅读"), ("pause", "暂停"), ("settings", "设置"), ("history", "历史"), ("credits", "制作人员"), ("save", "保存"), ("load", "读取"), ("confirm", "确认"),
        ("extras", "鉴赏分类"), ("gallery", "CG 鉴赏"), ("music", "音乐鉴赏"), ("replays", "回想"), ("endings", "结局"), ("glossary", "词条"), ("characters", "角色资料"), ("collectionDetail", "收藏详情") };
    /// <summary>Gets actions whose dispatch paths are implemented by the production game view.</summary>
    public static IEnumerable<(string id, string name)> Actions => new (string, string)[]
    {
        ("keyboard.reset", "设置 · 恢复阅读快捷键默认值"),
        ("image.zoomIn", "图片视口 · 放大"), ("image.zoomOut", "图片视口 · 缩小"), ("image.reset", "图片视口 · 适应"),
        ("collection.image.previous", "CG · 上一张已解锁变体"), ("collection.image.next", "CG · 下一张已解锁变体"),
        ("page.reset", "页面状态 · 恢复字段默认值"), ("page.toggle", "页面状态 · 切换 Boolean 字段"),
        ("profile.retry", "玩家数据 · 重试读取或保存"), ("profile.defaults", "玩家数据 · 请求使用默认值"),
        ("profile.defaults.confirm", "玩家数据 · 确认默认值"), ("profile.defaults.cancel", "玩家数据 · 取消默认值"),
        ("command.cancel", "扩展 · 取消当前指令"), ("checkpoint.rollback", "回滚 · 回到命名检查点"),
        ("", "无动作"), ("newGame", "游戏 · 新游戏"), ("advance", "阅读 · 推进"), ("choice", "阅读 · 选择列表项"), ("input.submit", "输入 · 确认"), ("input.cancel", "输入 · 取消"),
        ("theme", "界面 · 切换主题"), ("language", "设置 · 切换语言项"), ("open", "界面 · 打开页面"), ("resume", "界面 · 返回阅读"), ("back", "界面 · 返回上一页"), ("title", "界面 · 返回标题并确认"),
        ("automatic", "阅读 · 自动开关"), ("skip", "阅读 · 快进开关"), ("hide", "阅读 · 隐藏界面"), ("rollback", "阅读 · 回滚"), ("history.voice", "历史 · 重播条目语音"), ("history.rollback", "历史 · 回到条目位置"), ("history.previous", "历史 · 上一页"), ("history.next", "历史 · 下一页"),
        ("save.write", "存档项 · 保存"), ("save.load", "存档项 · 读取并确认"), ("save.delete", "存档项 · 删除并确认"), ("slots.previous", "存档 · 上一页"), ("slots.next", "存档 · 下一页"),
        ("slots.manual", "存档 · 手动分类"), ("slots.automatic", "存档 · 自动分类"), ("slots.quick", "存档 · 快速分类"), ("continue", "游戏 · 继续最近存档"), ("quickSave", "存档 · 快速保存"), ("quickLoad", "存档 · 快速读取并确认"),
        ("confirm.accept", "确认框 · 确认原动作"), ("confirm.cancel", "确认框 · 取消原动作"), ("collection.select", "收藏项 · 查看/播放/回想"),
        ("collections.previous", "收藏 · 上一页"), ("collections.next", "收藏 · 下一页"), ("music.previous", "音乐 · 上一首"), ("music.next", "音乐 · 下一首"), ("music.pause", "音乐 · 暂停/继续"), ("replay.exit", "回想 · 退出")
    };
}
