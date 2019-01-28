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
                            if (parameterNodeControllers.Contains(t.output.sourceNode))
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

                        var linkedParameter = outputs.FirstOrDefault(t => t.sourceNode is VFXParameterNodeController);
                        if( linkedParameter != null)
                            newTargetParamController.exposedName = (linkedParameter.sourceNode as VFXParameterNodeController).parentController.exposedName;
                        else
                            newTargetParamController.exposedName = newSourceInputs[i].name;

                        //first the equivalent of sourceInput in the target

                        VFXNodeController targetNode = null;

                        if(newSourceInputs[i].sourceNode is VFXBlockController)
                        {
                            var blockController = newSourceInputs[i].sourceNode as VFXBlockController;
                            
                            var targetContext = targetControllers[sourceControllers.IndexOf(blockController.contextController)] as VFXContextController;

                            targetNode = targetContext.blockControllers[blockController.index];
                        }
                        else
                            targetNode = targetControllers[sourceControllers.IndexOf(newSourceInputs[i].sourceNode)];

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

                        var linkedParameter = inputs.FirstOrDefault(t => t.sourceNode is VFXParameterNodeController);
                        if (linkedParameter != null)
                            newTargetParamController.exposedName = (linkedParameter.sourceNode as VFXParameterNodeController).parentController.exposedName;
                        else
                            newTargetParamController.exposedName = newSourceOutputs[i].name;

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
                else if( type == Type.Context)
                {
                    var initializeContexts = sourceControllers.OfType<VFXContextController>().Where(t => t.model.contextType == VFXContextType.Init ||
                                                                                                        t.model.contextType == VFXContextType.Spawner ||
                                                                                                        t.model.contextType == VFXContextType.Subgraph).ToArray();

                    var outputSpawners = new Dictionary<VFXContextController, List<VFXFlowAnchorController>>();
                    var outputEvents = new Dictionary<string, List<VFXFlowAnchorController>>();

                    foreach ( var initializeContext in initializeContexts)
                    {
                        for(int i = 0; i < initializeContext.flowInputAnchors.Count; ++i)
                        if( initializeContext.flowInputAnchors[i].connections.Count() > 0)
                        {

                            var outputContext = initializeContext.flowInputAnchors[i].connections.First().output.context; //output context must be linked through is it is linked with a spawner

                            if (!sourceControllers.Contains(outputContext))
                            {
                                if (outputContext.model.contextType == VFXContextType.Spawner /*||
                                    ((outputContext.model is VFXBasicEvent) &&
                                        (new string[] { VisualEffectAsset.PlayEventName, VisualEffectAsset.StopEventName }.Contains((outputContext.model as VFXBasicEvent).eventName) ||
                                            sourceController.model.isSubgraph && (outputContext.model as VFXBasicEvent).eventName == VFXSubgraphContext.triggerEventName))*/)
                                {
                                    List<VFXFlowAnchorController> inputs = null;
                                    if (!outputSpawners.TryGetValue(outputContext, out inputs))
                                    {
                                        inputs = new List<VFXFlowAnchorController>();
                                        outputSpawners.Add(outputContext, inputs);
                                    }
                                    inputs.Add(initializeContext.flowInputAnchors[i]);
                                }
                                else if(outputContext.model is VFXBasicEvent)
                                {
                                    List<VFXFlowAnchorController> inputs = null;
                                    var eventName = (outputContext.model as VFXBasicEvent).eventName;
                                    if (!outputEvents.TryGetValue(eventName, out inputs))
                                    {
                                        inputs = new List<VFXFlowAnchorController>();
                                            outputEvents.Add(eventName, inputs);
                                    }
                                    inputs.Add(initializeContext.flowInputAnchors[i]);
                                }
                            }
                        }
                    }

                    {

                        if (outputSpawners.Count() > 1)
                        {
                            Debug.LogWarning("More than one spawner is linked to the content if the new subgraph, some links we not be kept");
                        }

                        if(outputSpawners.Count > 0)
                        {
                            var kvContext = outputSpawners.First();

                            (sourceNodeController as VFXContextController).model.LinkFrom(kvContext.Key.model, 0, 2); // linking to trigger
                            CreateAndLinkEvent(sourceControllers, targetController, targetControllers, kvContext.Value,VFXSubgraphContext.triggerEventName);
                        }
                    }
                    { //link named events as if
                        foreach( var kv in outputEvents)
                        {
                            CreateAndLinkEvent(sourceControllers, targetController, targetControllers, kv.Value, kv.Key);
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

        private static void CreateAndLinkEvent(List<Controller> sourceControllers, VFXViewController targetController, List<VFXNodeController> targetControllers, List<VFXFlowAnchorController> inputs, string eventName)
        {
            var triggerEvent = VFXBasicEvent.CreateInstance<VFXBasicEvent>();
            triggerEvent.eventName = eventName;

            targetController.graph.AddChild(triggerEvent);

            float xMiddle = 0;
            float yMin = Mathf.Infinity;

            foreach (var edge in inputs)
            {
                var targetContext = targetControllers[sourceControllers.IndexOf(edge.context)] as VFXContextController;

                var targetInputLink = edge.slotIndex;

                triggerEvent.LinkTo(targetContext.model, 0, targetInputLink);
                xMiddle += targetContext.position.x;

                if (targetContext.position.y < yMin)
                    yMin = targetContext.position.y;
            }

            triggerEvent.position = new Vector2(xMiddle / inputs.Count, yMin) - new Vector2(0, 200); // place the event above the top center of the linked contexts.
        }
    }
}
