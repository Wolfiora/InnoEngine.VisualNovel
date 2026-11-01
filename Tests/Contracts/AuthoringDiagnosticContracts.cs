using System;
using System.Linq;
using Inno.Core.Serialization;
using Inno.VisualNovel.Authoring.Diagnostics;
using Inno.VisualNovel.Authoring.LocalizationEditing;
using Inno.VisualNovel.Authoring.Variables;
using Inno.VisualNovel.Editor.Localization;
using Inno.VisualNovel.Model.Localization;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class AuthoringDiagnosticContracts
{
    internal static void NativeResourcesActualErrorsAndLiteralValues(SerializationRegistry native)
    {
        var table = new LocalizationTableAsset { sourceLanguage = "zh-CN", languages = [new() { id = "zh-CN", name = "简体中文" }, new() { id = "en", name = "English" }] };
        Add("共享变量不存在：{0}", "The shared variable does not exist: {0}");
        Add("参数错误：{0} · {1:D4} · {{literal}}", "Invalid argument: {0} · {1:D4} · {{literal}}");
        var english = new EditorTextCatalog(table, "en"); var chinese = new EditorTextCatalog(null);
        AuthoringValidationException? captured = null;
        string name = "标题{0}##原始名称";
        try { VariableTableEditService.Remove(new VariableTableAsset(), name); }
        catch (AuthoringValidationException error) { captured = error; }
        Check(captured is not null && captured is InvalidOperationException && captured.source == "共享变量不存在：{0}"
            && english.Error(captured) == "The shared variable does not exist: " + name && chinese.Error(captured) == "共享变量不存在：" + name,
            "An actual authoring failure merged user values into its resource identity or translated them as labels.");
        var argument = new AuthoringArgumentException($"参数错误：{name} · {12:D4} · {{literal}}", "source", captured);
        Check(argument is ArgumentException && argument.ParamName == "source" && argument.InnerException == captured
            && english.Error(argument) == "Invalid argument: " + name + " · 0012 · {literal}", "Localized argument errors lost their normal exception family, cause, format or literal braces.");
        var row = native.Deserialize<LocalizedText>(native.Serialize(table.entries[1]));
        Check(row.source == argument.source && row.translations.Single().text.Contains("{1:D4}", StringComparison.Ordinal), "Native language resources lost composite specifications or escaped literal braces.");
        var host = new InvalidOperationException(name);
        Check(english.Error(host) == name, "An undeclared host message was guessed or treated as authored localization metadata.");
        table.entries[0].translations[0].text = "Mutated";
        Check(english.Error(captured!) == "The shared variable does not exist: " + name, "A caught message retained mutable translation rows instead of an owner-local catalog.");
        void Add(
            string source,
            string translated
        ) {
            string id = EditorTextCatalog.Key(source, true);
            table.entries = [.. table.entries, new() { id = id, source = source }];
            LocalizationEditService.SetTranslation(table, id, "en", translated, TranslationStatus.Reviewed);
        }
    }
    private static void Check(
        bool condition,
        string message
    ) { if (!condition) throw new InvalidOperationException(message); }
}
