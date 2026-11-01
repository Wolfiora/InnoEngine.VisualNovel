# 截图采用独立传输目标

截图使用引擎公开的中立渲染图、纹理服务与异步回读，不引用任何具体原生后端程序集。源图像与待保存的剧情状态必须在同一个 owner 逻辑边界冻结；后续活跃画面不能替代它。

核对已选宿主和 [bgfx 的公开纹理与回读契约](https://bkaradzic.github.io/bgfx/bgfx.html) 后，确认读回用途不能直接视为一个普通可绘制附件。生产路径为：

`可绘制图像（ColorAttachment / CopySource） → 显式 Copy Pass → 回读传输纹理（CopyDestination / Readback） → 后续帧异步回读`

`NovelCaptureTargets` 显式声明两种用途；无设备测试使用已选宿主的公开 RenderGraph 校验资源依赖。该结论不等于已经完成 GPU 输出验收。

请求、取消、超时、传输纹理与迟到结果均有明确 owner。状态与图片成功后才合并到原生完整存档包；不存在截图成功而状态取自另一时刻的路径。标准 PNG 仅负责图片，不替代原生剧情持久化。
