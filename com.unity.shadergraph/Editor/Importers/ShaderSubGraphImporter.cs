using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine;
using UnityEditor.Graphing;
using UnityEditor.Graphing.Util;

namespace UnityEditor.ShaderGraph
{
    [ScriptedImporter(7, Extension)]
    class ShaderSubGraphImporter : ScriptedImporter
    {
        public const string Extension = "shadersubgraph";

        [SuppressMessage("ReSharper", "UnusedMember.Local")]
        static string[] GatherDependenciesFromSourceFile(string assetPath)
        {
            return MinimalGraphData.GetDependencies(assetPath);
        }

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var graphAsset = ScriptableObject.CreateInstance<SubGraphAsset>();
            var subGraphPath = ctx.assetPath;
            var subGraphGuid = AssetDatabase.AssetPathToGUID(subGraphPath);
            graphAsset.assetGuid = subGraphGuid;
            var textGraph = File.ReadAllText(subGraphPath, Encoding.UTF8);
            var graphData = new GraphData { isSubGraph = true, assetGuid = subGraphGuid };
            var messageManager = new MessageManager();
            graphData.messageManager = messageManager;
            JsonUtility.FromJsonOverwrite(textGraph, graphData);

            try
            {
                ProcessSubGraph(graphAsset, graphData);
            }
            catch (Exception e)
            {
                graphAsset.isValid = false;
                Debug.LogException(e, graphAsset);
            }
            finally
            {
                if (messageManager.nodeMessagesChanged)
                {
                    var subGraphAsset = AssetDatabase.LoadAssetAtPath<SubGraphAsset>(AssetDatabase.GUIDToAssetPath(graphAsset.assetGuid));
                    foreach (var pair in messageManager.GetNodeMessages())
                    {
                        var node = graphData.GetNodeFromTempId(pair.Key);
                        foreach (var message in pair.Value)
                        {
                            MessageManager.Log(node, subGraphPath, message, subGraphAsset);
                        }
                    }
                }
                messageManager.ClearAll();
            }

            Texture2D texture = Resources.Load<Texture2D>("Icons/sg_subgraph_icon@64");
            ctx.AddObjectToAsset("MainAsset", graphAsset, texture);
            ctx.SetMainObject(graphAsset);
        }

        static void ProcessSubGraph(SubGraphAsset asset, GraphData graph)
        {
            var registry = new FunctionRegistry(new ShaderStringBuilder(), true);
            registry.names.Clear();
            asset.functions.Clear();
            asset.nodeProperties.Clear();
            asset.isValid = true;

            graph.OnEnable();
            graph.messageManager.ClearAll();
            graph.ValidateGraph();

            var assetPath = AssetDatabase.GUIDToAssetPath(asset.assetGuid);
            asset.hlslName = NodeUtils.GetHLSLSafeName(Path.GetFileNameWithoutExtension(assetPath));
            asset.inputStructName = $"Bindings_{asset.hlslName}_{asset.assetGuid}";
            asset.functionName = $"SG_{asset.hlslName}_{asset.assetGuid}";
            asset.path = graph.path;

            var outputNode = (SubGraphOutputNode)graph.outputNode;

            asset.outputs.Clear();
            outputNode.GetInputSlots(asset.outputs);

            List<AbstractMaterialNode> nodes = new List<AbstractMaterialNode>();
            NodeUtils.DepthFirstCollectNodesFromNode(nodes, outputNode);

            asset.effectiveShaderStage = ShaderStageCapability.All;
            foreach (var slot in asset.outputs)
            {
                var stage = NodeUtils.GetEffectiveShaderStageCapability(slot, true);
                if (stage != ShaderStageCapability.All)
                {
                    asset.effectiveShaderStage = stage;
                    break;
                }
            }

            asset.requirements = ShaderGraphRequirements.FromNodes(nodes, asset.effectiveShaderStage, false);
            asset.inputs = graph.properties.ToList();

            foreach (var node in nodes)
            {
                if (node.hasError)
                {
                    asset.isValid = false;
                    registry.ProvideFunction(asset.functionName, sb => { });
                    return;
                }
            }

            var childrenSet = new HashSet<string>();
            var descendentsSet = new HashSet<string>();

            foreach (var node in nodes)
            {
                if (node is SubGraphNode subGraphNode)
                {
                    var nestedAsset = subGraphNode.asset;
                    if (childrenSet.Add(nestedAsset.assetGuid))
                    {
                        asset.children.Add(nestedAsset.assetGuid);
                        if (descendentsSet.Add(nestedAsset.assetGuid))
                        {
                            asset.descendents.Add(nestedAsset.assetGuid);
                        }
                        foreach (var descendent in nestedAsset.descendents)
                        {
                            if (descendentsSet.Add(descendent))
                            {
                                asset.descendents.Add(descendent);
                            }
                        }
                    }
                }
                
                if (node is IGeneratesFunction generatesFunction)
                {
                    generatesFunction.GenerateNodeFunction(registry, new GraphContext(asset.inputStructName), GenerationMode.ForReals);
                }
            }

            registry.ProvideFunction(asset.functionName, sb =>
            {
                var graphContext = new GraphContext(asset.inputStructName);

                GraphUtil.GenerateSurfaceInputStruct(sb, asset.requirements, asset.inputStructName);
                sb.AppendNewLine();

                // Generate arguments... first INPUTS
                var arguments = new List<string>();
                foreach (var prop in asset.inputs)
                    arguments.Add(string.Format("{0}", prop.GetPropertyAsArgumentString()));

                // now pass surface inputs
                arguments.Add(string.Format("{0} IN", asset.inputStructName));

                // Now generate outputs
                foreach (var output in asset.outputs)
                    arguments.Add($"out {output.concreteValueType.ToString(outputNode.precision)} {output.shaderOutputName}_{output.id}");

                // Create the function prototype from the arguments
                sb.AppendLine("void {0}({1})"
                    , asset.functionName
                    , arguments.Aggregate((current, next) => $"{current}, {next}"));

                // now generate the function
                using (sb.BlockScope())
                {
                    // Just grab the body from the active nodes
                    var bodyGenerator = new ShaderGenerator();
                    foreach (var node in nodes)
                    {
                        if (node is IGeneratesBodyCode generatesBodyCode)
                            generatesBodyCode.GenerateNodeCode(bodyGenerator, graphContext, GenerationMode.ForReals);
                    }

                    foreach (var slot in asset.outputs)
                    {
                        bodyGenerator.AddShaderChunk($"{slot.shaderOutputName}_{slot.id} = {outputNode.GetSlotValue(slot.id, GenerationMode.ForReals)};");
                    }

                    sb.Append(bodyGenerator.GetShaderString(1));
                }
            });

            asset.functions.AddRange(registry.names.Select(x => new FunctionPair(x, registry.sources[x])));

            var collector = new PropertyCollector();
            asset.nodeProperties = collector.properties;
            foreach (var node in nodes)
            {
                node.CollectShaderProperties(collector, GenerationMode.ForReals);
            }

            asset.OnBeforeSerialize();
        }
    }
}