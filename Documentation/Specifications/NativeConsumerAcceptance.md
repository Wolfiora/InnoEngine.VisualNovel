# 原生场景候选包：消费工程记录

日期：2026-11-03。**全计划尚未完成；本报告是已交付增量的证据，不是 P0～P8 / R0～R7 的最终验收。**

## 当前启动修复包

用户实际启动日志暴露了此前无界面消费检查遗漏的 Inspector 注册失败：插件构造器请求宿主未提供的 `IInspectionPropertyEditService`，导致整个脚本候选被拒绝。11 个 Inspector 与工作区现已改为使用宿主实际提供的 `SceneEdits`，通过适配器复用原生属性 History。

- 当前包 SHA-256：`E196C0FA08614F6B5DC726AE030FF218C4AEF3A6BA5EDD5E42B61998D09C1064`，18,642,816 字节，2,481 个源文件。
- 两个消费工程的包已更新，未改场景身份、组件 ID 或游戏内容。备份见 `artifacts/ConsumerDeployment/20261103-125040-InspectorActivation/Manifest.csv`。
- 证据：`InspectorActivation-Before.txt` 在旧包程序集上复现注册失败；`InspectorActivation-FinalFormal.txt` 正式编译通过；`InspectorActivation-After.txt` 实际注册 14 个 VN Drawer，并通过原生 History/Prefab/保存重开定向用例。
- 本次只运行上述必要定向检查，没有重跑 260 项。以下旧包的消费结果不能充当本包的完整重新验收；实际 Editor 启动、画面与交互仍未由本轮操作桌面确认。

## 上一增量包的分发与验证（历史证据）

- 包：`artifacts/Distribution/NativeSceneCandidate/InnoEngine.VisualNovel.iplugin`，18,641,767 字节，官方 BuildPipeline 原子提交 2,479 个源文件。
- SHA-256：`8252A8C657F7B565B8EC7700A1C80D2FCED634A778BD2A96B1A08E84FD27A4C6`。
- 选定 SDK：`artifacts/EngineBuildSnapshot-20261031-0611`；引擎始终只读。
- 实际安装：`C:/Dev/GameEngineDev/InnoEngine.TestProject`、`C:/Dev/GameEngineDev/InnoEngine.Samples/NeonZero`。已复核两个安装文件与导出包的 SHA-256 一致。
- 备份：`artifacts/ConsumerDeployment/20261103-054624-NativeNavigation/Manifest.csv`，记录 10 个文件的源、目标、备份、前后指纹。本次每个项目只替换包、UI Workshop Scene/Prefab 及其元数据，保留原 Main、舞台示例、剧情、配音和美术。此前场景重建与布局的备份继续保留。

最新完整行为回归：`NativeUi-CollectionFullContracts.txt`，**260 通过、0 失败**。增量正式编译与候选激活：`NativeUi-CollectionAuthoringFormal.txt`；17 项相关检查：`NativeUi-CollectionAuthoringContracts.txt`；十程序集检查：`NativeUi-CollectionArchitecture.txt`，0 错误。完整回归有明确的宿主准备前提：通过公开导入器准备 62 个未就绪依赖及 Shader，未宣称自动冷导入已经通过。

## 实际消费工程检查

安装后各自正式编译、激活模块、重新扫描、实例化 SceneAsset。行为检查使用相应消费工程编译的程序集，没有用开发工程 DLL 代替安装包。

| 检查 | TestProject | NeonZero |
|---|---|---|
| 场景重开 | 3 Scene、每个 1 VN System、0 Missing | 同左 |
| UI Workshop | 24 个真实对象、17 个引擎网格 | 同左 |
| UI 交互 | 原生引擎命中、模态打开/关闭、明确组件引用通过 | 同左 |
| 舞台 Workshop | 19 对象、32 绘制项 | 21 对象、24 绘制项 |
| 舞台行为 | 角色/粒子启停、效果实例隔离、静音音频与时间线采样通过 | 同左 |
| 剧情路线 | 两条路线，各 2 条历史 | 两条路线，134 / 137 条历史 |

