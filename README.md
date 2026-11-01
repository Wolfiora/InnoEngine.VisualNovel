# InnoEngine.VisualNovel

InnoEngine 的独立源码视觉小说插件。目标是节点图与剧本双主创作、自有舞台和游戏 UI、精细演出与完整发布流程。

本仓库正在按完整设计分阶段实施。实际可用功能、验证证据与外部接口缺口见 [实施状态](Documentation/ImplementationStatus.md)，未完成的功能不作为当前能力宣传。

当前已实施共享图/剧本、卡片/紧凑/阅读稿与可见区域布局、原生共享变量库与 23 类指令、C# 命令、单焦点交互式并行、分支变量写入与审阅式剧本换序、富文本与五种阅读布局、角色变体/批量差分/独立语音、舞台源区域/裁剪/枢轴/混合/遮罩、时间线与粒子生命周期曲线、转场和屏幕效果参数轨道、自有 UI 与组件复用、提示说明/层级编辑/画布缩放吸附对齐、UI 事件动画/页面退场/共享时间线轨道、按住快进/阅读快捷键设置、页面状态、CG 多图视口、严格内容/代码匹配存档、原生回滚预算与准确帧缩略图、多语言图片/字体链与静态缺字检查、收藏、音乐鉴赏及隔离回想。最近完整行为基线为 244 项通过，包含舞台批量编辑、阅读组合键、单行输入编辑、游戏选择器搜索/滚动条、大字号适配与限时选项显示。正式编译和十程序集检查通过，最新插件/干净消费复验是 240 检查点，不能代表后续源码分发。既有 D3D11 离线 Shader 证据有效。编辑器资源已原生生成/重读 2,247 对审核文本；余下展示路径继续核对。实际最新证据和缺口以实施状态为准。

内置资源含 3 主题/54 页面、7 组件、68 图标、6 背景、2 分层角色（各 2 姿势/6 表情）、20 动作、8 粒子、12 转场、10 屏幕效果、20 UI 音效和 3 段循环音频。五组原生教学图、三种项目起点、分类浏览、正式链预览和可编辑副本已接通；高级创作、完整上手体验与游戏发布仍未齐，Runtime 原生根序列化仍有公开 API 缺口，当前不是完整 v1。视频和流媒体留待未来引擎 backend；本批次没有 Computer Use、GUI 或实机验收。

操作文档：[上手](Assets/Documentation/Start/README.md)、[效果](Assets/Documentation/Authoring/ImageEffects.md)、[玩家数据](Assets/Documentation/Authoring/PlayerProfiles.md)、[调试](Assets/Documentation/Authoring/Debugging.md)、[曲线](Assets/Documentation/Authoring/Curves.md)、[音频裁剪](Assets/Documentation/Authoring/AudioEditing.md)、[配音](Assets/Documentation/Authoring/VoiceManagement.md)、[发布检查](Assets/Documentation/Publishing/Preflight.md)。
