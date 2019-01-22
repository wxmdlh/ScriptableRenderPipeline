using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.VFX;
using UnityEditor.Experimental.VFX;
using UnityEditor.Experimental.GraphView;

using NodeID = System.UInt32;

namespace UnityEditor.VFX.UI
{
    static class VFXConvertSubgraph
    {
        public static void ConvertToSubgraphContext(VFXView sourceView, IEnumerable<Controller> controllers,Rect rect)
        {
            ConvertToSubgraph(sourceView, controllers, rect, Type.Context);
        }


        public static void ConvertToSubgraphOperator(VFXView sourceView, IEnumerable<Controller> controllers, Rect rect)
        {
            ConvertToSubgraph(sourceView, controllers, rect, Type.Operator);
        }

        enum Type
        {
            Context,
            Operator,
            Block
        }

        static VisualEffectObject CreateUniquePath(VFXView sourceView, Type type)
        {
            string graphPath = AssetDatabase.GetAssetPath(sourceView.controller.model.asset);
            string graphName = Path.GetFileNameWithoutExtension(graphPath);
            string graphDirPath = Path.GetDirectoryName(graphPath);

            switch (type)
            {
                case Type.Operator:
                    {
                        string targetSubgraphPath = string.Format("{0}/{1}_SubgraphOperator.vfxoperator", graphDirPath, graphName);
                        int cpt = 1;
                        while (File.Exists(targetSubgraphPath))
                        {
                            targetSubgraphPath = string.Format("{0}/{1}_SubgraphOperator_{2}.vfxoperator", graphDirPath, graphName, cpt++);
                        }
                        return VisualEffectResource.CreateNewSubgraphOperator(targetSubgraphPath);
                    }
                case Type.Context:
                    {
                        string targetSubgraphPath = string.Format("{0}/{1}_Subgraph.vfx", graphDirPath, graphName);
                        int cpt = 1;
                        while (File.Exists(targetSubgraphPath))
                        {
                            targetSubgraphPath = string.Format("{0}/{1}_Subgraph_{2}.vfx", graphDirPath, graphName, cpt++);
                        }
                        return VisualEffectResource.CreateNewAsset(targetSubgraphPath);
                    }
                case Type.Block:
                    {
                        string targetSubgraphPath = string.Format("{0}/{1}_SubgraphBlock.vfxblock", graphDirPath, graphName);
                        int cpt = 1;
                        while (File.Exists(targetSubgraphPath))
                        {
                            targetSubgraphPath = string.Format("{0}/{1}_SubgraphBlock_{2}.vfxblock", graphDirPath, graphName, cpt++);
                        }
                        return VisualEffectResource.CreateNewSubgraphBlock(targetSubgraphPath);
                    }
            }
            return null;
        }


