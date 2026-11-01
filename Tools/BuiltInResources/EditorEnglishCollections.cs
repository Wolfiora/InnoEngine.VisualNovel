using System.Collections.Generic;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal static class EditorEnglishCollections
{
    internal static IReadOnlyList<EditorEnglishEntry> entries { get; } = new EditorEnglishEntry[]
    {
        new("literal", "VN 收藏 · Collections", "VN Collections"),
        new("literal", "收藏库", "Collection library"),
        new("literal", "使用所选游戏的变量与资源", "Use variables and resources from the selected game"),
        new("literal", "关联检查：", "Reference check: "),
        new("literal", "先选择游戏，再点击上方按钮。也可先编辑纯常量规则。", "Select a game, then use the button above. Constant-only rules can be edited without a game."),
        new("literal", "新条目标识", "New entry ID"),
        new("literal", "分类", "Category"),
        new("literal", "添加收藏条目", "Add collection entry"),
        new("literal", "条目标识稳定保存。剧情使用“解锁鉴赏内容”指令或下方条件解锁。", "Entry IDs remain stable. Unlock entries with the story's unlock instruction or the conditions below."),
        new("literal", "搜索名称 / 标识", "Search name / ID"),
        new("literal", "上一页##collection", "Previous page##collection"),
        new("literal", "下一页##collection", "Next page##collection"),
        new("literal", "稳定 ID：", "Stable ID: "),
        new("literal", "名称", "Name"),
        new("literal", "说明 / 正文", "Description / body"),
        new("literal", "作者 / 作曲", "Author / composer"),
        new("literal", "初始解锁", "Unlocked initially"),
        new("literal", "图片：", "Image: "),
        new("literal", "使用所选图片", "Use selected image"),
        new("literal", "移除图片", "Remove image"),
        new("literal", "音乐提示 ID", "Music cue ID"),
        new("literal", "音乐", "Music"),
        new("literal", "自动解锁条件##", "Automatic unlock condition##"),
        new("literal", "编辑收藏条目", "Edit collection entry"),
        new("literal", "删除此条目…", "Delete this entry…"),
        new("literal", "删除将使引用此 ID 的剧情解锁指令失效；游戏导入会定位这些引用。全局解锁身份也会失效。", "Deleting this entry leaves story unlock instructions referring to its ID unresolved. Game import identifies these references. Its global unlock identity will also become unavailable."),
        new("literal", "确认删除收藏条目", "Confirm deletion of collection entry"),
        new("literal", "删除收藏条目", "Delete collection entry"),
        new("literal", "取消删除", "Cancel deletion"),
        new("literal", "回想章节 ID", "Replay chapter ID"),
        new("literal", "回想章节", "Replay chapter"),
        new("literal", "公开入口 ID", "Public entry ID"),
        new("literal", "公开入口", "Public entry"),
        new("literal", "显式初始变量", "Explicit initial variables"),
        new("literal", "添加或重置此初始变量##", "Add or reset this initial variable##"),
        new("literal", "移除此初始值##", "Remove this initial value##"),
        new("literal", "回想从游戏默认舞台及上述初值启动；不会猜测主路线的过去状态，退出后不写回主游戏。", "Replays start with the game's default stage and these explicit values. They do not reconstruct an assumed past route or write state back to the main game."),
        new("literal", "CG 多图变体##", "CG image variants##"),
        new("literal", "封面用于鉴赏列表。添加变体后，详情只浏览已解锁变体；附加解锁可引用同一收藏库的结局或其他条目。每张图都支持缩放与拖动。", "The cover appears in the gallery list. When variants are present, details show only unlocked variants. Additional unlock requirements can refer to endings or other entries in the same library. Every image supports zooming and panning."),
        new("literal", "添加所选图片为变体", "Add selected image as a variant"),
        new("literal", "变体名称", "Variant name"),
        new("literal", "附加解锁", "Additional unlock requirement"),
        new("literal", "随本条目解锁", "Unlock with this entry"),
        new("literal", "稳定身份 · ", "Stable identity · "),
        new("literal", " · 图像 · ", " · Image · "),
        new("literal", "替换为所选图片##", "Replace with selected image##"),
        new("literal", "前移##", "Move earlier##"),
        new("literal", "后移##", "Move later##"),
        new("literal", "移除变体##", "Remove variant##")
    };
}
