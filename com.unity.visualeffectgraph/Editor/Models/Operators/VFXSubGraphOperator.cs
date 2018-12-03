using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.VFX;

namespace UnityEditor.VFX
{
    class VFXSubgraphOperator : VFXOperator
    {
        [VFXSetting,SerializeField]
        protected VisualEffectAsset m_SubAsset;

        public VFXSubgraphOperator()
        {
        }

        public sealed override string name { get { return m_SubAsset!= null ? m_SubAsset.name : "Subgraph"; } }

        protected override IEnumerable<VFXPropertyWithValue> inputProperties
        {
            get {
                if (m_SubAsset == null)
                    yield break;
                VFXGraph graph = m_SubAsset.GetResource().GetOrCreateGraph();

                foreach ( var param in graph.children.OfType<VFXParameter>().Where(t=>t.exposed && !t.isOutput).OrderBy(t=> t.order))
                {
                    yield return new VFXPropertyWithValue(new VFXProperty(param.type, param.exposedName));
                }
            }
        }
        protected override IEnumerable<VFXPropertyWithValue> outputProperties
        {
            get {

                if (m_SubAsset == null)
                    yield break;
                VFXGraph graph = m_SubAsset.GetResource().GetOrCreateGraph();

                foreach (var param in graph.children.OfType<VFXParameter>().Where(t => t.isOutput).OrderBy(t => t.order))
                {
                    yield return new VFXPropertyWithValue(new VFXProperty(param.type, param.exposedName));
                }
            }
        }

        protected override VFXExpression[] BuildExpression(VFXExpression[] inputExpression)
        {
            if (m_SubAsset == null)
                return new VFXExpression[0];
            VFXGraph graph = m_SubAsset.GetResource().GetOrCreateGraph();
            int cptSlot = 0;

            // Change all the inputExpressions of the parameters.
            foreach (var param in graph.children.OfType<VFXParameter>().Where(t => t.exposed && !t.isOutput).OrderBy(t => t.order))
            {
                VFXSlot[] inputSlots = param.outputSlots[0].GetVFXValueTypeSlots().ToArray();
                for(int i = 0; i < inputSlots.Length; ++i)
                {
                    inputSlots[i].SetExpression(inputExpression[cptSlot + i]);
                }

                cptSlot += inputSlots.Length;
            }

            List<VFXExpression> outputExpressions = new List<VFXExpression>();
            foreach (var param in graph.children.OfType<VFXParameter>().Where(t => t.isOutput).OrderBy(t => t.order))
            {
                outputExpressions.AddRange(param.inputSlots[0].GetVFXValueTypeSlots().Select(t => t.GetExpression()));
            }

            foreach (var param in graph.children.OfType<VFXParameter>().Where(t => t.exposed && !t.isOutput).OrderBy(t => t.order))
            {
                param.ResetOutputValueExpression();
            }

            return outputExpressions.ToArray();
        }
    }
}
