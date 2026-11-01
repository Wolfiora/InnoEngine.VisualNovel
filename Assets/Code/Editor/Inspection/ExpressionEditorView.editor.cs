using System.Linq;
using InnoEditor.ImGui;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Editor.Inspection;

internal sealed class ExpressionEditorView(StoryInspectorFields fields)
{
    private readonly NovelValueView m_values = new(fields);

    internal bool Draw(
        string id,
        ExpressionDocument expression,
        VariableDefinition[] variables,
        int depth = 0
    ) {
        if (depth >= 64)
        {
            EditorUiSession.current.widgets.Hint(fields.Localize("表达式达到 64 层上限，请简化条件。"));
            return false;
        }
        bool changed = false;
        ExpressionOperation operation = expression.operation;
        if (fields.SelectEnum(id + "/operation", "操作 / Operation", ref operation))
        {
            expression.operation = operation;
            int arity = ExpressionOperations.OperandCount(operation);
            expression.operands = Enumerable.Range(0, arity).Select(index => index < expression.operands.Length ? expression.operands[index] : new ExpressionDocument()).ToArray();
            changed = true;
        }
        if (operation == ExpressionOperation.Literal)
        {
            NovelValue value = expression.literal;
            if (m_values.Draw(id + "/literal", ref value)) { expression.literal = value; changed = true; }
        }
        else if (operation == ExpressionOperation.Variable)
        {
            string variable = expression.variableId;
            if (fields.Select(id + "/variable", fields.Localize("变量 / Variable"), ref variable, variables.Select(static item => (item.id, item.name + " · " + item.id))))
            { expression.variableId = variable; changed = true; }
        }
        else
            for (int index = 0; index < expression.operands.Length; index++)
                if (ImGui.CollapsingHeader(fields.Caption($"操作数 {index + 1}##{id}/{index}", fields.Format("操作数 {0}##{1}/{2}", index + 1, id, index)), ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.Indent();
                    try { changed |= Draw(id + "/" + index, expression.operands[index], variables, depth + 1); }
                    finally { ImGui.Unindent(); }
                }
        return changed;
    }
}
