using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Experimental.VFX;
using UnityEditor.Experimental.GraphView;

using NodeID = System.UInt32;

namespace UnityEditor.VFX.UI
{
    static class VFXConvertSubgraph
    {

        public static void ConvertToSubgraphOperator(VFXView sourceView, IEnumerable<Controller> controllers,Rect rect)
        {
            List<Controller> sourceControllers = controllers.Concat(sourceView.controller.dataEdges.Where( t => controllers.Contains(t.input.sourceNode) && controllers.Contains(t.output.sourceNode) ) ).Distinct().ToList();
            VFXViewController sourceController = sourceView.controller;
            VFXGraph sourceGraph = sourceController.graph;
            sourceController.useCount++;
            try
            {

                var sourceParameters = new Dictionary<string, VFXParameterNodeController>();

                foreach (var parameterNode in sourceControllers.OfType<VFXParameterNodeController>())
                {
                    sourceParameters[parameterNode.exposedName] = parameterNode;
                }

                object result = VFXCopy.Copy(sourceControllers, rect);

                string graphPath = AssetDatabase.GetAssetPath(sourceView.controller.model.asset);
                string graphName = Path.GetFileNameWithoutExtension(graphPath);
                string graphDirPath = Path.GetDirectoryName(graphPath);

                string targetSubgraphPath = string.Format("{0}/{1}_Subgraph.subvfxoperator", graphDirPath, graphName);
                int cpt = 1;
                while(File.Exists(targetSubgraphPath))
                {
                    targetSubgraphPath = string.Format("{0}/{1}_Subgraph_{2}.subvfxoperator", graphDirPath, graphName,cpt++);
                }

                VisualEffectSubgraphOperator targetSubgraph = VisualEffectResource.CreateNewSubgraphOperator(targetSubgraphPath);

                //VFXViewWindow.currentWindow.LoadResource(targetSubgraph.GetResource());
                //VFXView targetView = VFXViewWindow.currentWindow.graphView;

                VFXViewController targetController = VFXViewController.GetController(targetSubgraph.GetResource());
                targetController.useCount++;

                List<VFXNodeController> targetControllers = new List<VFXNodeController>();

                VFXPaste.Paste(targetController, rect.center, result, null, null, targetControllers);

                // Change each parameter created by copy paste ( and therefore a parameter copied ) to exposed

                List<VFXParameterController> targetParameters = new List<VFXParameterController>();

                foreach ( var parameter in targetController.parameterControllers)
                {
                    targetParameters.Add(parameter);
                    parameter.exposed = true;
                }

                VFXSubgraphOperator op = ScriptableObject.CreateInstance<VFXSubgraphOperator>();

                op.SetSettingValue("m_SubGraph", targetSubgraph);

                op.position = rect.center;

                sourceController.graph.AddChild(op);

                sourceController.LightApplyChanges();

                var sourceNodeController = sourceController.GetRootNodeController(op, 0);

                sourceNodeController.ApplyChanges();

                for(int i = 0; i < targetParameters.Count; ++i)
                {
                    var input = sourceNodeController.inputPorts.First(t => t.model == op.inputSlots[i]);
                    var output = sourceParameters[targetParameters[i].exposedName].outputPorts.First();

                    targetController.CreateLink(input, output);
                }

                {
                    // Search for links between with inputs in the selected part and the output in other parts of the graph.
                    Dictionary<VFXDataAnchorController, List<VFXDataAnchorController>> traversingInEdges = new Dictionary<VFXDataAnchorController, List<VFXDataAnchorController>>();

                    foreach(var edge in sourceController.dataEdges.Where(
                        t=>
                        {
                            if (t.output.sourceNode is VFXParameterNodeController || t.input.sourceNode is VFXParameterNodeController)
                                return false;
                            var inputInControllers = sourceControllers.Contains(t.input.sourceNode);
                            var outputInControllers = sourceControllers.Contains(t.output.sourceNode);

                            return inputInControllers && !outputInControllers;
                        }
                        ))
                    {
                        List<VFXDataAnchorController> outputs = null;
                        if( ! traversingInEdges.TryGetValue(edge.input,out outputs) )
                        {
                            outputs = new List<VFXDataAnchorController>();
                            traversingInEdges[edge.input] = outputs;
                        }

                        outputs.Add(edge.output);
                    }

                    var newSourceInputs = traversingInEdges.Keys.ToArray();

                    for (int i = 0; i < newSourceInputs.Length; ++i)
                    {
                        VFXParameter newTargetParameter = targetController.AddVFXParameter(Vector2.zero, VFXLibrary.GetParameters().First(t => t.model.type == newSourceInputs[i].portType));

                        targetController.LightApplyChanges();

                        VFXParameterController newTargetParamController = targetController.GetParameterController(newTargetParameter);
                        newTargetParamController.exposed = true;

                        var outputs = traversingInEdges[newSourceInputs[i]];

                        //first the equivalent of sourceInput in the target

                        VFXNodeController targetNode = targetControllers[sourceControllers.IndexOf(newSourceInputs[i].sourceNode)];
                        VFXDataAnchorController targetAnchor = targetNode.inputPorts.First(t => t.path == newSourceInputs[i].path);

                        VFXNodeController parameterNode = targetController.AddVFXParameter(targetNode.position - new Vector2(200, 0), newTargetParamController, null);

                        // Link the parameternode and the input in the target
                        targetController.CreateLink(targetAnchor, parameterNode.outputPorts[0]);

                        op.ResyncSlots(true);
                        sourceNodeController.ApplyChanges();
                        //Link all the outputs to the matching input of the subgraph
                        foreach (var output in outputs)
                        {
                            sourceController.CreateLink(sourceNodeController.inputPorts.First(t => t.model == op.inputSlots.Last()), output);
                        }
                    }
                }

                {
                    var traversingOutEdges = new Dictionary<VFXDataAnchorController, List<VFXDataAnchorController>>();

                    foreach (var edge in sourceController.dataEdges.Where(
                        t =>
                        {
                            if (t.output.sourceNode is VFXParameterNodeController || t.input.sourceNode is VFXParameterNodeController)
                                return false;
                            var inputInControllers = sourceControllers.Contains(t.input.sourceNode);
                            var outputInControllers = sourceControllers.Contains(t.output.sourceNode);

                            return !inputInControllers && outputInControllers &&;
                        }
                        ))
                    {
                        List<VFXDataAnchorController> inputs = null;
                        if (!traversingOutEdges.TryGetValue(edge.output, out inputs))
                        {
                            inputs = new List<VFXDataAnchorController>();
                            traversingOutEdges[edge.output] = inputs;
                        }

                        inputs.Add(edge.input);
                    }

                    var newSourceOutputs = traversingOutEdges.Keys.ToArray();

                    for (int i = 0; i < newSourceOutputs.Length; ++i)
                    {
                        VFXParameter newTargetParameter = targetController.AddVFXParameter(Vector2.zero, VFXLibrary.GetParameters().First(t => t.model.type == newSourceOutputs[i].portType));

                        targetController.LightApplyChanges();

                        VFXParameterController newTargetParamController = targetController.GetParameterController(newTargetParameter);
                        newTargetParamController.exposed = true;
                        newTargetParamController.isOutput = true;

                        var inputs = traversingOutEdges[newSourceOutputs[i]];

                        //first the equivalent of sourceInput in the target

                        VFXNodeController targetNode = targetControllers[sourceControllers.IndexOf(newSourceOutputs[i].sourceNode)];
                        VFXDataAnchorController targetAnchor = targetNode.outputPorts.First(t => t.path == newSourceOutputs[i].path);

                        VFXNodeController parameterNode = targetController.AddVFXParameter(targetNode.position + new Vector2(400, 0), newTargetParamController, null);

                        // Link the parameternode and the input in the target
                        targetController.CreateLink(parameterNode.inputPorts[0],targetAnchor );

                        op.ResyncSlots(true);
                        sourceNodeController.ApplyChanges();
                        //Link all the outputs to the matching input of the subgraph
                        foreach (var input in inputs)
                        {
                            sourceController.CreateLink(input, sourceNodeController.outputPorts.First(t => t.model == op.outputSlots.Last()));
                        }
                    }

                }
                targetController.useCount--;

                foreach ( var element in sourceControllers.Where(t=> !(t is VFXParameterNodeController)))
                {
                    sourceController.RemoveElement(element);
                }

            }
            catch(System.Exception)
            {
                throw;
            }
            finally
            {
                sourceController.useCount--;
            }
        }

    }
}
