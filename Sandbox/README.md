# Sandbox

本仓库唯一的开发宿主工程。它是普通 InnoEngine 项目，通过 `Plugins/` 安装本仓库构建出的全部 `.iplugin`，用来在 Editor 中联调 Scene View、Game View 与 Play Mode。

- `Plugins/*.iplugin` 由 `Tools/Build-Plugins.ps1 -Install` 生成，不提交。
- 场景与测试素材放在 `Assets/`，只服务于开发验证，不随任何插件发布。
