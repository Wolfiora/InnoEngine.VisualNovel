# 开发流

## 准备

- 与本仓库并列 clone `Wolfiora/InnoEngine`（`code-format` 分支或后续指定分支），即 `../InnoEngine`。
- 使用 `global.json` 指定的 .NET SDK；Editor 与插件导出器只支持 Windows x64 和 macOS ARM64。
- 需要 PowerShell 7 运行 `Tools/` 下的脚本。

## 日常循环

1. 在 Editor 中打开要改的插件工程 `Plugins/<Name>/` 编写代码与资源。依赖插件需先构建并安装（第 3 步会自动放入该工程的 `Plugins/`）。
2. 新建的 `.imeta`、`.iasmdef`、`Settings.*.inno` 由 Editor 生成，与源码一起提交。
3. 构建插件：

   ```powershell
   ./Tools/Build-Plugins.ps1              # 按依赖顺序构建全部插件到 Artifacts/
   ./Tools/Build-Plugins.ps1 -Install     # 同时安装到 Sandbox/Plugins/
   ./Tools/Build-Plugins.ps1 -Only InnoPlugin.VisualNovel.Stage
   ```

4. 在 Editor 中打开 `Sandbox/`，用普通 Scene、Scene View、Game View 与 Play Mode 联调。

## 约定

- 新插件：在 `Plugins/` 下建工程目录（`Assets/Runtime`、`Assets/Editor`、`README.md`），在 Editor 中把 Project ID 设为 pluginId，然后登记到 `Tools/Plugins.psd1` 与架构文档。
- 程序集：在写入第一份代码时，由 Editor 在 `Assets/Runtime/` 与 `Assets/Editor/` 各建一个程序集定义，命名 `<Name>` 与 `<Name>.Editor`，跨插件引用只能指向 manifest 已声明的依赖。没有程序集定义时，引擎按插件默认程序集编译，`.editor.cs` 文件归入 Editor。
- 格式：遵守根目录 `.editorconfig` 与 InnoEngine `.agents/rulebook/csharp-style`。多参数函数每个参数单独一行并使用 `) {`，私有字段 `m_` 前缀，禁止 global using。
- 不额外引入校验脚本或 CI；验证以 Editor 中的脚本编译、Play Mode 行为与插件导出为准。
