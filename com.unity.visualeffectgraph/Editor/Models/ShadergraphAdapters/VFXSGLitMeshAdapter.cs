
#if VFX_HAS_SHADERGRAPH
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor.Experimental.VFX;
using UnityEngine.Experimental.VFX;
using UnityEditor.ShaderGraph;

namespace UnityEditor.VFX
{
    class VFXShaderGraphAdapter
    {

    }

    class VFXSGLitMeshAdapter : VFXShaderGraphAdapter
    {


        void Generate()
        {/*
            // build the graph outputs structure, and populate activeFields with the fields of that structure
            .GenerateSurfaceDescriptionStruct(pixelGraphOutputs, pixelSlots, true, pixelGraphOutputStructName, activeFields);

            // Build the graph evaluation code, to evaluate the specified slots
            GraphUtil.GenerateSurfaceDescriptionFunction(
                pixelNodes,
                masterNode,
                masterNode.owner as GraphData,
                pixelGraphEvalFunction,
                functionRegistry,
                sharedProperties,
                pixelRequirements,  // TODO : REMOVE UNUSED
                mode,
                pixelGraphEvalFunctionName,
                pixelGraphOutputStructName,
                null,
                pixelSlots,
                pixelGraphInputStructName);*/
        }
    }
}

#endif
