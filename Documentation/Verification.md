# 验证方式与边界

## 已选引擎产物

验证使用现有引擎构建的本地快照，源码保持只读。`artifacts/EngineBuildSnapshot` 取自已有 Debug Build，`artifacts/EngineEditorSnapshot` 取自已有 Editor 构建。早先 Release 产物缺少当前编辑器公开 API，已不作为当前验证依据。

## 重现

```powershell
./Tools/Validate-VisualNovel.ps1 -EngineRoot ../InnoEngine -BuildAssembly ./artifacts/EngineBuildSnapshot/InnoEngine.Build.dll -DotNet "$env:USERPROFILE/.dotnet/dotnet.exe"
./Tools/Validate-Distribution.ps1 -EngineRoot ../InnoEngine -BuildAssembly ./artifacts/EngineBuildSnapshot/InnoEngine.Build.dll -DotNet "$env:USERPROFILE/.dotnet/dotnet.exe"
./Tools/Probe-RuntimeSerialization.ps1 -EngineRoot ../InnoEngine -BuildAssembly ./artifacts/EngineBuildSnapshot/InnoEngine.Build.dll -DotNet "$env:USERPROFILE/.dotnet/dotnet.exe"
```

契约测试消费正式脚本编译的 DLL，不将业务源码复制进测试程序集。宿主测试通过公开 ModuleHost.Register 注册正式脚本程序集，再组合真实 TypeCatalog、SerializationRegistry、AssetPipeline 与 EditorInteractionRuntime；没有内部可见性或反射穿透。

新增程序集会改变依赖闭包，因此验证脚本对测试宿主执行 Rebuild，避免复用缺少新依赖的旧 deps.json。

分发检查使用正式 `PluginSourceService` 读取和验证原生清单，断言身份、十个程序集定义、零插件依赖与零覆盖声明，再在同一全新消费工程中走正式脚本编译。每次验收目录唯一，旧工程不会作为首次安装结果重复使用。每次源码更新后的新包仍须重新检查，不能沿用旧包结果。

## 架构自查

| 维度 | 证据与结论 |
|---|---|
| 分层与组织 | 十个有实际实现的程序集；PE 引用检查通过；Runtime 不引用创作或编辑器层 |
| 基础能力复用 | 使用引擎原生序列化、GraphDocument、资产导入、文档、History 和模块发现 |
| 状态与所有权 | 运行程序复制、变量防御复制、快照候选验证；文档以稳定 ID 关联中立字节；正常退出已观察 |
| 可扩展性 | 指令和端口具有稳定语义 ID；显式会话命令注册、类型化参数、恢复/取消与副作用政策已有测试；自动发现和其他扩展种类仍未齐 |
| UI 一致性 | 使用引擎 PropertyRow、BoundedCombo 和通用图画布；全套布局、缩放和大字号矩阵尚未完成 |
| 交互与撤销 | 原生编辑器实测 dirty/Undo；宿主契约验证 Edit/Undo/Redo/Save；完整拖放和批量事务矩阵未完成 |
| 持久化 | 原生会话/舞台/音频包及候选恢复、槽位隔离与替身写入失败测试通过；正式 Runtime 根序列化入口仍缺失，真实盘故障及发布游戏存档未验收 |
| 性能与规模 | 未执行设计中的规模、帧率与耐久基准 |

UI 测试生成的故事源已移至 `artifacts/UIAcceptance/Assets` 保存证据，不混入正式插件资源。正式教学源位于 `Assets/~Samples/FirstStory`。

后续 UI 验收使用只安装 `.iplugin` 的 `artifacts/AcceptanceProjects/CleanConsumer`。插件在真实 Direct3D11 编辑器中自动编译激活，创建故事、创建/撤销变量、查看引用、编辑递归条件并保存。此工程保留原生源与日志。分发验证从此使用每次唯一的新消费目录，避免重用这个已经产生用户内容的验收工程。

当前用户要求不使用 Computer Use，不启动可见应用。本批次仅运行命令行正式编译、原生资产导入和逻辑测试；先前的局部 GUI 证据不覆盖新增设计器与渲染功能。没有执行新的画面、音频设备、手柄、性能或成品实机验收。

最近完整基线为 `artifacts/Reports/Validation-104.txt`：104 项契约测试通过，十程序集依赖检查 0 错误。新增覆盖源断点/单步与并行暂停、发布闭包/样例禁入、PCM/float WAV 波形/裁剪及原生音频导入、源配音批次、长台词与键盘翻页。后续尚未通过的功能不会沿用这份基线。

离线 Shader 验证通过公开宿主 ShaderArtifactBuilder 和现有 shaderc 执行，不创建编辑器窗口或图形设备。当前 Windows D3D11 结果为精灵 1 通道、后处理 2 通道，证据 `artifacts/Reports/ShaderValidation-D3D11.txt`。重现命令：

```powershell
& "$env:USERPROFILE/.dotnet/dotnet.exe" artifacts/bootstrap/bin/Bootstrap/debug/Bootstrap.dll (Get-Location).Path --validate-shaders
```
