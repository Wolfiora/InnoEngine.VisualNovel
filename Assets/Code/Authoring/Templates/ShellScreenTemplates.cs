using System;
using System.Collections.Generic;
using InnoEngine.Text;
using Inno.VisualNovel.Model.Interface;
using Inno.VisualNovel.Model.Localization;

namespace Inno.VisualNovel.Authoring.Templates;

/// <summary>Creates editable native pages for the implemented VN shell actions.</summary>
public static class ShellScreenTemplates
{
    /// <summary>Instantiates an authored page with independent element identities and editable styles.</summary>
    /// <param name="role">The implemented system role.</param>
    /// <param name="font">The imported default font.</param>
    /// <param name="theme">Clear, Paper or Cinema palette.</param>
    /// <returns>An unsaved editable screen.</returns>
    public static ScreenAsset Create(
        string role,
        FontAsset font,
        string theme
    ) {
        (uint background, uint panel, uint foreground, uint accent) = theme switch
        {
            "Paper" => (0xEEE5D5FFu, 0xFFF9EDFAu, 0x453C35FFu, 0x96634BFFu),
            "Cinema" => (0x101318FFu, 0x171D26F0u, 0xEDEEEFFFu, 0xCEAD79FFu),
            _ => (0x141C2AFFu, 0x233149F2u, 0xF2F5FCFFu, 0x8FBFDBFFu)
        };
        var screen = new ScreenAsset { title = role, font = font, modal = role is "pause" or "settings" or "history" or "save" or "load" or "confirm" };
        if (role is "extras" or "gallery" or "music" or "replays" or "endings" or "glossary" or "characters" or "collectionDetail")
            return CollectionScreenTemplates.Create(role, font, background, panel, foreground, accent);
        var children = new List<UiElementDefinition>
        {
            Panel("background", 0, 0, 1920, 1080, background, false),
            Panel("accent-line", 110, 116, 72, 5, accent, false)
        };
        if (role == "loading")
        {
            UiElementDefinition waiting = Text("profile-loading", "正在读取玩家设置与全局进度…", 220, 240, 1480, 130, 40, foreground); waiting.visibleBinding = "profile.loading";
            UiElementDefinition failure = Text("profile-error", "", 220, 240, 1480, 300, 30, foreground, "profile.error"); failure.visibleBinding = "profile.failed";
            UiElementDefinition retry = Button("profile-retry", "重新读取", "profile.retry"); retry.visibleBinding = "profile.failed"; retry.layout.x = 220; retry.layout.y = 570;
            UiElementDefinition defaults = Button("profile-defaults", "使用默认值…", "profile.defaults"); defaults.visibleBinding = "profile.canUseDefaults"; defaults.layout.x = 700; defaults.layout.y = 570;
            UiElementDefinition confirm = Stack("profile-confirm", 220, 700, 1300, 280, [
                Text("profile-warning", "继续后将从默认进度开始，后续保存可能替换无法读取的数据。", 0, 0, 1250, 90, 28, foreground),
                Button("profile-confirm-defaults", "确认使用默认值", "profile.defaults.confirm"), Button("profile-cancel-defaults", "返回重试", "profile.defaults.cancel")]); confirm.visibleBinding = "profile.confirmDefaults";
            children.AddRange([waiting, failure, retry, defaults, confirm]);
        }
        else if (role == "title")
        {
            children.Add(Text("eyebrow", "VISUAL NOVEL", 112, 155, 900, 70, 24, accent));
            children.Add(Text("title", "", 106, 240, 1600, 200, 76, foreground, "game.title"));
            children.Add(Text("subtitle", "每一个选择，都将通向新的故事。", 112, 456, 1200, 80, 30, foreground));
            UiElementDefinition load = Button("load", "读取故事", "open", "load"); load.enabledBinding = "saves.ready";
            UiElementDefinition resume = Button("continue", "继续故事", "continue"); resume.enabledBinding = "saves.canContinue";
            var menu = Stack("menu", 112, 548, 500, 516,
                [Button("new", "开始新故事", "newGame"), resume, load, Button("extras", "鉴赏与资料", "open", "extras"), Button("settings", "阅读设置", "open", "settings"), Button("credits", "制作人员", "open", "credits")]);
            menu.layout.gap = 12; children.Add(menu);
            var message = Text("title-save-message", "", 700, 800, 1000, 200, 28, foreground, "saves.message"); message.visibleBinding = "saves.failed"; children.Add(message);
        }
        else if (role == "reading")
        {
            children.Clear();
            children.Add(Panel("background", 0, 0, 1920, 1080, background, false));
            children[0].behindStage = true;
            UiElementDefinition choicePrompt = Text("choice-prompt", "", 410, 60, 1100, 90, 32, foreground, "choice.prompt"); choicePrompt.visibleBinding = "choice.visible";
            children.Add(choicePrompt);
            UiElementDefinition timerLabel = Text("choice-timer-label", "剩余秒数", 430, 150, 400, 80, 24, foreground); timerLabel.visibleBinding = "choice.timed";
            UiElementDefinition timerSeconds = Text("choice-timer-seconds", "", 830, 150, 140, 80, 24, accent, "choice.remainingSeconds"); timerSeconds.visibleBinding = "choice.timed";
            children.AddRange([timerLabel, timerSeconds, new() { id = "choice-timer-progress", kind = UiElementKind.Progress, binding = "choice.timeRemainingRatio", visibleBinding = "choice.timed",
                minimum = 0, maximum = 1, blocksPointer = false, layout = new() { x = 1000, y = 178, width = 480, height = 50 }, style = new() { background = 0, foreground = accent, focus = accent } }]);
            children.Add(new()
            {
                id = "advance-surface", kind = UiElementKind.Button, action = "advance", visibleBinding = "reading.advanceVisible",
                layout = new() { width = 1920, height = 1080 }, style = new() { background = 0, hover = 0, pressed = 0, focus = 0 }
            });
            children.Add(new()
            {
                id = "dialogue", kind = UiElementKind.Panel, visibleBinding = "dialogue.adv", blocksPointer = false,
                layout = new() { x = 96, y = 710, width = 1728, height = 260, padding = 20 }, style = new() { background = panel },
                children = [Text("speaker", "", 8, 0, 1600, 58, 30, accent, "dialogue.speaker"), new()
                {
                    id = "dialogue-scroll", kind = UiElementKind.Scroll, followEndBinding = "dialogue.adv", blocksPointer = false,
                    layout = new() { x = 8, y = 65, width = 1648, height = 160, clip = true }, style = new() { background = 0 },
                    children = [new() { id = "line", kind = UiElementKind.Text, binding = "dialogue.text", blocksPointer = false,
                        layout = new() { widthMode = UiSizeMode.Fill, heightMode = UiSizeMode.Content }, style = new() { foreground = foreground, fontSize = 32, textPadding = 0 } }]
                }]
            });
            ReadingPresentationTemplates.Add(children, panel, foreground, accent);
            children.Add(new() { id = "advance-wait", kind = UiElementKind.Panel, visibleBinding = "reading.waitingForAdvance", blocksPointer = false,
                layout = new() { x = 580, y = 700, width = 760, heightMode = UiSizeMode.Content, minHeight = 96, padding = 20 }, style = new() { background = panel }, children = [new() {
                    id = "advance-wait-hint", kind = UiElementKind.Text, text = "点击或按确认继续", blocksPointer = false,
                    layout = new() { widthMode = UiSizeMode.Fill, heightMode = UiSizeMode.Content }, style = new() { foreground = foreground, fontSize = 28, textPadding = 0 } }] });
            UiElementDefinition option = Button("option", "", "choice");
            option.binding = "item.text"; option.enabledBinding = "item.enabled"; option.argumentBinding = "item.id";
            option.tooltipBinding = "item.reason";
            option.layout.widthMode = UiSizeMode.Fill;
            option.style.textPadding = 48;
            UiElementDefinition selectedMark = Text("option-selected", "✓", 12, 4, 30, 42, 24, accent); selectedMark.visibleBinding = "item.selected";
            option.children = [selectedMark];
            children.Add(new()
            {
                id = "choices", kind = UiElementKind.Scroll, visibleBinding = "choice.visible", repeatBinding = "choices",
                blocksPointer = false, layout = new() { x = 410, y = 240, width = 1100, height = 600, gap = 18, padding = 12, clip = true }, children = [option]
            });
            children.Add(new()
            {
                id = "toolbar", kind = UiElementKind.Horizontal, blocksPointer = false, visibleBinding = "reading.toolbarVisible", layout = new() { x = 96, y = 992, width = 1728, height = 64, gap = 12 },
                children = [Button("pause", "菜单", "open", "pause"), Button("history", "历史", "open", "history"), Button("auto", "自动", "automatic"),
                    Button("skip", "快进", "skip"), Button("hide", "隐藏", "hide"), Button("rollback", "回退", "rollback")]
            });
            UiElementDefinition completion = Text("complete", "本段故事已经结束。感谢阅读。", 280, 420, 1400, 180, 46, foreground);
            completion.visibleBinding = "game.completed"; children.Add(completion);
            UiElementDefinition commandStatus = Text("command-status", "请稍候…", 280, 420, 1350, 100, 36, foreground); commandStatus.visibleBinding = "command.active"; children.Add(commandStatus);
            UiElementDefinition cancelCommand = Button("command-cancel", "取消当前指令", "command.cancel"); cancelCommand.visibleBinding = "command.cancelable"; cancelCommand.layout.x = 280; cancelCommand.layout.y = 560; children.Add(cancelCommand);
            UiElementDefinition exitReplay = Button("replay-exit", "退出回想", "replay.exit"); exitReplay.visibleBinding = "game.replay";
            exitReplay.layout.x = 1560; exitReplay.layout.y = 40; children.Add(exitReplay);
            UiElementDefinition failure = Text("error", "", 160, 200, 1600, 600, 28, 0xFFAFAFFF, "game.error");
            failure.visibleBinding = "game.faulted"; children.Add(failure);
            UiElementDefinition submit = Button("input-submit", "确认", "input.submit");
            submit.enabledBinding = "input.valid"; submit.layout.x = 320; submit.layout.y = 300;
            UiElementDefinition cancel = Button("input-cancel", "取消", "input.cancel");
            cancel.visibleBinding = "input.canCancel"; cancel.layout.x = 600; cancel.layout.y = 300;
            children.Add(new()
            {
                id = "player-input-overlay", kind = UiElementKind.Panel, visibleBinding = "input.visible", blocksPointer = true,
                layout = new() { width = 1920, height = 1080 }, style = new() { background = 0x000000B0 }, children = [new()
                {
                    id = "player-input-dialog", kind = UiElementKind.Panel, blocksPointer = true,
                    layout = new() { x = 460, y = 320, width = 1000, height = 440, padding = 32 }, style = new() { background = panel },
                    children = [Text("input-prompt", "", 16, 0, 880, 100, 36, foreground, "input.prompt"),
                        new() { id = "input-value", kind = UiElementKind.Input, binding = "input.value", action = "input.submit", maxLength = 1024,
                            layout = new() { x = 16, y = 140, width = 880, height = 90 }, style = new() { background = background, foreground = foreground, focus = accent, fontSize = 36 } }, submit, cancel]
                }]
            });
        }
        else
        {
            string title = role switch { "pause" => "故事暂停", "settings" => "阅读设置", "history" => "阅读历史", "credits" => "制作人员",
                "save" => "保存故事", "load" => "读取故事", "confirm" => "请确认", _ => throw new ArgumentException("Unknown shell role.", nameof(role)) };
            children.Add(Text("heading", title, 106, 165, 1500, 140, 58, foreground));
            if (role == "pause")
            {
                UiElementDefinition save = Button("save", "保存故事", "open", "save"); save.enabledBinding = "saves.canSave";
                UiElementDefinition load = Button("load", "读取故事", "open", "load"); load.enabledBinding = "saves.ready";
                children.Add(Stack("pause-menu", 112, 320, 640, 620,
                    [Button("resume", "继续阅读", "resume"), save, load, Button("history", "阅读历史", "open", "history"), Button("settings", "阅读设置", "open", "settings"), Button("title", "返回标题", "title")]));
                children[^1].children[^1].visibleBinding = "game.normal";
                UiElementDefinition exit = Button("pause-replay-exit", "退出回想", "replay.exit"); exit.visibleBinding = "game.replay";
                children[^1].children = [.. children[^1].children, exit];
            }
            if (role is "save" or "load")
            {
                children.Add(new() { id = "slot-categories", kind = UiElementKind.Horizontal, blocksPointer = false,
                    layout = new() { x = 112, y = 285, width = 1696, height = 76, gap = 18 }, children = [
                        Button("manual", "手动存档", "slots.manual"), Button("automatic", "自动存档", "slots.automatic"), Button("quick", "快速存档", "slots.quick")] });
                UiElementDefinition primary = Button("slot-primary", role == "save" ? "写入槽位" : "读取", role == "save" ? "save.write" : "save.load");
                primary.argumentBinding = "item.key"; primary.enabledBinding = role == "save" ? "item.canSave" : "item.canLoad";
                UiElementDefinition delete = Button("slot-delete", "删除…", "save.delete"); delete.argumentBinding = "item.key"; delete.enabledBinding = "item.canDelete";
                children.Add(new() { id = "slot-list", kind = UiElementKind.Scroll, repeatBinding = "saves.slots",
                    layout = new() { x = 112, y = 390, width = 1696, height = 470, gap = 16, padding = 8, clip = true },
                    children = [new() { id = "slot-row", kind = UiElementKind.Horizontal, blocksPointer = false,
                        layout = new() { widthMode = UiSizeMode.Fill, height = 170, gap = 18 }, children = [
                            new() { id = "slot-thumbnail", kind = UiElementKind.Image, imageBinding = "item.thumbnail", imageFit = UiImageFit.Contain, blocksPointer = false,
                                layout = new() { width = 256, height = 144 }, style = new() { background = panel } },
                            Text("slot-summary", "", 0, 0, 786, 170, 28, foreground, "item.label"), primary, delete] }] });
                children.Add(Text("slot-message", "", 112, 870, 1696, 70, 24, foreground, "saves.message"));
                UiElementDefinition previous = Button("slots-previous", "上一页", "slots.previous"); previous.enabledBinding = "saves.previous";
                UiElementDefinition next = Button("slots-next", "下一页", "slots.next"); next.enabledBinding = "saves.next";
                children.Add(new() { id = "slot-pagination", kind = UiElementKind.Horizontal, blocksPointer = false,
                    layout = new() { x = 112, y = 968, width = 1100, height = 76, gap = 18 },
                    children = [previous, Text("slot-page", "", 0, 0, 220, 76, 28, foreground, "saves.page"), next] });
            }
            if (role == "confirm")
            {
                children.Add(Text("confirmation-message", "", 112, 380, 1600, 240, 36, foreground, "confirmation.message"));
                children.Add(new() { id = "confirmation-actions", kind = UiElementKind.Horizontal, blocksPointer = false,
                    layout = new() { x = 112, y = 700, width = 1000, height = 90, gap = 24 },
                    children = [Button("confirm-accept", "确认", "confirm.accept"), Button("confirm-cancel", "取消", "confirm.cancel")] });
            }
            if (role == "credits") children.Add(Text("credits", "使用 InnoEngine.VisualNovel 制作\n\n在界面设计器中编辑这一页，写下你的创作团队。\n\n字体：Noto Sans CJK · SIL Open Font License 1.1", 112, 380, 1600, 440, 32, foreground));
            if (role == "history") children.Add(new()
            {
                id = "history-list", kind = UiElementKind.Scroll, repeatBinding = "history", blocksPointer = true, followEndBinding = "history.followLatest",
                layout = new() { x = 112, y = 360, width = 1696, height = 570, gap = 12, padding = 12, clip = true },
                children = [new() { id = "history-row", kind = UiElementKind.Vertical,
                    layout = new() { widthMode = UiSizeMode.Fill, heightMode = UiSizeMode.Content, gap = 6 }, children = [
                        new() { id = "history-text", kind = UiElementKind.Text, binding = "item.text", blocksPointer = false,
                            layout = new() { widthMode = UiSizeMode.Fill, heightMode = UiSizeMode.Content }, style = new() { foreground = foreground, fontSize = 28, textPadding = 12 } },
                        new() { id = "history-voice", kind = UiElementKind.Button, text = "重播语音", action = "history.voice", argumentBinding = "item.index", visibleBinding = "item.voiced",
                            layout = new() { width = 280, height = 64 }, style = new() { foreground = foreground, background = 0x253447FF, fontSize = 26, textPadding = 12 } },
                        new() { id = "history-rollback", kind = UiElementKind.Button, text = "回到这句", action = "history.rollback", argumentBinding = "item.index", enabledBinding = "item.canRollback",
                            layout = new() { width = 280, height = 64 }, style = new() { foreground = foreground, background = 0x253447FF, fontSize = 26, textPadding = 12 } }
                    ] }]
            });
            if (role == "history")
            {
                UiElementDefinition previousHistory = Button("history-previous", "上一页", "history.previous"); previousHistory.enabledBinding = "history.previous";
                UiElementDefinition nextHistory = Button("history-next", "下一页", "history.next"); nextHistory.enabledBinding = "history.next";
                children.Add(new() { id = "history-pagination", kind = UiElementKind.Horizontal, blocksPointer = false,
                    layout = new() { x = 112, y = 968, width = 1100, height = 76, gap = 18 },
                    children = [previousHistory, Text("history-page", "", 0, 0, 220, 76, 28, foreground, "history.page"), nextHistory] });
            }
            if (role == "settings")
            {
                UiElementDefinition settings = Stack("settings-list", 112, 340, 1600, 580,
                    [Slider("text-speed", "文字速度", "settings.textSpeed", 0.1, 5), Slider("auto-delay", "自动等待秒数", "settings.autoDelay", 0.1, 10),
                        Slider("font-scale", "字体大小", "settings.fontScale", 0.75, 2), Slider("master", "总音量", "settings.masterVolume", 0, 1),
                        Slider("bgm-volume", "背景音乐", "settings.bgmVolume", 0, 1), Slider("ambience-volume", "环境音", "settings.ambienceVolume", 0, 1),
                        Slider("sfx-volume", "音效", "settings.sfxVolume", 0, 1), Slider("voice-volume", "语音", "settings.voiceVolume", 0, 1), Slider("ui-volume", "界面声音", "settings.uiVolume", 0, 1),
                        Toggle("read-only", "仅快进已读内容", "settings.skipReadOnly"), Toggle("motion", "减少动态效果", "settings.reducedMotion"), Toggle("flashes", "允许闪烁效果", "settings.allowFlashing"),
                        Toggle("high-contrast", "高对比阅读", "settings.highContrast"), Toggle("voice-advance", "推进对白时停止语音", "settings.stopVoiceOnAdvance")]);
                settings.kind = UiElementKind.Scroll; settings.layout.clip = true; children.Add(settings);
                settings.children = [.. settings.children, Text("keyboard-heading", "阅读快捷键", 0, 0, 1400, 70, 32, foreground),
                    new() { id = "hold-skip-row", kind = UiElementKind.Horizontal, blocksPointer = false, layout = new() { widthMode = UiSizeMode.Fill, height = 84, gap = 16 }, children = [
                        Text("hold-skip-label", "按住快进组合", 0, 0, 600, 80, 28, foreground), new() { id = "hold-skip-choice", kind = UiElementKind.Selector,
                            binding = "settings.holdSkipModifiers", optionsBinding = "settings.holdSkipChoices", layout = new() { width = 800, height = 76 },
                            style = new() { background = panel, foreground = foreground, focus = accent, hover = accent & 0xFFFFFF00 | 0x55, fontSize = 28, textPadding = 16 } }] },
                    ShortcutRows("keyboard", "settings.shortcuts", "settings.shortcutKeys"),
                    Text("navigation-heading", "菜单导航键", 0, 0, 1400, 70, 32, foreground),
                    ShortcutRows("navigation", "settings.navigationShortcuts", "settings.navigationKeys"),
                    Button("keyboard-reset", "恢复默认快捷键", "keyboard.reset"),
                    Text("keyboard-help", "可重设阅读和菜单组合；相同组合不能分配多项。输入文字时不触发字母导航键。Tab、Shift+Tab 和文字编辑按键保持可用；按住快进可修改或关闭。", 0, 0, 1400, 160, 24, foreground)];
                var characterGain = Slider("character-gain", "", "item.voiceVolume", 0, 1); characterGain.children[0].binding = "item.name";
                var characterEnabled = Toggle("character-voice-enabled", "启用此角色语音", "item.voiceEnabled");
                settings.children = [.. settings.children, new() { id = "character-voices", kind = UiElementKind.Vertical, repeatBinding = "characters.voices", blocksPointer = false,
                    layout = new() { widthMode = UiSizeMode.Fill, heightMode = UiSizeMode.Content, gap = 14 }, children = [new() { id = "character-voice", kind = UiElementKind.Vertical, blocksPointer = false,
                        layout = new() { widthMode = UiSizeMode.Fill, heightMode = UiSizeMode.Content, gap = 8 }, children = [characterGain, characterEnabled] }] }];
                settings.children = [new() { id = "language-row", kind = UiElementKind.Horizontal, blocksPointer = false,
                    layout = new() { width = 1600, height = 90, gap = 16 }, children = [Text("language-label", "游戏语言", 0, 0, 600, 80, 28, foreground),
                        new() { id = "language-selector", kind = UiElementKind.Selector, binding = "settings.language", optionsBinding = "languages",
                            layout = new() { width = 800, height = 76 }, style = new() { background = panel, foreground = foreground, focus = accent, hover = accent & 0xFFFFFF00 | 0x55, fontSize = 28, textPadding = 16 } }] }, .. settings.children];
            }
            if (role == "settings")
            {
                UiElementDefinition profileError = Text("profile-write-error", "", 220, 930, 1200, 66, 24, foreground, "profile.error"); profileError.visibleBinding = "profile.failed"; children.Add(profileError);
                UiElementDefinition retryProfile = Button("profile-write-retry", "重试保存玩家数据", "profile.retry"); retryProfile.visibleBinding = "profile.failed"; retryProfile.layout.x = 220; retryProfile.layout.y = 1004; retryProfile.layout.width = 460; children.Add(retryProfile);
            }
            UiElementDefinition back = Button("back", "返回", "back");
            back.layout.x = 1460; back.layout.y = 968; back.layout.width = 340;
            if (role != "confirm") children.Add(back);
        }
        screen.root = new() { id = "root", kind = UiElementKind.Panel, blocksPointer = false,
            layout = new() { width = 1920, height = 1080, clip = true }, style = new() { background = 0 }, children = children.ToArray() };
        if (role == "reading") ReadingPresentationTemplates.AddPortrait(screen);
        if (role is "history" or "settings" or "credits" or "save" or "load") screen.initialFocus = "back";
        else if (role == "title") screen.initialFocus = "new";
        AssignTextIds(screen.root);
        UiAnimationTemplates.Apply(screen.root);
        return screen;

        void AssignTextIds(UiElementDefinition element)
        {
            element.textId = StoryTextKeys.Ui(role, element.id);
            element.tooltipTextId = StoryTextKeys.Ui(role, element.id + "/tooltip");
            foreach (UiElementDefinition child in element.children) AssignTextIds(child);
        }

        UiElementDefinition Button(string id, string text, string action, string argument = "") => new()
        {
            id = id, kind = UiElementKind.Button, text = text, tooltip = Help(action), action = action, argument = argument,
            layout = new() { width = 240, height = 76 }, style = new() { background = panel, hover = theme == "Paper" ? 0xE4D7BDFFu : theme == "Cinema" ? 0x343432FFu : 0x344863FFu,
                pressed = theme == "Paper" ? 0xD0BE9FFFu : 0x142336FFu, foreground = foreground, focus = accent, fontSize = 28, textPadding = 18 }
        };
        string Help(string action) => action switch
        {
            "automatic" => "按阅读速度和语音结束时间自动推进；再次操作可关闭。",
            "skip" => "点击切换快进；按住快进组合可在设置中调整。默认在未读台词、选项和脚本屏障前停止。",
            "hide" => "隐藏阅读界面；下一次推进操作恢复界面。",
            "rollback" => "恢复到保留的阅读检查点；已读和全局解锁不会撤销。",
            "continue" => "读取当前内容构建中最近的有效存档。",
            "save.delete" => "删除此槽位前需要确认。",
            _ => string.Empty
        };
        UiElementDefinition Stack(string id, float x, float y, float width, float height, UiElementDefinition[] items) => new()
        {
            id = id, kind = UiElementKind.Vertical, blocksPointer = false, layout = new() { x = x, y = y, width = width, height = height, gap = 18 }, children = items
        };
        UiElementDefinition ShortcutRows(
            string prefix,
            string rows,
            string options
        ) => new() { id = prefix + "-bindings", kind = UiElementKind.Vertical, repeatBinding = rows, blocksPointer = false,
            layout = new() { widthMode = UiSizeMode.Fill, heightMode = UiSizeMode.Content, gap = 12 }, children = [new() {
                id = prefix + "-row", kind = UiElementKind.Horizontal, blocksPointer = false, layout = new() { widthMode = UiSizeMode.Fill, height = 84, gap = 16 }, children = [
                    Text(prefix + "-name", "", 0, 0, 600, 80, 28, foreground, "item.name"), new() { id = prefix + "-key", kind = UiElementKind.Selector,
                        binding = "item.shortcutKey", optionsBinding = options, layout = new() { width = 800, height = 76 },
                        style = new() { background = panel, foreground = foreground, focus = accent, hover = accent & 0xFFFFFF00 | 0x55, fontSize = 28, textPadding = 16 } }] }] };
        UiElementDefinition Slider(string id, string label, string binding, double minimum, double maximum) => new()
        {
            id = id, kind = UiElementKind.Horizontal, blocksPointer = false, layout = new() { width = 1600, height = 80 },
            children = [Text(id + "-label", label, 0, 0, 600, 80, 28, foreground), new() { id = id + "-control", kind = UiElementKind.Slider, binding = binding, minimum = minimum, maximum = maximum,
                layout = new() { width = 800, height = 76 }, style = new() { background = panel, foreground = foreground, focus = accent, fontSize = 28 } }]
        };
        UiElementDefinition Toggle(string id, string label, string binding) => new()
        {
            id = id, kind = UiElementKind.Horizontal, blocksPointer = false, layout = new() { width = 1600, height = 80 },
            children = [Text(id + "-label", label, 0, 0, 600, 80, 28, foreground), new() { id = id + "-control", kind = UiElementKind.Toggle, binding = binding,
                layout = new() { width = 160, height = 76 }, style = new() { background = panel, foreground = foreground, focus = accent, fontSize = 24, textPadding = 40 } }]
        };
    }

    private static UiElementDefinition Text(string id, string text, float x, float y, float width, float height, float size, uint color, string binding = "") => new()
    {
        id = id, kind = UiElementKind.Text, text = text, binding = binding, blocksPointer = false,
        layout = new() { x = x, y = y, width = width, height = height }, style = new() { foreground = color, fontSize = size, textPadding = 0, background = 0 }
    };
    private static UiElementDefinition Panel(string id, float x, float y, float width, float height, uint color, bool blocks) => new()
    {
        id = id, kind = UiElementKind.Panel, blocksPointer = blocks, layout = new() { x = x, y = y, width = width, height = height }, style = new() { background = color }
    };
}
