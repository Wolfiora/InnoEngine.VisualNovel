# 自定义 UI 动作

普通页面、选项、存读档和设置使用内置动作即可。需要少量定制行为时，实现 `NovelUiAction`，把中立元数据发布成 `.ivnuiactions` 目录。目录描述表单，不包含实现工厂。

## 创作步骤

1. 在“VN 自定义 UI 动作”创建目录，定义 `custom.` 开头的稳定 ID、显示名、说明、参数类型、默认值及枚举成员。也可以由 `NovelUiActionPublishing.Publish` 从显式注册表发布准确元数据。
2. 在“VN 游戏设置”关联这个目录，动作声明会独立复制到游戏定义。更改目录后需要重新关联。
3. 在界面设计器选择游戏，点击“使用所选游戏的变量与动作”。在控件点击动作菜单选择自定义动作。
4. 使用类型化固定参数，或选择参数字段；字段优先于固定值。列表只能使用目录默认列表或类型化列表字段，不解析文本列表。
5. 在原生 `NovelPlayerComponent` 创建会话前调用 `ConfigureUiActions`；该注册表属于当前组件/代际，不是全局发现表。当前正式 Runtime API 没有类型发现入口，必须显式注册。

`Assets/~Samples/ScriptExtension/SetPageCountAction.cs.txt` 展示一个简单处理器。将示例复制为项目正常 `Assets` 下的 `.cs` 后使用。该示例要求目标页面声明 Game Integer 字段 `count`。

```csharp
var actions = new NovelUiActionRegistry();
actions.Register(() => new SetPageCountAction());
player.ConfigureUiActions(actions);
```

直接嵌入时在创建会话前设置 `NovelSessionOptions.uiActions`。同一会话拥有冻结注册与严格代码匹配，视图使用该注册。见 [严格构建存档](ExactBuildSaves.md)。

## 生命周期与状态

动作同步执行，必须短小。需要长时间等待、取消或独立恢复的小游戏应使用已有 `NovelCommand`。`CanExecute` 和 `IsSelected` 为只读查询；通过上下文写值或跳页会被拒绝。选中态使用控件 Pressed 外观，键盘焦点仍独立绘制。

通过 `context.Get/Set` 访问现有 `page.*`、`variables.*`、设置等绑定。状态由相应领域决定保存与回滚；私有处理器字段不会被序列化。页面 Local 字段离开重置，Game 字段进入普通快照。执行处理器每次独立创建并释放；查询处理器由游戏视图拥有，视图关闭时释放。工厂和查询不得自行产生外部副作用。

声明 Save 表示动作执行后没有悬挂的非中立状态。声明 Rollback 表示允许普通回滚跨越动作；不声明则建立剧情回滚屏障。有 ExternalEffects 的动作禁止在预览执行，并且不能声明 Rollback。预览还必须声明 Preview。插件无法自动推断任意 C# 代码的副作用，扩展作者必须履行声明。

## 错误与边界

缺少实现、类型错误、元数据与工厂不一致、无效字段或处理器异常会禁用动作或中止本次执行，并提供 `动作 ID @ 控件 ID` 诊断。页面可以绑定 `interface.error`，游戏视图也公开 `actionError`。不会通过反射扫描 DLL 来补实现。

当前生产自动落盘仍受 Runtime 原生序列化根入口缺口影响；已有宿主提供原生持久化时，动作写入的游戏/页面值使用同一快照。无 GUI、GPU、设备或实际上手验收。
