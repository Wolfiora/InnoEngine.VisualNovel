using System;
using System.IO;
using Inno.Build.Toolchains.Bgfx.Shaders;
using Inno.Build.Toolchains.Bgfx.Tools;
using Inno.Core.IO;
using Inno.Integration.WindowsX64.Bgfx;
using Inno.Rendering;

namespace Inno.VisualNovel.Tools.Bootstrap;

internal static class ShaderTargetValidation
{
    internal static void Run(string root)
    {
        string output = Path.Combine(root, "artifacts", "ShaderValidation");
        Directory.CreateDirectory(output);
        string temporary = Path.Combine(output, "Temp"); Directory.CreateDirectory(temporary);
        Environment.SetEnvironmentVariable("TEMP", temporary); Environment.SetEnvironmentVariable("TMP", temporary);
        string native = Path.Combine(root, "artifacts", "EngineEditorSnapshot", "native", "bgfx");
        var tools = new ToolRunner(new System.Collections.Generic.Dictionary<BgfxTool, string>
            { [BgfxTool.Shaderc] = Path.Combine(native, "windows-x64", "tools", "shaderc.exe") }, Path.Combine(native, "includes"));
        ShaderBuildInputs inputs = ShaderBuildInputs.Capture(Path.Combine(root, "Assets"));
        foreach (string name in new[] { "NovelSurface", "NovelPostProcess" })
        {
            using var workspace = ProcessOwnedDirectory.CreateAsync(Path.Combine(output, "Workspaces")).AsTask().GetAwaiter().GetResult();
            ShaderArtifactBuilder.Compile(inputs, workspace, "BuiltIn/Shaders/Graphs/" + name + ".ishader", WindowsX64BgfxShaderProfiles.target,
                GraphicsApi.Direct3D11, Path.Combine(output, name + ".artifact"), tools);
        }
    }
}
