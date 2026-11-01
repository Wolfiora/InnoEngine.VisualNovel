using System;
using System.Collections.Generic;
using System.Linq;
using Inno.VisualNovel.Model.Performance;

namespace Inno.VisualNovel.Authoring.BuiltInResources;

/// <summary>Lists known shipped presets; native Assets remains the only asset identity and loading authority.</summary>
public static class BuiltInResourceCatalog
{
    private static readonly IReadOnlyList<BuiltInResourceEntry> m_entries = Array.AsReadOnly(Create());
    /// <summary>Gets immutable metadata for available presets, without scanning or caching imported objects.</summary>
    public static IReadOnlyList<BuiltInResourceEntry> entries => m_entries;
    private static BuiltInResourceEntry[] Create()
    {
        var entries = new List<BuiltInResourceEntry>();
        foreach (var widget in Templates.UiWidgetTemplates.entries) entries.Add(new(BuiltInResourceKind.Widget, "BuiltIn/Widgets/" + widget.id + ".ivnui", widget.title, widget.description));
        foreach (string name in Templates.ShellThemeTemplates.names) entries.Add(new(BuiltInResourceKind.Theme, "BuiltIn/Themes/" + name + "/" + name + ".ivntheme", name, "18 个可编辑系统页面、样式继承、控件状态与字号 token。"));
        Add(BuiltInResourceKind.Stage, "BuiltIn/Backgrounds/", ".ivnstage", "二维舞台背景；1920×1080 原创矢量插画。", "Classroom|午后的教室", "Station|黄昏车站", "Seafront|海边步道", "Woodland|林间小径", "Bedroom|安静卧室", "Rooftop|天台远景");
        foreach (string name in new[] { "Cheng", "Shu" }) entries.Add(new(BuiltInResourceKind.Character, "BuiltIn/Characters/" + name + "/" + name + ".ivncharacter", name == "Cheng" ? "澄 · 分层示例角色" : "树 · 分层示例角色", "2 姿势、6 表情、5 独立图层与可编辑呼吸。"));
        Add(BuiltInResourceKind.Motion, "BuiltIn/Motions/", ".ivnclip", "角色或 camera 目标槽；复制后在时间线编辑关键帧。",
            "enter-left|从左入场", "enter-right|从右入场", "enter-bottom|从下入场", "fade-in|淡入", "fade-out|淡出", "exit-left|向左退场", "exit-right|向右退场", "exit-bottom|向下退场",
            "bounce|轻跳强调", "emphasis|缩放强调", "shake|短促抖动", "nod|点头", "float|轻轻漂浮", "breathe|细微呼吸", "sway|柔和摇摆", "camera-push|镜头推进", "camera-pull|镜头拉远", "camera-pan|镜头横移", "camera-shake|镜头震动", "dim|压暗角色");
        Add(BuiltInResourceKind.Particle, "BuiltIn/Particles/", ".ivneffect", "舞台发射器；固定随机种子，支持保存与逻辑时间预览。", "Rain|雨", "Snow|雪", "Dust|尘", "Petals|花瓣", "Fireflies|萤火", "LightPoints|光点", "Bubbles|气泡", "Confetti|纸屑");
        Add(BuiltInResourceKind.Transition, "BuiltIn/Transitions/", ".ivneffect", "舞台、图层组或完整输出边界；可编辑时长、柔度与中心。", "fade-black|淡出至黑", "fade-white|淡出至白", "dissolve|交叉溶解", "wipe-right|向右擦除", "wipe-down|向下擦除", "wipe-diagonal|斜向擦除", "mask-noise|颗粒溶解", "mask-checker|棋盘展开", "mask-blinds|百叶窗", "slide-left|向左推移", "iris-open|虹膜展开", "iris-close|虹膜收拢");
        string[] effectNames = ["柔焦模糊", "柔和暗角", "灰度", "暖色调", "像素化", "闪白", "电影遮幅", "怀旧棕褐", "反相", "色差"];
        foreach (var kind in Enum.GetValues<ScreenEffectKind>()) entries.Add(new(BuiltInResourceKind.ScreenEffect, "BuiltIn/ScreenEffects/" + kind.ToString().ToLowerInvariant() + ".ivneffect", effectNames[(int)kind], "舞台或包含 UI 的后处理；复制后编辑强度与参数。"));
        Add(BuiltInResourceKind.Audio, "BuiltIn/Audio/UI/", ".ivnaudio", "UI 类别单次提示；在音频面板编辑音量与淡变。",
            "Hover|轻触悬停", "Confirm|柔和确认", "Cancel|返回取消", "Open|打开页面", "Close|关闭页面", "PageForward|向前翻页", "PageBack|向后翻页", "Notice|轻声提示", "Error|操作不可用", "Save|保存完成", "Load|读取完成", "Unlock|收藏解锁", "Focus|焦点移动", "Select|选择条目", "ToggleOn|启用选项", "ToggleOff|关闭选项", "Slider|调整滑块", "Typing|文字轻响", "Start|故事开始", "End|段落落幕");
        Add(BuiltInResourceKind.Audio, "BuiltIn/Audio/Music/", ".ivnaudio", "8 秒周期合成氛围，教学用途；循环播放并具有淡变。", "Dawn|晨光", "PaperRoom|纸间", "QuietCinema|静影");
        foreach (string name in ("play pause stop next previous forward rewind skip auto history save load quick-save quick-load settings menu close check plus minus chevron-left chevron-right chevron-up chevron-down arrow-left arrow-right arrow-up arrow-down home search filter sort grid list folder file image music voice volume volume-mute headphones language font eye eye-off lock unlock star heart bookmark info warning error help refresh undo redo copy trash edit link external fullscreen window controller keyboard mouse").Split(' '))
            entries.Add(new(BuiltInResourceKind.Icon, "BuiltIn/Icons/" + name + ".png", name, "128×128 白色透明图标；在 UI 元素中使用图片着色。"));
        if (entries.Select(static entry => entry.path).Distinct(StringComparer.Ordinal).Count() != entries.Count) throw new InvalidOperationException("Built-in preset paths must be unique.");
        return entries.ToArray();
        void Add(
            BuiltInResourceKind kind,
            string folder,
            string extension,
            string usage,
            params string[] values
        )
        {
            foreach (string value in values) { string[] fields = value.Split('|'); entries.Add(new(kind, folder + fields[0] + extension, fields[1], usage)); }
        }
    }
}
