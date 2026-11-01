# UI 行为参考与本插件边界

本轮参考了 Ren'Py 官方 [Screen Actions](https://www.renpy.org/doc/html/screen_actions.html) 和 [Screens and Python](https://www.renpy.org/doc/html/screen_python.html)：它将控件动作、值绑定、可用状态和选中状态作为可组合契约，支持自定义 Action。它的 [存档与回滚说明](https://www.renpy.org/doc/html/save_load_rollback.html) 也明确区分可回滚游戏状态与其他状态。

这些资料用于核对常见 VN 工作流，不是本插件的实现证据。InnoEngine.VisualNovel 的具体取舍如下：

- 页面状态使用明确的 Local/Game 生命周期，复用既有 typed value、变量存储和引擎原生序列化；不采用 Python 对象或 pickle。
- 控件动作保留稳定身份和中立参数；重复行以稳定键定位，焦点和一次输入消费由自有 UI owner 控制。
- 图片视口只管理临时交互几何；CG 元数据、解锁和本地化由 Player/Model 拥有，渲染只消费最终帧。
- UI 自定义动作及数据源已通过正式编译和逻辑回归，使用同步查询/动作、类型化参数/字段、稳定 key 行和受控原生绑定。实例所有权、预览副作用、原生状态和回滚契约均分别验证；自动发现及其他未齐功能另列，不能因参考引擎有相应能力就把本插件标为完成。

来源核对日期为 2026-11-01。未复制参考引擎的代码、美术或默认资源。
