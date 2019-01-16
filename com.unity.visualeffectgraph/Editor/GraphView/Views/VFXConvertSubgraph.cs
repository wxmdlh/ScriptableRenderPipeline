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

                VFXPaste.Paste(targetView.controller, rect.center, result, targetView, null);

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

                foreach (var parameterNode in controllers.OfType<VFXParameterNodeController>())
                {
                    if (parameterNode.viewController != sourceController || parameterNode.viewController.graph != sourceGraph)
                    {
                        Debug.LogError("incoherent");
                    }
                }

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
