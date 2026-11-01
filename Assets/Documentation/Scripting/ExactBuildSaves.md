# 严格构建存档与扩展注册

`NovelSession.buildFingerprint` 包含游戏内容、已知运行代码、明确登记的扩展实现与宿主贡献。创建 `SaveService` 时使用该值，不能用 `NovelProjectAsset.buildFingerprint` 代替。后者仍是导入时的内容摘要。

原生 `NovelPlayerComponent` 在 Scene/GameObject 生命周期中完成同一配置与匹配。`ConfigureCommands`、`ConfigureUiActions`、`ConfigureUiDataSources` 必须在会话启动前调用。注册声明和实现被会话冻结，之后修改调用方注册表只影响新会话。

直接嵌入会话时，把 UI 注册放在 `NovelSessionOptions`：

```csharp
var actions = new NovelUiActionRegistry();
actions.Register(() => new SetPageCountAction());
var sources = new NovelUiDataSourceRegistry();
sources.Register(PageCounterDataSource.schema,
    context => new PageCounterDataSource(context));
var options = new NovelSessionOptions {
    uiActions = actions,
    uiDataSources = sources
};
var session = new NovelSession(project, options: options);
```

游戏视图使用这个会话已冻结的注册；回想保留代码契约并创建独立状态与实例。数据源工厂登记时不执行，具体 `TSource` 通常由 lambda 返回值推断；不要把工厂擦除为抽象 `UiDataSource` 返回类型。原生创建时会再次检查具体实现。

小游戏或外部桥接使用了其他构建依赖时，通过组件的 `ConfigureHostCodeFingerprint` 或 SessionOptions 的 `hostCodeFingerprint` 贡献其构建标记。可以用 `NovelImplementationFingerprint.TypeCode` 计算明确已知实现的标记，再用 Combine 合并。插件不自动扫描其他 DLL 或猜测调用图。

工厂应保持实现类型稳定；参数和持久值属于中立声明/状态，不能依赖未经恢复的可变捕获。实现或宿主构建不匹配时，普通读档拒绝候选，当前会话继续保留。没有旧格式读取或隐式迁移。
