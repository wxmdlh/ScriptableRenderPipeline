
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


        void Generate(string shaderFilePath)
        {
            HashSet<string> activeFields = new HashSet<string>();

            var shaderGraph = GraphUtilForVFX.LoadShaderGraph(shaderFilePath);

            string surfaceDescStruct = GraphUtilForVFX.GenerateSurfaceDescriptionStruct(shaderGraph);

            string surfaceDescFunction = GraphUtilForVFX.GenerateSurfaceDescriptionFunction(shaderGraph);
        }
    }
}

#endif
