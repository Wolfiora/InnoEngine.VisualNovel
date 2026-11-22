# InnoEngine.VisualNovel 工作规则

- 本仓库是 `InnoPlugin.VisualNovel.*` 插件集合，不是单个插件。每个插件是 `Plugins/<Name>/` 下的独立 Inno 源码工程，可单独导出。
- 只用纯插件形式实现：不修改 InnoEngine 源码，不依赖 Rendering2D、Canvas 或其他外部功能插件。引擎缺的通用能力记入 `Documentation/Architecture.md` 的引擎缺口，不在这里绕路实现。
- 一切挂在原生 Scene → GameObject → 组件 / GameSystem 上；Scene View、Game View、Inspector、History、Assets、Serialization 由宿主拥有，只通过正式扩展点接入。
- 依赖只能沿 `Documentation/Architecture.md` 的依赖图向下；新增插件或依赖时同步更新 `Tools/Plugins.psd1`、该插件 README 与架构文档。
- 代码格式与 C# 规范沿用 InnoEngine `code-format`：根目录 `.editorconfig` 与引擎 `.agents/rulebook/csharp-style`。源码注释与公开 XML 用英文，产品文档用中文。
- `.inno`、`.imeta`、`.iasmdef` 是引擎生成的二进制文件，只通过 Editor 或引擎工具产生，不手写。
- 未验证的内容不得写成已完成。
