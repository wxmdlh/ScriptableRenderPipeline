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
            List<Controller> sourceControllers = controllers.ToList();
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

                VFXViewWindow.currentWindow.LoadResource(targetSubgraph.GetResource());

                VFXView targetView = VFXViewWindow.currentWindow.graphView;


                List<VFXNodeController> targetControllers = new List<VFXNodeController>();

                VFXPaste.Paste(targetView.controller, rect.center, result, targetView, null, targetControllers);

                // Change each parameter created by copy paste ( and therefore a parameter copied ) to exposed

                List<VFXParameterController> targetParameters = new List<VFXParameterController>();

                foreach ( var parameter in targetView.controller.parameterControllers)
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

                    var inputNode = input.sourceNode;
                    var outputNode = output.sourceNode;

                    var inputGraph = inputNode.viewController;
                    var outputGraph = outputNode.viewController;

                    targetView.controller.CreateLink(input, output);
                }

                // Search for links between the selected part and other parts of the graph.
                Dictionary<VFXDataAnchorController, List<VFXDataAnchorController>> traversingEdges = new Dictionary<VFXDataAnchorController, List<VFXDataAnchorController>>();

                foreach(var edge in sourceController.dataEdges.Where(
                    t=>
                    {
                        var inputInControllers = sourceControllers.Contains(t.input.sourceNode);
                        var outputInControllers = sourceControllers.Contains(t.output.sourceNode);

                        return inputInControllers && !outputInControllers;
                    }
                    ))
                {
                    List<VFXDataAnchorController> outputs = null;
                    if( ! traversingEdges.TryGetValue(edge.input,out outputs) )
                    {
                        outputs = new List<VFXDataAnchorController>();
                        traversingEdges[edge.input] = outputs;
                    }

                    outputs.Add(edge.output);
                }

                var newSourceInputs = traversingEdges.Keys.ToArray();

                for(int i = 0; i < newSourceInputs.Length; ++i)
                {
                    VFXParameter newTargetParameter = targetView.controller.AddVFXParameter(Vector2.zero, VFXLibrary.GetParameters().First(t => t.model.type == newSourceInputs[i].portType));

                    targetView.controller.LightApplyChanges();

                    VFXParameterController newTargetParamController = targetView.controller.GetParameterController(newTargetParameter);
                    newTargetParamController.exposed = true;

                    var outputs = traversingEdges[newSourceInputs[i]];

                    var firstOutput = outputs.First();

                    VFXNodeController parameterNode = targetView.controller.AddVFXParameter(firstOutput.sourceNode.position - new Vector2( 200,0),newTargetParamController,null );

                    //first the equivalent of sourceInput in the target

                    VFXNodeController targetNode = targetControllers[sourceControllers.IndexOf(newSourceInputs[i].sourceNode)];

                    VFXDataAnchorController targetAnchor = targetNode.inputPorts.First(t => t.path == newSourceInputs[i].path);

                    // Link the parameternode and the input in the target
                    targetView.controller.CreateLink(targetAnchor, parameterNode.outputPorts[0]);

                    op.ResyncSlots(true);
                    sourceNodeController.ApplyChanges();
                    //Link all the outputs to the matching input of the subgraph
                    foreach ( var output in outputs)
                    {
                        sourceController.CreateLink(sourceNodeController.inputPorts.First(t=> t.model == op.inputSlots.Last()), output);
                    }
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
