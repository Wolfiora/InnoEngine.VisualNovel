# 验证工具的所有权

正式编译绑定调用者明确选择的现有 `InnoEngine.Build.dll` 所在目录。生产代码继续由官方 ScriptCompiler 的正式裁剪 API、别名映射与访问检查编译；工具引用宿主程序集，不进入 `.iplugin` 或 Player。

`Tools/Bootstrap/HeadlessProjectValidation.cs` 使用公开 EngineHost、AssetPipeline、PluginEnvironment、ScriptCompiler 和代际事务准备创作元数据，再使用官方编译结果。发布包调用公开 BuildPipeline。工具不实现自己的编译器、序列化器、资产库或插件安装器。

没有经过引擎 CLI 的 PrepareNativeAsync 和自动缓存维护入口。原 CLI 的这两个步骤可能准备源码工具或维护兄弟 checkout 下的缓存，超出本仓库的写入边界。现有 Shader 工具从本仓库 artifacts 的显式快照读取；没有源码 provisioner，也没有自动寻找旁边引擎源码的步骤。

开发期间新增必需原生字段时，旧的受控内置源会使候选资产激活被正式引擎拒绝。`--candidate-scripts` 只导出官方编译器的候选 DLL 到显式候选目录，用来运行本仓库原生资源生成器，重新生成当前模型的受控源。它不激活失败候选、不提供旧格式 reader、不写私有封装，也不能算正式验收成功；资源更新后仍须重新执行完整 `--scripts` 代际事务和行为验证。上一份有效包继续保留在独立目录。

项目、Library、脚本输出、临时目录、原生插件包和干净消费工程均限定在本仓库。工具会验证项目与输出路径。没有 GUI、窗口、实际 GPU 或音频设备调用。

此工具的成功表示正式脚本与插件分发逻辑通过，不能证明 Windows 成品或设备行为。公开 API 的缺口仍按决策记录处理。

无界面游戏构建使用显式复制到 `artifacts/SupportPacks` 的既有 Windows Support Pack；BuildPipeline 不配置源码 provisioner。准备 Scene 时使用公开 SceneWorld、GameScene、SceneAsset.Capture 和官方场景导入器；游戏入口仍是原生 GameObject 上的 NovelPlayerComponent。构建操作不会加载该场景、启动 Player、创建窗口或打开设备。

场景导入程序集没有可供工具引用的公开标记类型，验证宿主通过标准 AssemblyLoadContext 从选定 SDK 路径显式装载该程序集，并交由官方 DotNetAssemblyCatalogSource 发现。工具没有扫描目录或调用内部类型/方法。这仅用于 SDK 宿主的元数据组成，不进入生产脚本。

Windows 无界面构建的最新探查仍未取得成品。基线 SDK 能完成当前正式 API 编译，但现有 Support Pack 的 Serialization DLL 缺少该 SDK 生成代码依赖的 CaptureKeyBuilder/RevisionAttribute。尝试另一份已存在的 SourceDebug SDK 时，独立临时验证宿主可以编译，但其正式脚本 API 缺少 EditorEditGesture、GraphCanvasPointerController、EditorUiSession 等当前已使用的公开类型。证据为 `WindowsBuild-ImportContext.txt`、`WindowsPublication-SelectedSdkBuild.txt` 和 `WindowsPublication-SelectedSdk.txt`。这属于现有构建配套不一致，不能归为插件逻辑通过，也不能通过替换单个 DLL、添加旧格式 alias 或修改引擎规避。没有运行该验收场景或 Player。
