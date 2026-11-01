# C# 指令样例

在创建向导选择「教学 · 脚本扩展」，会生成配套可编辑章节、参数定义及 Integer 分数变量。其余教学样例不需要脚本，这一组专门示范扩展契约；没有注册实现时会给出 Missing command implementation 诊断。

将 `DelayScoreCommand.cs.txt` 复制到普通 `Assets` 目录并改名为 `DelayScoreCommand.cs`，放入引用 VN Model 与 Narrative 的运行程序集后编译；正式游戏不能引用 `~Samples` 子树。模板以文本分发，避免在尚未导入时参与默认脚本编译。它的参数和返回值均有真实类型，等待状态可保存，取消有明确图输出；逻辑验证工程会编译同一份模板源。

扩展作者创建当前代际的注册表：

```csharp
var registry = new NovelCommandRegistry();
registry.Register(static () => new DelayScoreCommand());
player.ConfigureCommands(registry);
```

`ConfigureCommands` 在首次访问 `player.session` 或开始运行前调用。嵌入使用方也可传给 `new NovelSession(project, commands: registry)`。不要把注册表或 handler 放在静态全局字段。

在 Editor scope 的脚本中调用 `NovelCommandPublishing.Publish(registry, AssetPath.Project("Game/Commands.ivncommands"))` 发布原生表单目录。创作者在「VN 游戏」将目录关联到游戏，再在剧情中添加「自定义 C# 指令」，选择目录并应用表单。参数用现有表达式表单配置，返回值选择 Integer 变量，连接 `next` 与 `cancel` 后运行。

这条显式注册链不依赖运行时反射扫描。自动发现仍需要当前正式宿主尚未导出的类型发现契约。
