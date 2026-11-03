# 在 Scene 中制作 UI

当前原生 UI 路径使用引擎 UI 服务。此说明覆盖已实现的制作态组件与列表，不表示完整 Play 游戏壳已经迁移。

## 创建与布局

1. 选中 Scene 的 `Visual Novel Scene` System，在 Inspector 点击 `Create UI canvas`。
2. 选中新 Canvas，添加 Page；在 Canvas、Page 或 Layout Container 的 Inspector 中添加文字、按钮、开关、横向/纵向布局或 Grid。
3. 在 Hierarchy 选中子对象，通过原生 Inspector 编辑组件与 Transform。自由布局使用 Transform；自动布局中 Transform 是容器分配位置上的作者偏移，子对象顺序控制排列顺序。
4. 添加 `NovelUiScrollComponent` 后，引擎处理滚轮、裁切与滚动区域。删除或停用真实对象会影响下一次画面和输入。

默认 Canvas 已指定引擎 UI 模板和中日韩字体，普通创作无需编辑 RML。按钮和开关的 Normal 外观来自 Panel，Hover/Pressed/Focused/Disabled 外观来自 Control Style。开关未绑定时保存到自身组件，绑定 Boolean 字段时只写数据源；拒绝写入不会修改作者的默认值。

静态文字可启用富文本，沿用 VN 的字体样式、颜色、Ruby 和字素揭示规则。台词绑定传递完整文本及揭示边界，未揭示部分保持布局位置。

## 页面按钮与运行输入

在 Page 或 Container Inspector 点击 **Add page navigation button**，然后在按钮的 **VN Page Navigation** 中选择真实的目标 Page：

- `Show`：显示目标，保留当前页。目标 Page 勾选 `Modal` 后阻止底层输入。
- `Close`：隐藏目标；页面对象和字段值继续保留。
- `Replace`：关闭按钮所在的最近一层 Page，再显示目标。目标不能位于即将关闭的页面内部。

目标页应放在保持激活的 Canvas 下。删除目标后会报告 Missing，不按名称绑定其他页面。这个流程不需要剧情资产或脚本；Play 时由 System 处理，Edit 模式只显示作者配置的激活状态。

多个 Canvas 按 Order 和原生顺序决定前后；只有一个 Canvas 接收一次操作。点击不会同时打开页面并推进下一句。按下后停用对象、切换模态或失去窗口焦点时，释放事件不会误触刚露出的控件。页面返回后的焦点由引擎文档事件恢复。

普通 **VN Story Action** 用于已配置游戏的推进、选项、存读档等动作；原生页面导航和剧情动作不能同时绑定在同一按钮上。游戏壳原生页面整体迁移尚未完成。

**VN UI Bindings** 提供可搜索的显示文本、显隐条件、交互条件与 Boolean 写回选择器；**VN Story Action** 提供系统/扩展动作及参数字段选择器。列表可按名称或稳定 ID 搜索。变量与扩展字段来自同一场景 System 引用的游戏，选择操作进入原生 Undo/Redo。高级自定义路径放在 `Advanced`，无需为普通对白或开关手输字段名。

## 列表、选项与原生 Prefab

`Add collection list` 创建真实的纵向布局、滚动组件和 List。创建一个按钮或条目组，将其通过引擎保存为原生 Prefab，再在 List 的 `Item Prefab` 中选择它。

- `Collection` 选择类型化集合，例如 `choices`。
- 条目文字可绑定 `item.text`，选项动作参数可绑定 `item.id`，交互条件可绑定 `item.enabled`；字段须以所选数据源实际提供的声明为准。
- `First` 与 `Capacity` 控制当前窗口。每个条目都有真实 GameObject、Component 和 Prefab 身份；相同条目 ID 在重排时复用同一实例。
- 条目在明确的会话更新中生成，编辑态的渲染和对象选择不会创建或删除条目。
- 自定义玩法可以调用 `NovelUiListComponent.Refresh(UiDataSource)`；`Clear()` 只删除该 List 拥有的条目，不删除作者手工添加的装饰。
- 每个窗口最多 512 条，场景最多 4096 个生成条目。重复或空 ID 报错，保留上一次有效列表。

List Inspector 的 **Choose collection** 可搜索内置集合和游戏声明的扩展集合，选择操作进入原生 History。需要翻页时，在父 Page 上点击 **Add list navigation button**，把 `Target` 设为这个 List，并选择 `First / Previous / Next / Last`。页大小使用 List 的 `Capacity`；按钮在边界、目标停用或缺失时自动禁用。不要把翻页按钮放进会被替换的条目 Prefab。列表首次运行刷新前总数尚未确定，编辑态不会为了显示页码而执行剧情或创建条目。

当前正式游戏壳仍使用旧页面路径，原生列表还不能当作所有默认页面已经完成替换的证明。生成条目的完整存档/回滚整合尚未完成。

## 演出与检查

时间线可以绑定真实 UI 对象，修改其 Transform 和 Layout 的 Opacity。一个对象同时含舞台 Image 与 UI Layout 时，Opacity 的目标不明确，会拒绝整次采样；应拆成两个子对象表达不同职责。

未配置资源或绑定错误时，诊断会说明对象或字段；不要通过复制另一份隐藏的 UI 树绕开错误。

## 当前限制

选定 SDK 的 UI Change 事件不携带变更值，也没有读取当前控件值的公开方法，因此滑块与输入框写回尚不能接入。导入 TextureAsset 到引擎 UI RGBA 输入的公开桥接、最终矩形查询及原生隔离 Preview 也仍有缺口。不要把这些能力的属性或类型存在当成已经完成。

页面初始焦点与返回恢复已通过引擎文档的 autofocus 和焦点事件接入：在 Page 的 `Initial Focus` 选择页面内的按钮或开关即可；文字和样式更新不重置当前焦点。跨页面、删除、停用和引用错误有明确检查。

当前验证包括无窗口真实 UI 后端、原生 Scene/Prefab/History 与行为测试；没有 GUI/GPU、真实输入设备或手感验收。
