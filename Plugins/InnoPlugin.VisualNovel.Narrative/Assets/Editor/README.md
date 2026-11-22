# Editor

`InnoPlugin.VisualNovel.Narrative` 的编辑器扩展：EditorPanel、EditorModule、EditorViewportContributor、InspectionDrawer、AssetImporter 等。只进入编辑器编译，可以使用 Editor scope 的脚本 API。

- 在没有 Editor 程序集定义前，文件名使用 `.editor.cs` 后缀，引擎据此归入插件默认 Editor 程序集。
- 命名空间以 `InnoPlugin.VisualNovel.Narrative.Editor` 开头。
- Scene View / Game View 由宿主拥有，这里只提供 contributor、gizmo 和面板，不复制视口。
