# InnoPlugin.VisualNovel.Narrative

通用叙事子插件：剧情图、编译与执行。与画面无关，可以被 RPG 对话、任务对白等非 VN 游戏单独复用。

| 项 | 值 |
| --- | --- |
| pluginId | `innoplugin.visualnovel.narrative` |
| displayName | `InnoPlugin.VisualNovel.Narrative` |
| 依赖 | 无 |

## 负责

- 基于引擎 Graph 的叙事图文档：单一 GraphDocument 是控制流权威，剧本文本视图与图视图共享同一份数据、身份与 History。
- 剧本语法到叙事图的编译、诊断与源码定位。
- 无画面的执行器：节点推进、分支、变量、条件、等待与外部指令回调（指令由使用方解释）。
- 编辑器：叙事图编辑器、剧本资产的 AssetImporter 与 InspectionDrawer。

## 不负责

- 立绘、背景、对话框等任何呈现；只通过指令回调把意图交给使用方。
- 自建序列化、事件总线或撤销栈：复用引擎 Serialization、Events、Identity、Editor History。

## 目录

```text
Assets/
├── Runtime/   叙事图模型、编译结果、执行器
└── Editor/    图编辑器、剧本导入与检查器
```
