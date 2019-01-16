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

        public static void ConvertToSubgraphOperator(VFXView oldView, IEnumerable<Controller> operators,Rect rect)
        {

            VFXViewController oldController = oldView.controller;
            oldController.useCount++;
            try
            {
                object result = VFXCopy.Copy(operators, rect);

                string graphPath = AssetDatabase.GetAssetPath(oldView.controller.model.asset);
                string graphName = Path.GetFileNameWithoutExtension(graphPath);
                string graphDirPath = Path.GetDirectoryName(graphPath);

                string newSubgraphPath = string.Format("{0}/{1}_Subgraph.subvfxoperator", graphDirPath, graphName);
                int cpt = 1;
                while(File.Exists(newSubgraphPath))
                {
                    newSubgraphPath = string.Format("{0}/{1}_Subgraph_{2}.subvfxoperator", graphDirPath, graphName,cpt++);
                }

                VisualEffectSubgraphOperator newSubgraph = VisualEffectResource.CreateNewSubgraphOperator(newSubgraphPath);

                VFXViewWindow.currentWindow.LoadResource(newSubgraph.GetResource());

                VFXView newView = VFXViewWindow.currentWindow.graphView;

                VFXPaste.Paste(newView.controller, rect.center, result, newView, null);
            }
            finally
            {
                oldController.useCount--;
            }
        }

    }
}
