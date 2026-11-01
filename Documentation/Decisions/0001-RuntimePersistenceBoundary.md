# 原生运行时存档入口

状态：已确认外部接口缺口；已实现并逻辑测试中立会话、原生存档数据包与原子宿主存储协议，但尚未接通发布游戏的 Runtime 根序列化入口。

## 证据

当前选定引擎构建将 `SerializationRegistry` 仅导出到 Editor scope。Runtime scope 虽然能够声明 `ISerializable`、生成转换器并在转换器回调中使用 reader/writer，但 reader/writer 构造函数不公开，不能据此开启根序列化操作。

`Tools/Probe-RuntimeSerialization.ps1` 在本仓库独立验收工程中走正式脚本引用裁剪链，确认 Runtime 引用 Registry 编译失败。日志：`artifacts/RuntimeSerializationProbe.txt`。编译器、Editor 导入器和宿主契约测试中的原生往返正常，不能用这些 Editor 能力冒充正式 Player 能力。

## 所需能力

引擎需要提供会话所有、可释放的 Runtime 原生序列化服务，或等价的受控存储 API，至少支持：

- 使用当前代际的转换器与稳定类型身份编码、解码闭合的中立状态契约。
- 不要求 VN 持有引擎内部类型、反射创建 reader/writer 或直接引用实现程序集。
- 可报告损坏数据与缺失扩展；失败不修改活动游戏状态。
- 服务在热重载与退出时明确失效，不能跨代保留旧转换器。

这里描述能力需求，没有定义或宣称存在新的引擎方法签名。VN 已提供 `INovelStateCodec`、`EditorNovelStateCodec`、`NovelSessionState` 和 `SaveService`：原生会话包、独立槽位、列表重建、候选切换以及舞台/音频/时间线恢复已有逻辑测试。端口接入后仍须验证真实盘、正式 Player、持久 UI 状态和完整扩展/回滚矩阵；Editor 适配不能充当 Runtime 适配。

## 当前决定

不修改引擎。不新增 JSON、手写二进制格式、旧格式 reader 或兼容 alias。继续独立的内容、编辑、演出与 UI 实现，对正式文件存档保持未完成状态。

## 相关创作保存缺口

`EditorAssets.Save` 当前要求先成功导入，再提交源。无效控制流草稿因此无法保存，虽然它仍保留在当前文档和中立重载转移中。所需能力是能够独立提交原生创作源并保留最后成功导入产物的公开资产事务；同时必须报告导入失败并保留文档历史。VN 尚未以直接覆盖源文件绕过这条事务。
