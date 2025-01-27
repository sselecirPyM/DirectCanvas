﻿using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using NekoPainter.Core;
using NekoPainter.Core.Util;
using NekoPainter.FileFormat;
using CanvasRendering;
using System.Numerics;
using System.IO;
using System;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using NekoPainter.Data;
using NekoPainter.Core.Nodes;

namespace NekoPainter
{
    public class DocumentRenderer : IDisposable
    {
        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
        long prevTick = 0;
        float deltaTime = 0;

        public void RenderAll(NekoPainterDocument doc, RenderTexture output)
        {
            this.nekoPainterDocument = doc;
            this.livedDocument = doc.livedDocument;

            if (ManagedLayout.Count == 0) return;

            gpuCompute.document = livedDocument;
            gpuCompute.deviceResources = nekoPainterDocument.DeviceResources;
            nodeContext.gpuCompute = gpuCompute;

            deltaTime = Math.Clamp((stopwatch.ElapsedTicks - prevTick) / 1e7f, 0, 1);
            prevTick = stopwatch.ElapsedTicks;

            output.Clear();
            int ofs = 0;
            for (int i = ManagedLayout.Count - 1; i >= 0; i--)
            {
                PictureLayout selectedLayout = ManagedLayout[i];
                if (ManagedLayout[i].Hidden)
                {
                    ofs += 256;
                    continue;
                }

                livedDocument.LayoutTex.TryGetValue(selectedLayout.guid, out var tiledTexture);
                if (livedDocument.blendModesMap.TryGetValue(selectedLayout.BlendMode, out var blendMode1))
                {
                    int executeCount = 0;
                    if (nekoPainterDocument.PaintAgent.CurrentLayout == selectedLayout || selectedLayout.generateCache)
                    {
                        List<int> executeOrder;
                        TiledTexture finalOutput = null;
                        var graph = selectedLayout.graph;
                        if (graph != null)
                        {
                            if (graph.Nodes.Count > 0)
                            {
                                SetAnimateNodeCacheInvalid(graph);
                                executeOrder = graph.GetUpdateList(graph.outputNode);
                                ExecuteNodes(graph, executeOrder);
                                executeCount = executeOrder.Count;
                            }
                            if (graph.NodeParamCaches != null && graph.NodeParamCaches.TryGetValue(graph.outputNode, out var cache))
                            {
                                foreach (var cache1 in cache.outputCache)
                                    if (cache1.Value is TiledTexture t1)
                                        finalOutput = t1;
                            }
                        }
                        selectedLayout.generateCache = false;
                        //if (selectedLayout.generateCache.SetFalse())
                        //{
                        TiledTexture tiledTexture2 = null;
                        if (livedDocument.LayoutTex.TryGetValue(selectedLayout.guid, out var tiledTexture1))
                        {
                            if (graph != null && graph.cacheParams.TryGetValue("outputNode", out int outputNode1) && outputNode1 == graph.outputNode && executeCount == 0)
                            {
                                tiledTexture2 = tiledTexture1;
                            }
                            else
                            {
                                tiledTexture1.Dispose();
                                tiledTexture2 = finalOutput != null ? new TiledTexture(finalOutput) : null;
                            }
                        }
                        else
                        {
                            tiledTexture2 = finalOutput != null ? new TiledTexture(finalOutput) : null;
                        }
                        if (tiledTexture2 != null)
                            livedDocument.LayoutTex[selectedLayout.guid] = tiledTexture2;
                        else
                            livedDocument.LayoutTex.Remove(selectedLayout.guid);
                        //}

                        if (tiledTexture2 != null)
                        {
                            var texture1 = gpuCompute.GetTemporaryTexture();
                            tiledTexture2.UnzipToTexture(((Texture2D)texture1)._texture);
                            BlendMode(blendMode1, selectedLayout, texture1, output);
                            gpuCompute.RecycleTemplateTextures();
                        }
                        if (graph != null)
                            graph.cacheParams["outputNode"] = graph.outputNode;
                    }
                    else if (tiledTexture != null && tiledTexture.tilesCount != 0)
                    {
                        var texture1 = gpuCompute.GetTemporaryTexture();
                        tiledTexture.UnzipToTexture(((Texture2D)texture1)._texture);
                        BlendMode(blendMode1, selectedLayout, texture1, output);
                        gpuCompute.RecycleTemplateTextures();
                    }
                }
                ofs += 256;
            }
        }

        IReadOnlyList<PictureLayout> ManagedLayout { get { return livedDocument.Layouts; } }

        public NodeContext nodeContext = new NodeContext();

