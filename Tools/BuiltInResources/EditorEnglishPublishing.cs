using System.Collections.Generic;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal static class EditorEnglishPublishing
{
    internal static IReadOnlyList<EditorEnglishEntry> entries { get; } = new EditorEnglishEntry[]
    {
        new("literal", "VN 发布检查 · Publishing Checks", "VN Publishing Checks"),
        new("literal", "先保存全部文档，再在 File Browser 选择 .ivnproject。检查已导入内容，最终游戏由引擎 Export as Game 构建。", "Save all documents, then select an .ivnproject in File Browser. These checks inspect imported content; the engine's Export as Game builds the final game."),
        new("literal", "检查所选游戏 / Check selected game", "Check selected game"),
        new("literal", "请选择已导入的 .ivnproject。", "Select an imported .ivnproject asset."),
        new("literal", "打开引擎游戏导出", "Open engine game export"),
        new("literal", "当前宿主未提供可用的 export/game 动作。", "This host does not provide an available export/game action."),
        new("literal", " · 内容检查通过", " · Content checks passed"),
        new("literal", " · 需要修复内容", " · Content needs corrections"),
        new("literal", "宿主能力或启动配置仍待验证。打开导出窗口不代表这些项目已经完成。", "Host capabilities or startup configuration still require validation. Opening the export window does not complete these checks."),
        new("literal", "保存检查报告", "Save check report"),
        new("literal", "报告已写入：", "Report written: "),
        new("literal", "资源闭包：", "Resource dependency closure: "),
        new("literal", " 项。点击可定位资产；属性字节不是发行包体积。", " entries. Click to locate an asset. Property bytes do not represent package size."),
        new("literal", " 属性字节##", " property bytes##")
    };
}
