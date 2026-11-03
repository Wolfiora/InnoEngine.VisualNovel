# 依赖和所有权规则

权威边界是 `Assets/Code` 下的十个 `.iasmdef`。Model 定义声明和中立数据；Compiler 仅供创作态编译；Narrative、Stage、UI 分别求值剧情、演出和界面；Player 协调会话；Rendering 生成引擎绘制数据；Integration 适配原生 Scene 和宿主；Authoring 承担文档与编辑操作；Editor 只负责呈现和手势。

Runtime 不引用 Compiler、Authoring 或 Editor。Editor 和 Authoring 可以引用公开签名实际涉及的 Runtime 程序集，不能用传递引用或二进制路径绕过脚本 API。新增功能首先归入既有职责目录，不因一个控件或一个节点新增程序集。

引擎负责身份、GraphDocument、资产和 Artifact、Serialization、Editor 文档和 History。VN 的索引、剧本投影、布局缓存是按文档修订派生的数据，不能成为第二份可保存语义。运行时回滚日志是游戏检查点，不是编辑器撤销栈。

每个 Scene System、预览和回想拥有独立会话。GPU、音频、异步保存与事件注册由具体 owner 释放。没有静态 CurrentGame 或全局插件实例注册表；跨代传输只保留中立数据。原生 Scene → GameSystem 会话入口及 GameObject → Component 表现所有权见 [引擎复用](EngineReuse.md)。

正式检查由 `Tools/ArchitectureCheck` 检查源码归属与输出 PE 引用；它不会修改引擎规则。所有工具输出、测试工程和缓存放在插件仓库。引擎及其他功能插件是只读边界。

原生场景创作需要 Authoring → Integration 依赖：Authoring 通过正式 SceneEdits 创建 VN 组件并记录 History，Editor 仅发起操作。Integration 仍仅引用 Runtime 层，不反向依赖 Authoring。这是将原生对象作为内容权威后的明确边界调整，没有新增程序集。