        NekoPainterDocument nekoPainterDocument;
        LivedNekoPainterDocument livedDocument;

        public void ExecuteNodes(Graph graph, List<int> executeOrder)
        {
            GC(graph);
            foreach (int nodeId in executeOrder)
            {
                var node = graph.Nodes[nodeId];
                if (node.strokeNode != null)
                {
                    var cache = graph.NodeParamCaches.GetOrCreate(node.Luid);
                    cache.outputCache["strokes"] = node.strokeNode.strokes;
                    cache.valid = true;
                }
                else if (node.fileNode != null)
                {
                    var cache = graph.NodeParamCaches.GetOrCreate(node.Luid);
                    if (!cache.outputCache.TryGetValue(node.fileNode.path, out object path1))
                    {
                        cache.outputCache["filePath"] = node.fileNode.path;
                        cache.outputCache["bytes"] = File.ReadAllBytes(node.fileNode.path);
                    }
                    cache.valid = true;
                }
                else if (node.scriptNode != null)
                {
                    var nodeDef = livedDocument.scriptNodeDefs[node.GetNodeTypeName()];

                    ScriptGlobal global = new ScriptGlobal { parameters = new Dictionary<string, object>(), context = nodeContext };
                    nodeContext.deltaTime = deltaTime;
                    nodeContext.width = livedDocument.Width;
                    nodeContext.height = livedDocument.Height;
                    var cache = graph.NodeParamCaches.GetOrCreate(nodeId);
                    cache.valid = true;

                    //获取输入
                    if (node.Inputs != null)
                        foreach (var input in node.Inputs)
                        {
                            var inputNode = graph.Nodes[input.Value.targetUid];
                            var inputNodeCache = graph.NodeParamCaches.GetOrCreate(inputNode.Luid);
                            if (inputNodeCache.outputCache.TryGetValue(input.Value.targetSocket, out object obj1))
                            {
                                if (obj1 is TiledTexture tex1)
                                {
                                    var texx = (Texture2D)gpuCompute.GetTemporaryTexture();
                                    tex1.UnzipToTexture(texx._texture);
                                    global.parameters[input.Key] = texx;
                                }
                                else
                                {
                                    global.parameters[input.Key] = obj1;
                                }
                            }
                        }
                    //检查null输入
                    foreach (var ioDef in nodeDef.ioDefs)
                    {
                        var param = nodeDef.parameters.Find(u => u.name == ioDef.name);
                        if (param.type == "texture2D" && ioDef.ioType == "input")
                        {
                            if (!global.parameters.ContainsKey(ioDef.name))
                            {
                                global.parameters[ioDef.name] = gpuCompute.GetTemporaryTexture();
                            }
                            else
                            {

                            }
                        }
                        else if (ioDef.ioType == "cache")
                        {
                            global.parameters[ioDef.name] = cache.outputCache.GetOrDefault(ioDef.name, null);
                        }
                    }
                    //检查参数
                    if (nodeDef.parameters != null)
                    {
                        foreach (var param in nodeDef.parameters)
                        {
                            if (param.type == "float")
                            {
                                global.parameters[param.name] = node.fParams.GetOrDefault(param.name, (float)(param.defaultValue1));
                            }
                            if (param.type == "float2")
                            {
                                global.parameters[param.name] = node.f2Params.GetOrDefault(param.name, (Vector2)(param.defaultValue1));
                            }
                            if (param.type == "float3" || param.type == "color3")
                            {
                                global.parameters[param.name] = node.f3Params.GetOrDefault(param.name, (Vector3)(param.defaultValue1));
                            }
                            if (param.type == "float4" || param.type == "color4")
                            {
                                global.parameters[param.name] = node.f4Params.GetOrDefault(param.name, (Vector4)(param.defaultValue1));
                            }
                            if (param.type == "int")
                            {
                                global.parameters[param.name] = node.iParams.GetOrDefault(param.name, (int)(param.defaultValue1));
                            }
                            if (param.type == "bool")
                            {
                                global.parameters[param.name] = node.bParams.GetOrDefault(param.name, (bool)(param.defaultValue1));
                            }
                            if (param.type == "string")
                            {
                                global.parameters[param.name] = node.sParams.GetOrDefault(param.name, (string)(param.defaultValue1));
                            }
                        }
                    }
                    //编译、运行脚本
                    RunScript(nodeDef.path, global);

                    //缓存输出
                    foreach (var ioDef in nodeDef.ioDefs)
                    {
                        var param = nodeDef.parameters.Find(u => u.name == ioDef.name);
                        if (ioDef.ioType == "output")
                        {
                            if (param.type == "texture2D" && global.parameters.ContainsKey(ioDef.name))
                            {
                                Texture2D tex = (Texture2D)global.parameters[ioDef.name];
                                if (cache.outputCache.TryGetValue(ioDef.name, out var _tex1))
                                {
                                    ((TiledTexture)_tex1).Dispose();
                                }
                                cache.outputCache[ioDef.name] = new TiledTexture(tex._texture);
                            }
                            else
                            {
                                cache.outputCache[ioDef.name] = global.parameters[ioDef.name];
                            }
                        }
                        else if (ioDef.ioType == "cache")
                        {
                            cache.outputCache[ioDef.name] = global.parameters[ioDef.name];
                        }
                    }
                    gpuCompute.RecycleTemplateTextures();
                }
            }
        }

