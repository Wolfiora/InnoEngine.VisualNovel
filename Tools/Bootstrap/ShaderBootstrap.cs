using System;
using System.Linq;
using Inno.Assets;
using Inno.Assets.Pipeline;
using Inno.Core.Graphs;
using Inno.Core.Serialization;
using Inno.Rendering;
using Inno.Rendering.Assets;
using Inno.Rendering.Assets.Authoring;
using Inno.Rendering.Shaders;

namespace Inno.VisualNovel.Tools.Bootstrap;

internal static class ShaderBootstrap
{
    internal static void Ensure(
        AssetPipeline assets,
        SerializationRegistry serialization
    ) { Build(assets, serialization, false); Build(assets, serialization, true); }

    private static void Build(
        AssetPipeline assets,
        SerializationRegistry serialization,
        bool post
    ) {
        AssetPath vertexPath = AssetPath.Project("BuiltIn/Shaders/Functions/NovelVertex.ishadersource");
        AssetPath fragmentPath = AssetPath.Project("BuiltIn/Shaders/Functions/" + (post ? "NovelPostProcess" : "NovelFragment") + ".ishadersource");
        ImportFunction(vertexPath);
        ImportFunction(fragmentPath);
        ShaderFunctionAsset vertex = assets.Load<ShaderFunctionAsset>(vertexPath);
        ShaderFunctionAsset fragment = assets.Load<ShaderFunctionAsset>(fragmentPath);
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(assets.assetRoot, "BuiltIn", "Shaders", "Graphs"));
        AssetPath shaderPath = AssetPath.Project("BuiltIn/Shaders/Graphs/" + (post ? "NovelPostProcess" : "NovelSurface") + ".ishader");
        var store = new ShaderGraphSourceStore(assets, serialization);
        string? expected = null;
        try { expected = store.Read(shaderPath).contentHash; }
        catch (System.IO.FileNotFoundException) { }
        ShaderPropertyDefinition[] properties = post ? [Texture("s_current"), Texture("s_previous"), Uniform("u_config"), Uniform("u_parameters"), Uniform("u_color"), Uniform("u_size")]
            : [Texture("s_image")];
        var definition = post ? new ShaderDefinition("VN Image Composition", properties, [],
            [new("Process", ShaderProgramKind.Raster, renderState: new() { cull = ShaderCullMode.None, depthCompare = ShaderCompareFunction.Always, depthWrite = false, blend = RenderBlendState.opaque, colorWriteMask = 15 }),
             new("Present", ShaderProgramKind.Raster, renderState: new() { cull = ShaderCullMode.None, depthCompare = ShaderCompareFunction.Always, depthWrite = false, blend = RenderBlendState.premultiplied, colorWriteMask = 15 })],
            techniques: [new(new("default"), new("inno.vn.post"), [new(new("inno.vn.process"), "Process"), new(new("inno.vn.present"), "Present")])])
            : new ShaderDefinition("VN Surface", properties, [],
            [new("Alpha", ShaderProgramKind.Raster, renderState: new() { cull = ShaderCullMode.None, depthCompare = ShaderCompareFunction.Always, depthWrite = false, blend = RenderBlendState.alpha, colorWriteMask = 15 })],
            techniques: [new(new("default"), new("inno.vn.surface"), [new(new("inno.vn.alpha"), "Alpha")])]);
        GraphDocument graph = ShaderGraphDocument.Create(definition, serialization, SerializationContext.empty);
        GraphNodeRecord vertexOutput = Stage(ShaderStage.Vertex, vertex,
            [Input("vertexPosition", "float2", ShaderIrInputKind.VertexAttribute, "position"), Input("uv", "float2", ShaderIrInputKind.VertexAttribute, "texcoord"), Input("tint", "float4", ShaderIrInputKind.VertexAttribute, "color")],
            [("position", ShaderIrOutputKind.ClipPosition, "", 0), ("textureCoordinate", ShaderIrOutputKind.Varying, "texcoord", 0), ("color", ShaderIrOutputKind.Varying, "color", 0)]);
        ShaderGraphInputSettings[] fragmentInputs = [Input("uv", "float2", ShaderIrInputKind.Varying, "texcoord"), Input("tint", "float4", ShaderIrInputKind.Varying, "color"),
            .. properties.Select((property, index) => Input(property.id.value, property.type == ShaderPropertyType.Texture2D ? "sampled-texture2d" : "float4",
                property.type == ShaderPropertyType.Texture2D ? ShaderIrInputKind.SampledTexture : ShaderIrInputKind.Uniform, "", index))];
        GraphNodeRecord fragmentOutput = Stage(ShaderStage.Fragment, fragment, fragmentInputs,
            [("return", ShaderIrOutputKind.Color, "", 0)]);
        foreach (var pass in definition.passes) graph = ShaderGraphPrograms.Bind(graph, pass.name, [vertexOutput.id, fragmentOutput.id], serialization, SerializationContext.empty);
        store.Save(shaderPath, graph, expected);
        if (!assets.Import(shaderPath)) throw new InvalidOperationException("VN surface graph did not import.");

