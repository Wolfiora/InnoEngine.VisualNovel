# 正式宿主能力缺口与插件边界

检查对象是项目显式选定的现有构建，以及该构建为 Integration 生成的正式 Runtime 引用。兄弟引擎工作区正在变化，本记录不把其源码中存在的类型当成已导出的脚本能力。

重现：`Tools/Inspect-VisualNovelCapabilities.ps1`。证据：`artifacts/CapabilityReport.txt`。工具只读取正式 API 程序集的 PE 元数据，不通过反射调用内部实现。

| 所需能力 | 当前正式契约 | 对应影响与最小需求 |
|---|---|---|
| 根序列化操作 | Runtime 无 SerializationRegistry；Reader/Writer 无公开根构造 | 原生存档与全局进度的逻辑已有 codec 端口及 Editor 实现，生产 Runtime 接入尚缺。需要实例所有的原生编码/解码服务，不能改为 JSON 或手写二进制 |
| 扩展类型发现 | Runtime 无 TypeCache/TypeRegistry | 自动发现命令与数据源不能按计划接入。需要按基类/接口与稳定元数据查询的公开契约；可以先完成明确注册的实例扩展逻辑 |
| 输入法组合态 | RenderOutputInput/InputSnapshot 只有提交文本，未导出 composition 状态 | UI 域能测试组合期间禁止快捷键，但 Integration 无法从宿主获取组合态。需要视口范围的组合开始/更新/结束状态 |
| 手柄 | 上述视口输入无手柄按钮与导航数据 | 键盘焦点导航已经实施，不能宣称手柄完成。需要公开的手柄动作/导航与消费语义 |
| 无效创作草稿保存 | EditorAssets.Save 在成功导入后才提交源；AssetSourceStore 未导出至脚本 | 文档能保留和热重载 Missing/无效内容，但导入失败的源不能独立落盘。需要源草稿事务入口，仍由引擎处理身份、冲突与提交 |
| File Browser 资产拖放桥 | EditorDragData、EditorDrop 和 EditorInteraction 已导出；EditorDragDropRenderer/EditorDropWidgetResult 未导出 | 通用自有 UI 树拖放可使用公开 widgets 完成，不能据此接收 File Browser 的私有 native payload 协议。原生文件拖入图片/音频工作流需要公开桥；现有所选资源按钮和匹配工具可用 |
| 原生场景活动会话重载交接 | 正式 Runtime/Editor 裁剪引用都没有 ISceneReloadStateTransfer，IEditorReloadParticipant 也不在脚本面 | Scene 组件仍由原生生命周期销毁/重建；创作草稿由原生面板状态恢复，预览主动停止。不能宣称活动游戏无缝恢复；需要候选事务的中立会话交接或明确的重载生命周期契约 |
| 任意附加资产的候选只读检查 | 捕获属性会登记依赖，缺少不登记的候选只读入口 | 已知 VN 与图像/字体/音频/材质/Shader 采用官方类型化原生导出/读取完成指纹检查；未知第三方附加资源明确拒绝，不能重复登记、忽略或读私有封装 |
| 窗口显示控制 | 选定 Runtime 裁剪 API 没有 IPlatformWindow/IPlatformApplication、DisplayMode/WindowMode；当前宿主窗口契约也没有切换模式方法 | 不显示无效全屏开关。需要输出 owner 所有的显示能力查询和受控模式切换，不能从脚本直接调用原生后端 |
| 玩家输入系统剪贴板 | 选定 Runtime 裁剪 API 未导出 Clipboard/IClipboard/IApplicationClipboard；Editor 专用剪贴板不能作为 Runtime 依赖 | 单行输入已有字素选区/编辑/横向查看；系统复制粘贴仍缺宿主端口，不能用 OS P/Invoke 或 Editor 依赖补洞 |
| 编辑器预览画布提交文字 | 选定 EditorUiInput 的公开属性只有修饰键、指针/轮滚、时长、缩放与 wantsText，没有提交文字缓冲区 | 仅阻塞从画布直接取得 Unicode 提交。现已使用公开原生文本控件，经同一 UI/会话同步输入或选择器查询；需要受控的 Editor 提交文字/组合态入口才能统一直接输入 |

纹理回读类型在正式 Runtime 中存在；准确帧捕获与原生传输/PNG 链已实现并通过逻辑契约，GPU 回读尚未实机验收，不能列为“宿主完全没有 API”。

拖放/场景交接的裁剪 PE 证据为 `artifacts/Reports/Editor-FormalLifecycleAndDragSurface.txt` 和 `Runtime-FormalLifecycleAndDragSurface.txt`；重现使用 `Tools/Inspect-EditorCapabilities.ps1` 和原有 Runtime 检查脚本。通用树拖放不需要读取上述私有协议。

显示/剪贴板的选定正式裁剪 PE 证据为 `artifacts/Reports/Runtime-DisplayClipboardSurface.txt`；检查器只读取公开类型及操作元数据。兄弟源码的后续变化不能替代选定 SDK 的脚本契约。

编辑器预览输入的正式 PE 属性清点为 `artifacts/Reports/Editor-PreviewInputSurface.txt`；原生文字桥的域/会话/路线行为已纳入 237 项完整回归，真实控件操作未实机验收。

这些缺口只阻塞表中明确列出的宿主接入。时间线、配音、UI、资源库、本地化和其他独立功能仍须继续实施，不能用接口缺口将整个 P0～P8 标记完成或全部阻塞。用户已明确暂缓本批次实机验收；本记录也不要求用户现在停止使用电脑。