        public void SetAnimateNodeCacheInvalid(Graph graph)
        {
            foreach (var nodePair in graph.Nodes)
            {
                var node = nodePair.Value;
                var nodeDef = livedDocument.scriptNodeDefs[node.GetNodeTypeName()];
                if (nodeDef.animated)
                {
                    graph.SetNodeCacheInvalid(nodePair.Key);
                }
            }
        }

        public void RunScript(string path, ScriptGlobal global)
        {
            var script = livedDocument.scriptCache.GetOrCreate(path, () =>
            {
                ScriptOptions options = ScriptOptions.Default
                .WithReferences(typeof(Texture2D).Assembly, typeof(SixLabors.ImageSharp.Image).Assembly, typeof(SixLabors.ImageSharp.Drawing.Path).Assembly)
                .WithImports("NekoPainter.Data").WithOptimizationLevel(Microsoft.CodeAnalysis.OptimizationLevel.Release);

                return CSharpScript.Create(livedDocument.scripts[path], options, typeof(ScriptGlobal));
            });
            var state = script.RunAsync(global).Result;
        }

        GPUCompute gpuCompute = new GPUCompute();

        HashSet<int> gcRemoveNode = new HashSet<int>();
        public void GC(Graph graph)
        {
            if (graph.NodeParamCaches == null) return;
            gcRemoveNode.Clear();
            foreach (var cache in graph.NodeParamCaches)
            {
                if (!graph.Nodes.ContainsKey(cache.Key))
                {
                    gcRemoveNode.Add(cache.Key);
                    foreach (var cache1 in cache.Value.outputCache)
                    {
                        if (cache1.Value is TiledTexture t1)
                        {
                            t1.Dispose();
                        }
                    }
                }
            }
            foreach (var key in gcRemoveNode)
                graph.NodeParamCaches.Remove(key);
        }

        public void BlendMode(BlendMode blendMode, PictureLayout layout, ITexture2D tex1, RenderTexture output)
        {
            ScriptGlobal global = new ScriptGlobal { parameters = new Dictionary<string, object>(), context = nodeContext };
            Texture2D tex0 = new Texture2D() { _texture = output };
            global.parameters["tex0"] = tex0;
            global.parameters["tex1"] = tex1;

            var node = layout;
            var parameters = blendMode.parameters;
            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    if (param.type == "float")
                    {
                        global.parameters[param.name] = node.fParams.GetOrDefault(param.name, (float)(param.defaultValue1));
                    }
                    if (param.type == "float2")
                    {
                        global.parameters[param.name] = node.f2Params.GetOrDefault(param.name, (Vector2)(param.defaultValue1));
                    }
                    if (param.type == "float3" || param.type == "color3")
                    {
                        global.parameters[param.name] = node.f3Params.GetOrDefault(param.name, (Vector3)(param.defaultValue1));
                    }
                    if (param.type == "float4" || param.type == "color4")
                    {
                        global.parameters[param.name] = node.f4Params.GetOrDefault(param.name, (Vector4)(param.defaultValue1));
                    }
                    if (param.type == "int")
                    {
                        global.parameters[param.name] = node.iParams.GetOrDefault(param.name, (int)(param.defaultValue1));
                    }
                    if (param.type == "bool")
                    {
                        global.parameters[param.name] = node.bParams.GetOrDefault(param.name, (bool)(param.defaultValue1));
                    }
                    if (param.type == "string")
                    {
                        global.parameters[param.name] = node.sParams.GetOrDefault(param.name, (string)(param.defaultValue1));
                    }
                }
            }

            RunScript(blendMode.script, global);
        }

        public void Dispose()
        {
            gpuCompute.Dispose();
        }
    }

    public class ScriptGlobal
    {
        public Dictionary<string, object> parameters;
        public NodeContext context;
    }
}

