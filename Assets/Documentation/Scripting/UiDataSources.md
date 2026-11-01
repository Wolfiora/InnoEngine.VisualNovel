# 自定义 UI 数据源

扩展玩法可以把类型化字段、稳定列表和图片提供给可视化页面。实现仍使用现有 `UiDataSource`；`NovelUiDataSourceRegistry` 负责明确注册、元数据匹配和视图所有权，不增加剧情状态或资产数据库。

## 使用流程

1. 定义 `UiDataSourceDefinition`，或在“VN 自定义 UI 数据”创建 `.ivnuidata`。声明源 ID、根字段、字段类型/枚举、读写权限、列表行字段和图片字段。
2. 使用 `NovelUiDataSourcePublishing.Publish` 从实际注册表发布元数据，可以避免手工表单与 C# 注册声明不一致。
3. 在游戏设置关联目录。在界面设计器选取游戏并使用它的变量、动作与数据源。值、显隐、可用、列表、图片和动作参数的菜单会列出声明路径。
4. 启动组件创建会话前调用 `ConfigureUiDataSources`。当前引擎未公开 Runtime 类型发现入口，目录不会自动加载处理器。

```csharp
var sources = new NovelUiDataSourceRegistry();
sources.Register(PageCounterDataSource.schema,
    context => new PageCounterDataSource(context));
player.ConfigureUiDataSources(sources);
```

`Register<TSource>` 由返回值推断具体类型，登记阶段不执行工厂；具体代码和工厂绑定参与严格存档匹配。直接嵌入时在 `NovelSessionOptions.uiDataSources` 配置，视图使用会话已冻结的注册。见 [严格构建存档](ExactBuildSaves.md)。

完整可编译源见 `Assets/~Samples/ScriptExtension/PageCounterDataSource.cs.txt`。复制到正常项目脚本目录后，页面需声明 Game Integer 字段 `count`。

## 路径与类型

根路径为 `custom.源ID.字段ID`，列表路径相同。源 ID 和字段 ID 都是单段稳定身份。列表子控件使用 `item.字段ID`，设计器只列出最近重复容器的行 schema，避免混用不同列表的同名字段。

每个源最多 64 个类型/图片字段；每次列表查询最多 4096 行，需要更大数据时由源实现明确分页。每行必须有唯一非空稳定 key，以及恰好符合声明的字段。UI 按 key 保留身份；写入前重查 key 是否仍存在，不能把旧显示下标当作当前行。

`UiDataSource.TryRead` 收到本地字段名；`ReadRows` 收到本地列表名。根 `TryWrite` 收到本地字段名，行写入重载收到 `item.*` 和带 owner 路径的 `UiDataRow`。行值只用于显示，写回由实现根据稳定 key 更新原生状态。

下拉选择器的数据列表需提供兼容的 `value` 字段；可提供 `text` 和 `enabled`。图片通过 `ReadImage` 提供，行图片同时接收稳定行 owner。动态图片需要在游戏附加打包集合显式声明，不能依靠运行时字符串自动找到并打包资源。

## 状态、副作用与释放

`NovelUiDataSourceContext.Get` 读取当前原生页面/剧情变量、设置等。查询阶段 `Set` 被禁止；只有已声明可写字段提交期间允许写入。写入继续使用原有类型、存档和回滚规则，源私有字段不会被序列化。不可回滚写入建立屏障。

源实例在当前游戏视图首次使用时创建。实现了 `IDisposable` 的实例在视图关闭时释放；回想使用独立实例。恢复存档后上下文读取恢复后的原生状态；缓存应使用实际读取的值或 `stateRevision` 判断失效，后者只表示会话替换，不是每次变量赋值通知。

查询和工厂须同步、无写入副作用。ExternalEffects 源在预览禁用；其他源仍需要 Preview 声明。不能通过递归查询其他 custom 源建立隐蔽依赖。需要异步工作时在宿主或剧情扩展完成，再把已有结果呈现为数据源。

缺少实现、契约不一致、字段类型错误、重复 key、越界值和查询异常产生 `interface.dataError`，并可从 `NovelGameView.dataSourceError` 读取。失败不抛入渲染器。Runtime 自动持久化入口、设备与实际上手仍按实施状态单独列出，本文不代表实机验收。
