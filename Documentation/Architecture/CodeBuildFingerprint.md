# 普通存档的运行代码匹配

普通存档必须匹配 `NovelSession.buildFingerprint`。它组合原生内容/剧情指纹、Model/Narrative/Stage/UI/Player 的已知实现，以及显式注册的命令、UI 动作、UI 数据源与宿主贡献。Scene 中的 `NovelSceneSystem` 还加入 Rendering/Integration 的实现信息。调用者为 SaveService 传入会话的最终值，不能继续使用游戏资产的纯内容摘要。

实现标记读取已知类型与显式工厂的公开 CLR 模块 MVID、类型名称和方法绑定，然后仅保留散列文本。[Microsoft 的 ModuleVersionId 契约](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.module.moduleversionid?view=net-10.0)用于区分发布模块。这不执行程序集/目录扫描、不读取引擎私有成员、不引入扩展发现或另一套资产身份；也不把 CLR Type、Module、Method 或委托写入存档。

三个扩展注册表先冻结中立声明和已登记实现。命令/动作创建时再次核对具体实现，不能在相同声明下由工厂换成另一类型。UI 数据源按 `Register<TSource>` 的具体类型登记，登记阶段不调用需要游戏上下文的工厂；真正创建时核对精确类型，拒绝并释放不匹配对象。

UI 注册表在创建会话前放入 `NovelSessionOptions.uiActions/uiDataSources`，由同一会话拥有。`NovelGameView` 使用这些已冻结注册，不能在计算存档匹配后另外装入处理器。组件的 ConfigureUiActions/ConfigureUiDataSources 接口仍用于启动前配置；回想继承同一代码契约，创建独立实例和状态。

其他桥接库或外部宿主依赖由其所有者通过 `hostCodeFingerprint` 或组件的 `ConfigureHostCodeFingerprint` 显式贡献。插件不猜测任意 C# 调用图，也不扫描其他 DLL。工厂捕获的可变状态不能替代作者声明的参数和中立恢复数据。新的正式构建使用新的最终指纹，既有纯内容存档不提供兼容读取或迁移。

定向契约 `CodeFingerprint-TargetedContracts.txt` 当前 30 项通过，包含相同声明替换实现、原生快照匹配/拒绝原子性、UI 注册冻结、宿主变化、工厂替换、常规存读档/回想/声音/预览。正式编译和十程序集见 `CodeFingerprint-OwnedFormalCompile.txt`。这些是逻辑与公开元数据契约，没有运行 Windows 成品实机验收。
