
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
        protected string pixelGraphInputStructName = "SurfaceDescriptionInputs";
        protected string pixelGraphOutputStructName = "SurfaceDescription";
        protected string pixelGraphEvalFunctionName = "SurfaceDescriptionFunction";
        string pixelGraphEvalFunctionName = "SurfaceDescriptionFunction";
    }

    class VFXSGLitMeshAdapter : VFXShaderGraphAdapter
    {


        void Generate(string shaderFilePath)
        {
            HashSet<string> activeFields = new HashSet<string>();

            var shaderGraph = GraphUtilForVFX.LoadShaderGraph(shaderFilePath);

            string surfaceDescStruct = GraphUtilForVFX.GenerateSurfaceDescriptionStruct(shaderGraph);

            /*
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
