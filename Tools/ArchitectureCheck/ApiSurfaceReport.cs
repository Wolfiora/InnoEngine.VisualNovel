using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Inno.VisualNovel.Tools.ArchitectureCheck;

internal static class ApiSurfaceReport
{
    internal static void Write(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        MetadataReader metadata = pe.GetMetadataReader();
        var types = metadata.TypeDefinitions.Select(handle => metadata.GetTypeDefinition(handle))
            .Where(type => (type.Attributes & TypeAttributes.VisibilityMask) is TypeAttributes.Public or TypeAttributes.NestedPublic).ToArray();
        Console.WriteLine("Selected formal scripting API: " + Path.GetFullPath(path));
        foreach (string name in new[] { "IUiService", "UI", "UiContextOptions", "UiDocumentAsset", "UiRenderFrame", "UiEvent", "GameSystem", "GameScene", "GameObject", "GameComponent", "PrefabInstanceInfo", "PrefabAsset", "SceneAsset", "SceneEdits", "IEditorSceneWorkspace", "IEditorScenePlayMode", "EditorSceneWorkspaceFactory", "RuntimeSession", "SceneWorld", "EditorViewportContributor", "EditorViewportContribution", "EditorViewportContext", "EditorViewportManipulationSpace", "IEditorHistory", "IInspectionPropertyEditService" })
        {
            TypeDefinition[] matches = types.Where(type => metadata.GetString(type.Name) == name).ToArray();
            Console.WriteLine("Native scene authoring boundary: " + name + ": " + (matches.Length == 0 ? "not exported" : "exported"));
            foreach (TypeDefinition type in matches)
            {
                string[] methods = type.GetMethods().Select(handle => metadata.GetMethodDefinition(handle))
                    .Where(method => (method.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public)
                    .Select(method => metadata.GetString(method.Name)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
                Console.WriteLine(name + " public operations: " + string.Join(", ", methods));
            }
        }
        foreach (string name in new[] { "SerializationRegistry", "TypeCache", "TypeRegistry" })
            Console.WriteLine(name + ": " + (types.Any(type => metadata.GetString(type.Name) == name) ? "exported" : "not exported"));
        foreach (string name in new[] { "EditorDragDropRenderer", "EditorDropWidgetResult", "EditorDragData", "EditorDrop", "EditorInteraction", "ISceneReloadStateTransfer", "IEditorReloadParticipant" })
            Console.WriteLine("Native lifecycle/drag boundary: " + name + ": " + (types.Any(type => metadata.GetString(type.Name) == name) ? "exported" : "not exported"));
        foreach (string name in new[] { "Identity", "IdentityObject", "RuntimeIdentity", "Event", "EventDispatcher", "EventHub", "GraphDocument", "Rect", "Color", "Vector2", "Matrix", "GameScene", "GameObject", "GameComponent", "GameBehavior", "SceneManager", "Time", "AssetPipeline", "AssetRecord", "AssetDatabase", "AssetCatalog", "AssetFileEntry", "AssetSourceId", "AssetPath" })
        {
            TypeDefinition[] matches = types.Where(type => metadata.GetString(type.Name) == name).ToArray();
            Console.WriteLine("Core/assets/scene reuse: " + name + ": " + (matches.Length == 0 ? "not exported" : string.Join(", ", matches.Select(type => metadata.GetString(type.Namespace) + "." + name))));
            if (name is not ("Rect" or "AssetPipeline" or "AssetDatabase" or "AssetCatalog" or "AssetRecord")) continue;
            foreach (TypeDefinition type in matches)
            {
                string[] methods = type.GetMethods().Select(handle => metadata.GetMethodDefinition(handle))
                    .Where(method => (method.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public)
                    .Select(method => metadata.GetString(method.Name)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
                Console.WriteLine(name + " public operations: " + string.Join(", ", methods));
            }
        }
        foreach (TypeDefinition type in types.Where(type => metadata.GetString(type.Name) is "RenderOutputInput" or "InputSnapshot"))
        {
            string[] properties = type.GetProperties().Select(handle => metadata.GetString(metadata.GetPropertyDefinition(handle).Name)).Order(StringComparer.Ordinal).ToArray();
            Console.WriteLine(metadata.GetString(type.Namespace) + "." + metadata.GetString(type.Name) + " properties: " + string.Join(", ", properties));
            Console.WriteLine("IME composition property: " + properties.Any(name => name.Contains("compos", StringComparison.OrdinalIgnoreCase)));
            Console.WriteLine("Gamepad property: " + properties.Any(name => name.Contains("gamepad", StringComparison.OrdinalIgnoreCase) || name.Contains("joystick", StringComparison.OrdinalIgnoreCase)));
        }
        foreach (TypeDefinition type in types.Where(type => metadata.GetString(type.Name) is "EditorPanel" or "EditorPanelAttribute" or "EditorSettings" or "EditorSetting" or "EditorSettingObject" or "EditorUiInput"))
        {
            string[] properties = type.GetProperties().Select(handle => metadata.GetString(metadata.GetPropertyDefinition(handle).Name)).Order(StringComparer.Ordinal).ToArray();
            Console.WriteLine("Native editor language boundary: " + metadata.GetString(type.Namespace) + "." + metadata.GetString(type.Name) + " properties: " + string.Join(", ", properties));
        }
        foreach (string name in new[] { "IPlatformWindow", "IPlatformApplication", "Clipboard", "IClipboard", "IApplicationClipboard", "DisplayMode", "WindowMode" })
        {
            TypeDefinition[] matches = types.Where(type => metadata.GetString(type.Name) == name).ToArray();
            Console.WriteLine("Display/clipboard boundary: " + name + ": " + (matches.Length == 0 ? "not exported" : "exported"));
            foreach (TypeDefinition type in matches)
            {
                string[] methods = type.GetMethods().Select(handle => metadata.GetMethodDefinition(handle))
                    .Where(method => (method.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public)
                    .Select(method => metadata.GetString(method.Name)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
                Console.WriteLine(name + " public operations: " + string.Join(", ", methods));
            }
        }
    }
}
