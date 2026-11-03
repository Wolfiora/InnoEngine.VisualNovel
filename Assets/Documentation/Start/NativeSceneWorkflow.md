# 原生场景制作

所有可创作的图片、角色部位、控件和发射器都应在 Hierarchy 中有真实对象。Scene View 用于制作画面，Game View 使用 Scene 上指定的 VN Camera。无需进入 Play 才能看到当前制作画面。

图片组件的 `Parallax` 控制游戏镜头平移响应：0 固定、0.5 远景、1 普通世界图层。粒子使用预设中的同名参数。编辑视角始终按实际对象变换显示，不会为了制造视差改写 Transform。

Page 的 `Initial Focus` 可直接选择同页按钮或开关；引擎负责 Tab/Enter 导航，返回页面时恢复此前有效焦点。Canvas 的 `Font` 与文本组件覆盖字体必须已在引擎 UI 模板声明；默认模板已经配置中文字形。自定义多字体模板使用 `.vn-font-N` 样式类，详见开发文档；当前尚无可靠的自动模板生成入口。

## 从场景开始

1. 新建 Scene，在 Scene 的 System 列表中添加 `NovelSceneSystem`。
2. 选中 System，在 Inspector 的 **Add scene content** 中点击 **Create background image**。新对象会自动选中。
3. 为图片组件指定已导入的 Texture；没有图片时也可用 Color 制作纯色背景。在 Scene View 移动对象，或修改原生 Transform。
4. 回到 System，展开 **Camera and performance**，点击 **Create and use camera**。新镜头会直接绑定到该 System 的输出。
5. 调整镜头 Transform 或 Zoom。Scene View 的导航视角不受游戏镜头控制。

尚未指定 VN Project 时仍可制作画面；剧情运行需要另外绑定游戏定义。不要为使画面出现而创建 holder 或手写更新脚本。

## 添加角色

点击 **Create character**，在新角色的 Inspector 指定 Character 资源，然后点击 **Create missing parts**。每个部位都是可选择的真实子对象。

选择姿势和表情，再点击 **Apply appearance**。该操作使用原生 History，可撤销；普通画面刷新不会重新创建被删除的部位，也不会每帧覆盖手动修改的图片或 Transform。

将角色根对象保存为引擎 Prefab，可以实例化多个独立角色。修改一个实例不会修改资源或另一个实例。需要恢复误删的部位时，再显式点击 **Create missing parts**。

## 镜头、效果与声音

- **VN Camera**：指定 Follow Target 和 Following 后，运行更新跟随真实目标。Follow Offset 使用世界坐标；在 Edit 中点击 **Apply follow position** 才会应用位置，支持原生 Undo。跟随与位置轨道不能同时控制同一个镜头。
- **Shake Offset**：镜头局部震动通道，独立于 Transform，允许与跟随一起使用。
- **Create screen effect**：指定 Screen 类型的效果预设。**Override preset parameters** 创建对象独立参数，**Use preset values** 恢复继承。切换到不同机制的预设后应清除旧覆盖。
- **Create particles**：指定粒子预设，通过 Elapsed 检查某一时间的粒子；普通编辑画面不会推进时间。隐藏或停用发射器会移除它的绘制内容。
- **Create audio object**：指定 Audio Cue，调整 Gain、Paused 和 Play On Enable。编辑场景和绘制 Inspector 不会播放声音；需要安静的展示场景时关闭 Play On Enable。

## 可复用演出

点击 **Create performance**，指定 Clip，并将每个目标槽绑定到场景里的真实对象。设置 Elapsed，点击 **Apply current position**，可用一个原生属性事务应用并撤销该时间点。

当前原生通道包括位置、缩放、旋转、图片或 UI 透明度、音频增益、镜头震动及对应机制的屏幕效果参数。无效目标和冲突通道会在修改前报错。停止轨道后，其他原生脚本可直接修改组件；下一次画面采集读取修改后的值。

## 制作 UI

点击 **Create UI canvas**，继续在 Canvas、Page 和 Container 的 Inspector 添加页面、文字、按钮、开关、容器或列表。布局、文字与输入由引擎 UI 服务处理；对象仍由 Scene 管理。具体步骤见 [原生 UI 制作](../Interface/NativeSceneUI.md)。

## 当前边界

原生制作链已具备以上功能，但完整 Play 游戏壳、全部剧情表现目标、全部主题页面以及动态对象存档/回滚仍在迁移。不要把制作态示例当作已经全部迁移的完整游戏。本文功能有无界面行为检查，尚未完成实际桌面、GPU、输入设备和使用体验验收。