        static ShaderPropertyDefinition Texture(string id) => new(new(id), id, ShaderPropertyType.Texture2D, ShaderStage.Fragment, default, bindingOwner: ShaderPropertyBindingOwner.RenderPass);
        static ShaderPropertyDefinition Uniform(string id) => new(new(id), id, ShaderPropertyType.Vector4, ShaderStage.Fragment,
            MaterialValue.FromVector(default), bindingOwner: ShaderPropertyBindingOwner.RenderPass);

        GraphNodeRecord Stage(
            ShaderStage stage,
            ShaderFunctionAsset function,
            ShaderGraphInputSettings[] inputs,
            (string id, ShaderIrOutputKind kind, string semantic, int location)[] outputs
        ) {
            var output = new GraphNodeRecord(new(stage + ".output"), ShaderGraphDocument.outputDefinitionId);
            output.SetValue("settings", Value(new ShaderGraphStageSettings
            {
                stage = stage, outputs = outputs.Select(item => new ShaderGraphOutput { id = item.id, kind = item.kind, semantic = item.semantic, location = item.location }).ToArray()
            }));
            graph.AddNode(output);
            var call = new GraphNodeRecord(new(stage + ".function"), "inno.shader.source");
            graph.AddNode(call);
            call.SetValue("stage", Value(output.id.value)); call.SetValue("sourceId", Value(function.identity.persistentId));
            call.SetValue("sourcePath", Value(function.assetPath.ToString())); call.SetValue("function", Value(function.exports.Single()));
            foreach (ShaderGraphInputSettings input in inputs)
            {
                var node = new GraphNodeRecord(new(stage + "." + input.id), "inno.shader.stage-input");
                node.SetValue("stage", Value(output.id.value)); node.SetValue("settings", Value(input));
                graph.AddNode(node);
                graph.AddEdge(new(new(stage + ".input." + input.id), new(node.id, new("value")), new(call.id, new("input." + input.id))));
            }
            foreach (var result in outputs)
                graph.AddEdge(new(new(stage + ".output." + result.id), new(call.id, new(result.id == "return" ? "return" : "output." + result.id)), new(output.id, new(result.id))));
            return output;
        }
        GraphSerializedValue Value<T>(T value) => ShaderGraphDocument.Encode(value, serialization, SerializationContext.empty);

        void ImportFunction(AssetPath path)
        {
            AssetImportSettingsSnapshot snapshot = assets.GetImportSettings(path);
            bool imported = snapshot.value is ShaderSourceImportSettings settings && settings.languageId == "inno.shader-language.bgfx-sc" && settings.implementationId == "bgfx"
                ? assets.Import(path) : assets.SaveImportSettings(path, new ShaderSourceImportSettings { languageId = "inno.shader-language.bgfx-sc", implementationId = "bgfx", exports = ["Evaluate"], catalogPath = "VisualNovel/Surface" }, snapshot.fingerprint);
            if (!imported) throw new InvalidOperationException("VN shader source function did not import: " + path);
        }
    }

    private static ShaderGraphInputSettings Input(
        string id,
        string type,
        ShaderIrInputKind kind,
        string semantic,
        int location = 0
    ) => new() { id = id, type = new() { id = type }, kind = kind, semantic = semantic, location = location };
}
