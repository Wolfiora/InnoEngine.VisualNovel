# 成熟 VN 行为与创作流程参考

本记录把参考产品的已公开行为对应到具体决策；宣传、计划和代码实现分别判断。技术机制仍使用 InnoEngine 正式 API。

| 参考 | 对应实现决策 |
|---|---|
| [Ren'Py 翻译](https://www.renpy.org/doc/html/translation.html) | 分别处理对白与界面字符串；语言切换重排文字，不重复剧情副作用；原文变化要能定位并复核；语音、图片和字体属于语言资源 |
| [Ren'Py 鉴赏与回想](https://www.renpy.org/doc/html/rooms.html) | 解锁与入口分开；回想保持主游戏状态，结束返回调用处，回想禁止普通存档；回想需要明确初始变量和舞台，不猜测前置剧情 |
| [LetsGal 可视化 UI](https://docs.avg-engine.com/advanced/visual-ui-editor) | 系统页面也可编辑；稳定元素身份；只读内置资源通过项目副本修改；设计与真实交互预览分开 |
| [LetsGal 数据与动作](https://docs.avg-engine.com/advanced/visual-ui-data-and-actions) | 表单选择绑定字段、系统动作和列表条目；页面临时状态不混进剧情存档；列表、详情和播放器模板共享同一个数据源 |
| [Naninovel 自定义指令](https://naninovel.com/guide/custom-commands) | 已采用参数元数据、受控执行上下文、明确取消/完成与原生表单目录；继续状态采用中立数据，不保存 Task 栈。注册当前为会话显式注入，不将其自动发现机制假设为 InnoEngine 已导出能力 |
| [Ren'Py 存档与回滚](https://www.renpy.org/doc/html/save_load_rollback.html) | 交互边界保存完整逻辑状态；不要保存迭代器、活动任务、文件和设备对象。本插件保持显式状态，不照搬 Python 序列化机制 |
| [LetsGal 默认游戏壳](https://docs.avg-engine.com/advanced/default-shell) | 主题覆盖整套页面；存读档共用行为；鉴赏、语音历史、输入确认等入口必须具有真实运行结果 |

[VoidNovelEngine 主分支](https://github.com/VoidmatrixHeathcliff/VoidNovelEngine) 明确把当前代码指向 [dev](https://github.com/VoidmatrixHeathcliff/VoidNovelEngine/tree/dev)，因此 README 不当作具体机制已经完成的证据。节点与诊断体验需要结合开发分支代码和文档核对。

本插件已经采用稳定文本身份、原文摘要复核、统一 UI 文字解析、语言切换候选提交、原生文档 History、收藏条目/详情绑定、局部播放器状态和隔离回想。界面设计器也提供已有系统字段、游戏变量和动作选择器。字体回退、语言图片、完整主题与其余功能仍以功能矩阵的实际状态为准，不能因参考文档存在就标记完成。
