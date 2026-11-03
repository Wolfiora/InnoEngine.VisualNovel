using System;
using System.Linq;
using InnoEngine.Scene;
using Inno.VisualNovel.Authoring.ProjectEditing;
using Inno.VisualNovel.Authoring.SceneEditing;
using Inno.VisualNovel.Model.Project;
using Inno.VisualNovel.Model.Values;

namespace Inno.VisualNovel.Editor.Inspection;

internal sealed class NativeUiInspectorContext
{
    private NovelProjectAsset? m_project;
    private long m_revision = -1;
    private string m_chapters = string.Empty;
    internal VariableDefinition[] variables { get; private set; } = [];
    internal NovelProjectAsset? Refresh(GameObject owner)
    {
        var project = NovelUiBindingEditService.Project(owner);
        string chapters = project is null ? string.Empty : string.Join("/", project.chapters.Select(static chapter => chapter.identity.persistentId + ":" + chapter.contentVersion));
        long revision = project?.contentVersion ?? -1;
        if (!ReferenceEquals(project, m_project) || m_revision != revision || m_chapters != chapters)
        {
            var candidate = project is null ? [] : NovelProjectVariables.Read(project);
            variables = candidate; m_project = project; m_revision = revision; m_chapters = chapters;
        }
        return project;
    }
}
