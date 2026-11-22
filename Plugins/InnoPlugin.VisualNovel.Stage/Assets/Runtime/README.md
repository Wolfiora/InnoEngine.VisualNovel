# Runtime

`InnoPlugin.VisualNovel.Stage` 的运行时代码与资源。这里的代码同时进入游戏和编辑器编译，只能使用 Runtime 与 Authoring scope 的脚本 API。

- 命名空间以 `InnoPlugin.VisualNovel.Stage` 开头，按功能子目录继续细分。
- Authoring 标注（检查器、序列化提示等编译期注解）也写在这里；播放器编译会自动擦除。
- 不得引用本插件或其他插件的 Editor 程序集。
