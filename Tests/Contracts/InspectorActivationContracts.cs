using System;
using System.Collections.Generic;
using System.Linq;
using Inno.Adapter.Modules.DotNet;
using Inno.Adapter.Serialization.DotNet;
using Inno.Core.Serialization;
using Inno.Editor.Inspection;
using Inno.Editor.Interactions;
using Inno.Editor.Scene;
using Inno.Extensibility.Modules;
using Inno.Extensibility.Types;
using Inno.VisualNovel.Editor.Inspection;

namespace Inno.VisualNovel.Tests.Contracts;

internal static class InspectorActivationContracts
{
    internal static void Verify(
        EditorInteractions interactions,
        SceneEdits scenes
    ) {
        using var modules = new ModuleHost(new ModuleHostOptions { catalogSource = new DotNetAssemblyCatalogSource(typeof(InspectionDrawerRegistry).Assembly) });
        using var types = new TypeCatalog(modules, new ReflectionTypeCatalogSource());
        using var serialization = new SerializationRegistry(types, new ReflectionSerializationMetadataSource());
        var created = new HashSet<Type>();
        // These are real services also supplied by SceneInspectionModule. A property edit interface is deliberately absent.
        object[] dependencies = [interactions, scenes];
        using var registry = new InspectionDrawerRegistry(interactions, Construct, types, serialization);
        _ = modules.Register("vn.inspector-activation", [typeof(NovelCameraInspector).Assembly]);
        var expected = typeof(NovelCameraInspector).Assembly.GetExportedTypes()
            .Where(static type => !type.IsAbstract && typeof(IInspectionDrawer).IsAssignableFrom(type)).ToArray();
        if (expected.Length == 0 || expected.Any(type => !created.Contains(type)))
            throw new InvalidOperationException("Native Inspector registration did not construct every exported VN drawer.");
        Console.WriteLine("Native Inspector registry activated " + expected.Length + " VN drawers with actual SceneEdits and EditorInteractions, without a desktop.");
        var workspace = typeof(Inno.VisualNovel.Editor.Workspace.NovelWorkspacePanel).GetConstructors().Single();
        if (workspace.GetParameters().Any(static parameter => parameter.ParameterType == typeof(IInspectionPropertyEditService)))
            throw new InvalidOperationException("The VN workspace requests the Inspector's private property adapter from the editor composition root.");

        IInspectionDrawer Construct(Type type)
        {
            var constructor = type.GetConstructors().Single();
            object[] arguments = constructor.GetParameters().Select(parameter =>
            {
                var matches = dependencies.Where(parameter.ParameterType.IsInstanceOfType).ToArray();
                if (matches.Length != 1) throw new InvalidOperationException("Inspector dependency unavailable: " + type.FullName + " -> " + parameter.ParameterType.FullName);
                return matches[0];
            }).ToArray();
            var drawer = (IInspectionDrawer)constructor.Invoke(arguments);
            created.Add(type); return drawer;
        }
    }
}
