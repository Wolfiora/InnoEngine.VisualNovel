using System.Collections.Generic;

namespace Inno.VisualNovel.Tools.BuiltInResources;

internal static class EditorEnglishLicenses
{
    internal static IReadOnlyList<EditorEnglishEntry> entries { get; } = new EditorEnglishEntry[]
    {
        new("literal", "VN 资源授权记录 · Resource Licenses", "VN Resource Licenses"),
        new("literal", "授权目录", "License catalog"),
        new("literal", "记录原生资产的作者、来源、许可、署名及修改。未完成记录可以保存；资源审计指出缺项。本目录属于创作数据，不能加入 Player 动态打包集合。", "Record each asset's author, source, license, attribution and modifications. Incomplete entries can be saved; resource audits identify missing information. This catalog is authoring data and cannot be added to the Player's explicit packaging collection."),
        new("literal", "记录所选资源", "Record selected resource"),
        new("literal", "在 File Browser 选择已导入资源。", "Select an imported resource in File Browser."),
        new("literal", "记录资源授权", "Record resource license"),
        new("literal", "查找资源 / 作者 / 许可", "Find resource / author / license"),
        new("literal", "上一页##license", "Previous page##license"),
        new("literal", "下一页##license", "Next page##license"),
        new("literal", "授权记录缺少资源身份。", "The license entry has no resource identity."),
        new("literal", "作者 / 权利人", "Author / rights holder"),
        new("literal", "来源链接 / 原创说明", "Source link / original-work description"),
        new("literal", "许可名称 / 标识", "License name / identifier"),
        new("literal", "我已确认本游戏的计划分发权限", "I have confirmed permission for this game's planned distribution"),
        new("literal", "要求署名 / 附带声明", "Attribution / notice required"),
        new("literal", "署名或许可正文", "Attribution or license text"),
        new("literal", "修改情况", "Modifications"),
        new("literal", "编辑资源授权记录", "Edit resource license entry"),
        new("literal", "移除记录（保留资源）##", "Remove entry (keep resource)##"),
        new("literal", "移除资源授权记录", "Remove resource license entry")
    };
}