证据：`artifacts/Reports/Installed-TestProject-NavigationAudit.txt`、`Installed-NeonZero-NavigationAudit.txt`，以及相应的 `NavigationScenes.txt`、`NavigationBehavior.txt`。

`Assets/Game/Scenes/Main.iscene` 保留剧情入口；`NativeUiWorkshop.iscene` 展示真实控件和页面导航；`NativeStageWorkshop.iscene` 展示角色部位、镜头、效果、粒子和演出。舞台示例未绑定剧情，关闭音频自动播放；Performance Inspector 可以通过 Apply current position 采样指定位置。

## 本批实现与修复

原生 UI 继续由 Scene 对象拥有内容、由 IUiService 负责布局、字形和事件。已增加明确场景目标、多 Canvas 输入归属、模态遮挡、失焦与删除按下目标的释放保护、初始焦点/返回恢复、字体覆盖、Include UI 效果和输出转场网格、延迟捕获列表冻结。没有剧情的原生场景也能通过正式 RuntimeSession 更新执行 Show/Close/Replace 页面操作，渲染不派发动作。

默认字体按钮曾在实际消费检查中出现能命中却不点击的问题：Click 目标落入没有场景身份的排版片段。现让排版片段将输入交给真实控件。保留 `NativeUi-TextClickBefore.txt` 的失败证据和修复后的正式编译、原生后端回归及两个实际消费检查。

原生列表使用真实 Prefab 条目、稳定键和有限窗口。新增 First/Previous/Next/Last 导航按钮，目标缺失/停用及页边界自动禁用；部分末页、无效目标、集合选择 Undo/Redo、Prefab 目标重映射已测试。Inspector 的字段、动作、参数和集合选择复用已有类型化目录与引擎 History。

无窗口 UI 原生库 SHA-256：`1306FD3332707D1473EB69F2253D14FB0311FA82F26C34AFF273835EBADCA3F8`。这不是 GUI/GPU 或设备输入验收。

## Editor 布局

两个 editor.ini 已程序化设置并原样备份到 `artifacts/ConsumerWorkspaces`：左 Hierarchy、右 Inspector、中上 Scene/Game、中下 Visual Novel/File/Console。比例约左 15%、右 24%、中心 61%，中心上下约 60%/40%；停靠节点唯一、父引用存在、CentralNode 唯一的结构检查通过，默认打开 Main。

**未实际检查屏幕审美、系统缩放、文字截断和操作手感。** ini 结构正确不能证明界面已经漂亮或用户体验已经验收。

## 尚未完成的插件实施

1. Play 主游戏壳仍有 `NovelGameView` / `StageController` 路径；全部剧情演出尚未改为真实目标组件操作。路线测试只证明执行行为。
2. 全部默认页面、三套主题及全部模板/demo 尚未重建为原生 Scene/Prefab。Workshop 不代表完整游戏壳已经迁移。
3. 动态对象及完整原生 UI/焦点/演出状态的统一快照、存档、回滚与第三方恢复协议仍需整合。
4. 旧独立表现树及相关制作路径尚未全部删除。以上是实际实施剩余，**不能归咎于公开 API 限制**。

## 宿主阻塞与未验收

- 选定正式 SDK 缺少 Runtime 根序列化入口和原生隔离预演入口，没有使用反射或自建格式绕过。
- UI Change 事件缺少新值/读取接口；导入 TextureAsset 到 UI 图片输入的公开桥接、最终布局矩形查询仍有缺口。初始焦点和返回恢复已通过 autofocus 与事件接入，不再作为整体焦点阻塞。
- 最新 Windows 成品构建失败，见 `NativeNavigation-WindowsBuild.txt`：Player Support Pack 与 SDK 的序列化生成接口不匹配，缺少 `SerializationRevisionAttribute`、`SerializationCaptureKeyBuilder`。没有交付 Windows 可执行成品，也没有修改引擎或拼接二进制绕过。
- 没有 Computer Use、桌面软件启动或操作系统输入。GUI、GPU、音频设备、IME、手柄、实际拖拽/键盘、规模性能和两小时耐久未验收；没有要求用户在睡觉期间协助。
