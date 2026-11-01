# 编辑器语言归属

编辑器语言与游戏语言分开。当前增加的 `EditorTextCatalog` 复用已有 `.ivnlocale`、`NovelLocalizationService`、原生源摘要和翻译审核状态；不增加 JSON、gettext 存储、全局文化切换或第二个资产索引。

每个原生编辑器项目代际拥有一个 `NovelEditorLanguageModule`。候选表先完整验证来源、文本身份、复合格式及 ImGui 身份后缀，成功后再替换视图；失败保留旧视图。源表随后改变不会修改已安装的视图。模块退休释放对旧资源的引用，没有静态 Current 或跨项目缓存。

显式标签使用 `Text`，格式文本先翻译模板，再由 `Format` 插入值。玩家姓名、资源名、节点 ID、文档内容和其他传入值不自动翻译。字面花括号先按既有文本规则转义，不能把帮助文本中的 `{变量标识}` 当成实际剧情求值。

显示文本用 `Text`/`Format`；原生交互控件用 `Label`/`FormatLabel`。后者在两种语言中都使用同一个 `###` 身份，已有显式 `###` 则直接保留。仅保留 `##` 并不足以使可见标签退出 ID 哈希；这里遵循 [Dear ImGui 官方 ID 规则](https://github.com/ocornut/imgui/blob/master/docs/FAQ.md#q-how-can-i-make-a-label-dynamic)。动态 owner 字段和源格式值保留在身份中，不按译文重建。持久节点、文档与 History 身份不受显示语言影响。通用属性字段已接入当前项目 owner；选项中的用户内容仍按原值显示。其他绘制入口会在资源补齐后逐一接入同一边界。实际 GUI 焦点尚未验收。

资源准备工具复用 SDK Roslyn 解析实际 C# 字面文本、插值格式、节点元数据、内置资源目录和教程说明，并使用生产 `EditorTextCatalog.Key` 生成相同文本身份。英文文案按功能组放在 `Tools/BuiltInResources/EditorEnglish*.cs`，检查缺项、重复、失效项、占位符和控件后缀。`-RefreshEditorText` 只有在清单全部配齐且没有失效项时，才经原生 Assets 写入并重读 `Assets/BuiltIn/Editor/EditorText.ivnlocale`。当前 2,247 对已审核和原生重读，见 `TranslationReview-NativeResources.txt`；没有第二套存储或部分资源发布回退。

语言偏好位于引擎 `Editor/Visual Novel/Language`，使用原生 `EditorSettingObject`、Apply、Core 事件订阅与 History。模块通过引擎构造注入取得 Settings；候选激活期间不能用 `TryGetModule` 查询尚未发布的目录。模块退休释放订阅，重新启动读取原生项目偏好。无效语言或资源保留最后可用目录，并在工作区显示错误。游戏语言和玩家偏好不改变。

主要面板正文、按钮、提示和明确声明的选择项已接入；节点图和快捷菜单在显示处翻译名称/类别/端口，搜索同时支持源名称与译名。切换语言会使图显示缓存失效，节点和连线保持不变。带用户值的组合标签分别构造原始身份与翻译显示；颜色控件、剧本节点和通用选择项使用稳定 owner 后缀。界面源维护工具只处理声明的展示参数，不重写资产内容、默认台词或用户名称。

正式 API 清点 `Editor-LanguageChromeSurface.txt` 确认：原生 `EditorPanel` 没有动态标题属性，标题来自只读 `EditorPanelAttribute.title`。浮动窗口标题和宿主 Panel 菜单不能通过当前公开契约随语言实时替换；20 个 VN 面板已采用静态中英双语标题，保留原面板 ID 和菜单归属。这个边界不阻止插件面板正文、节点图和普通控件的语言接入。

作者操作可抛出 `AuthoringValidationException` 或 `AuthoringArgumentException`，继承普通异常族并保留 BCL `FormattableString` 的模板与独立参数。它们只属于当前调用和 owner，不进入持久状态。编辑器通过同一 `Error` 入口先翻译模板再填入用户值；其他异常原样显示，不能猜测用户名或已格式化消息的内容。参数名称、内层原因、数值格式、字面花括号和资源身份保持原值。预览准备、路线、资源副本和引用查找保留 owner 内的原始原因；同步准备继续抛出该原因。其他批量逐项错误、发布/导入的已扁平化文本和部分已缓存错误在切换语言后的重新展示仍需补齐。

编译器直接消费并返回原生 `GraphDiagnostic/GraphValidationResult`，删除了平行的 VN 诊断类型。原生图诊断不再丢弃连线身份。VN 自有规则的 BCL 模板与参数只作为该次编译的展示注释，`FormatDiagnostic` 每次提供独立参数副本；不会改变原始消息、源码位置、可执行程序或构建指纹。自定义节点降级合并时保留该注释。宿主和外部扩展的错误保持原文，未声明的消息不猜测翻译；空扩展错误转换成明确失败，不能让原生诊断构造再次抛出而逃过编译结果。工作区每帧按当前语言展示，诊断控件使用稳定 `###` 身份。

最近正式编译/十程序集见 `TranslationReview-FinalOwnedFormalCompile.txt`；2,247 对原生审核资源见 `TranslationReview-NativeResources.txt`。既有编译/卡片定向和 244 项完整回归通过，包含语言资源、控件身份、候选替换、独立 owner、实际作者错误，以及真正的 Settings Apply、Undo/Redo、错误候选恢复和项目重启。不能仅凭资源清单零缺项称为完整英文体验。没有进行 GUI 焦点、窗口、实际语言切换或英文上手验收。