        static void ConvertToSubgraph(VFXView sourceView, IEnumerable<Controller> controllers, Rect rect,Type type)
        {
            List<Controller> sourceControllers = controllers.Concat(sourceView.controller.dataEdges.Where( t => controllers.Contains(t.input.sourceNode) && controllers.Contains(t.output.sourceNode) ) ).Distinct().ToList();
            List<VFXParameterNodeController> parameterNodeControllers = sourceControllers.OfType<VFXParameterNodeController>().ToList();


            VFXViewController sourceController = sourceView.controller;
            VFXGraph sourceGraph = sourceController.graph;
            sourceController.useCount++;
            
            var sourceParameters = new Dictionary<string, VFXParameterNodeController>();

            foreach (var parameterNode in parameterNodeControllers)
            {
                sourceParameters[parameterNode.exposedName] = parameterNode;
            }

            object result = VFXCopy.Copy(sourceControllers, rect);

            VisualEffectObject targetSubgraph = CreateUniquePath(sourceView,type);

            var targetController = VFXViewController.GetController(targetSubgraph.GetResource());
            targetController.useCount++;

            //try
            {
                List<VFXNodeController> targetControllers = new List<VFXNodeController>();

                VFXPaste.Paste(targetController, rect.center, result, null, null, targetControllers);

                // Change each parameter created by copy paste ( and therefore a parameter copied ) to exposed
                List<VFXParameterController> targetParameters = new List<VFXParameterController>();

                foreach ( var parameter in targetController.parameterControllers)
                {
                    targetParameters.Add(parameter);
                    parameter.exposed = true;
                }

                VFXModel sourceNode = null;
                switch( type)
                {
                    case Type.Operator:
                        sourceNode = ScriptableObject.CreateInstance<VFXSubgraphOperator>();
                        break;
                    case Type.Context:
                        sourceNode = ScriptableObject.CreateInstance<VFXSubgraphContext>();
                        break;
                    case Type.Block:
                        sourceNode = ScriptableObject.CreateInstance<VFXSubgraphBlock>();
                        break;
                }

                sourceNode.SetSettingValue("m_SubGraph", targetSubgraph);
                var sourceSlotContainer = sourceNode as IVFXSlotContainer;

                sourceNode.position = rect.center;

                sourceController.graph.AddChild(sourceNode); //TODO change this for blocks

                sourceController.LightApplyChanges();

                var sourceNodeController = sourceController.GetRootNodeController(sourceNode, 0);

                sourceNodeController.ApplyChanges();

                for(int i = 0; i < targetParameters.Count; ++i)
                {
                    var input = sourceNodeController.inputPorts.First(t => t.model == sourceSlotContainer.inputSlots[i]);
                    var output = sourceParameters[targetParameters[i].exposedName].outputPorts.First();

                    targetController.CreateLink(input, output);
                }


                var sourceControllersWithBlocks = sourceControllers.Concat(sourceControllers.OfType<VFXContextController>().SelectMany(t => t.blockControllers));

                {
                    // Search for links between with inputs in the selected part and the output in other parts of the graph.
                    Dictionary<VFXDataAnchorController, List<VFXDataAnchorController>> traversingInEdges = new Dictionary<VFXDataAnchorController, List<VFXDataAnchorController>>();

                    foreach(var edge in sourceController.dataEdges.Where(
                        t=>
                        {
                            if (t.output.sourceNode is VFXParameterNodeController || t.input.sourceNode is VFXParameterNodeController)
                                return false;
                            var inputInControllers = sourceControllersWithBlocks.Contains(t.input.sourceNode);
                            var outputInControllers = sourceControllersWithBlocks.Contains(t.output.sourceNode);

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

                        VFXNodeController targetNode = null;

                        if(newSourceInputs[i].sourceNode is VFXBlockController)
                        {
                            var blockController = newSourceInputs[i].sourceNode as VFXBlockController;
                            
                            var targetContext = targetControllers[sourceControllers.IndexOf(blockController.contextController)] as VFXContextController;

                            targetNode = targetContext.blockControllers[blockController.index];
                        }
                        else
                        {
                            targetNode = targetControllers[sourceControllers.IndexOf(newSourceInputs[i].sourceNode)];
                        }

                        VFXDataAnchorController targetAnchor = targetNode.inputPorts.First(t => t.path == newSourceInputs[i].path);

                        VFXNodeController parameterNode = targetController.AddVFXParameter(targetNode.position - new Vector2(200, 0), newTargetParamController, null);

                        // Link the parameternode and the input in the target
                        targetController.CreateLink(targetAnchor, parameterNode.outputPorts[0]);

                        if (sourceSlotContainer is VFXOperator)
                            (sourceSlotContainer as VFXOperator).ResyncSlots(true);
                        else if (sourceSlotContainer is VFXSubgraphBlock)
                        {
                            VFXSubgraphBlock blk = (sourceSlotContainer as VFXSubgraphBlock);
                            blk.RecreateCopy();
                            blk.ResyncSlots(true);
                        }
                        else if (sourceSlotContainer is VFXSubgraphContext)
                        {
                            VFXSubgraphContext ctx = (sourceSlotContainer as VFXSubgraphContext);
                            ctx.RecreateCopy();
                            ctx.ResyncSlots(true);
                        }

                        sourceNodeController.ApplyChanges();
                        //Link all the outputs to the matching input of the subgraph
                        foreach (var output in outputs)
                        {
                            sourceController.CreateLink(sourceNodeController.inputPorts.First(t => t.model == sourceSlotContainer.inputSlots.Last()), output);
                        }
                    }
                }


                if(type == Type.Operator)
                {
                    var traversingOutEdges = new Dictionary<VFXDataAnchorController, List<VFXDataAnchorController>>();

                    foreach (var edge in sourceController.dataEdges.Where(
                        t =>
                        {
                            if (t.output.sourceNode is VFXParameterNodeController || t.input.sourceNode is VFXParameterNodeController)
                                return false;
                            var inputInControllers = sourceControllersWithBlocks.Contains(t.input.sourceNode);
                            var outputInControllers = sourceControllersWithBlocks.Contains(t.output.sourceNode);

                            return !inputInControllers && outputInControllers;
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
                        newTargetParamController.isOutput = true;

                        var inputs = traversingOutEdges[newSourceOutputs[i]];

                        //first the equivalent of sourceInput in the target

                        VFXNodeController targetNode = null;

                        if (newSourceOutputs[i].sourceNode is VFXBlockController)
                        {
                            var blockController = newSourceOutputs[i].sourceNode as VFXBlockController;

                            var targetContext = targetControllers[sourceControllers.IndexOf(blockController.contextController)] as VFXContextController;

                            targetNode = targetContext.blockControllers[blockController.index];
                        }
                        else
                        {
                            targetNode = targetControllers[sourceControllers.IndexOf(newSourceOutputs[i].sourceNode)];
                        }

                        VFXDataAnchorController targetAnchor = targetNode.outputPorts.First(t => t.path == newSourceOutputs[i].path);

                        VFXNodeController parameterNode = targetController.AddVFXParameter(targetNode.position + new Vector2(400, 0), newTargetParamController, null);

                        // Link the parameternode and the input in the target
                        targetController.CreateLink(parameterNode.inputPorts[0],targetAnchor );

                        if (sourceSlotContainer is VFXOperator)
                            (sourceSlotContainer as VFXOperator).ResyncSlots(true);
                        sourceNodeController.ApplyChanges();
                        //Link all the outputs to the matching input of the subgraph
                        foreach (var input in inputs)
                        {
                            sourceController.CreateLink(input, sourceNodeController.outputPorts.First(t => t.model == sourceSlotContainer.outputSlots.Last()));
                        }
                    }

                }

                foreach ( var element in sourceControllers.Where(t=> !(t is VFXDataEdgeController) && !(t is VFXParameterNodeController)))
                {
                    sourceController.RemoveElement(element);
                }

                foreach( var element in parameterNodeControllers)
                {
                    if (element.infos.linkedSlots == null || element.infos.linkedSlots.Count() == 0)
                        sourceController.RemoveElement(element);
                }

            }
            /*catch(System.Exception)
            {
                throw;
            }*/
            //finally
            {
                targetController.useCount--;
                sourceController.useCount--;
            }
        }

    }
}
