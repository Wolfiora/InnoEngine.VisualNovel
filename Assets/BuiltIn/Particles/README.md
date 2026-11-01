# 内置粒子预设

这八个预设使用原创透明精灵。所有参数都是可编辑的原生 `.ivneffect` 数据，不需要用户脚本。先将预设关联到游戏定义的粒子资源库，再用「舞台」指令的 StartParticles 指定资源 ID、实例名、位置和非零固定种子。

| 预设 | 资源 ID | 默认发射与生命期 | 精灵与目标 |
|---|---|---|---|
| 细雨 | builtin.particles.rain | 220/秒，1.2～1.8 秒 | 细长雨丝；前景，1920×1080 区域 |
| 落雪 | builtin.particles.snow | 45/秒，6～10 秒 | 六瓣雪花；前景，全屏缓落 |
| 浮尘 | builtin.particles.dust | 20/秒，6～12 秒 | 柔边小圆点；前景，低速飘动 |
| 花瓣 | builtin.particles.petals | 18/秒，5～9 秒 | 浅色花瓣；前景，顶部 300 像素区域 |
| 萤火 | builtin.particles.fireflies | 12/秒，4～7 秒 | 柔光圆点；前景，缓慢向上 |
| 柔光点 | builtin.particles.lightpoints | 16/秒，3～6 秒 | 紧凑柔光点；前景，缓慢向上 |
| 气泡 | builtin.particles.bubbles | 15/秒，4～7 秒 | 透明圆环及高光；前景，向上并放大 |
| 纸屑 | builtin.particles.confetti | 发射 0.7 秒，180/秒，2.5～4.5 秒 | 小矩形；前景，有重力和旋转 |

在 `VN 粒子设计` 打开预设，通过逻辑时间与固定种子预览。安装包内容只读；修改需先创建项目中的资源副本，当前通用复制界面仍待补齐。

StopParticles 只停止出生，已有粒子继续消退；RemoveParticles 立即移除；WaitParticles 等待有限发射结束或已停止的粒子全部消退。持续发射的粒子必须先停止或取消，等待才会完成。减少动态效果会暂停并隐藏装饰性粒子；essential 为真的效果继续运行。

精灵 PNG 可直接查看透明图像，完整演出预览使用游戏的 Stage 与 Rendering 链。本批次只验证原生导入、参数、求值与快照逻辑，没有对这些预设做实机画面验收。
