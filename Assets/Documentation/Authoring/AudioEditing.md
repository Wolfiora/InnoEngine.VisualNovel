# 音频波形与区间裁剪

在「VN 音频提示」中打开 `.ivnaudio`，展开「波形与裁剪」，点击「读取波形」。这一步仅通过引擎的 `audio-data` Artifact 读取样本，不播放音频。

拖动波形选择时间区间，也可以输入起点和终点。区间采用左闭右开规则，边界对齐到最近的完整采样帧。点击「创建裁剪副本并关联此提示」后，插件在 `Assets/Game/Audio` 创建独立 WAV，使用引擎导入器导入，再通过原生 History 改变提示的引用。原始录音保持独立；撤销引用替换不会删除已创建的资源。

支持 PCM 8/16/24/32 位、IEEE float 32/64 位以及相应的 WAVEFORMATEXTENSIBLE。最多读取 64 MiB，最多显示 8192 个时间桶；每个桶保留所有声道的最大/最小值，避免短促峰值被平均掉。压缩音频可继续走引擎播放链，但波形编辑需要先转换为支持的 WAV。

格式按微软 [WAVEFORMATEX](https://learn.microsoft.com/en-us/windows/win32/api/mmreg/ns-mmreg-waveformatex) 与 [WAVEFORMATEXTENSIBLE](https://learn.microsoft.com/en-us/windows/win32/api/mmreg/ns-mmreg-waveformatextensible) 的采样帧、对齐和子格式规则处理。工具不会把音频字节作为剧情状态序列化。

已通过格式、帧边界、异常数据、裁剪结果和引擎 WAV 原生导入逻辑测试。本批次没有播放音频或验收听感。
