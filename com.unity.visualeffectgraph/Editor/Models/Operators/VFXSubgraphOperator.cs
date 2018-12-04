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

        public VisualEffectAsset subAsset
        {
            get { return m_SubAsset; }
        }

        public VFXSubgraphOperator()
        {
        }

        public sealed override string name { get { return m_SubAsset!= null ? m_SubAsset.name : "Subgraph"; } }

        protected override IEnumerable<VFXPropertyWithValue> inputProperties
        {
            get {
                foreach ( var param in GetParameters(t=> InputPredicate(t)))
                {
                    yield return new VFXPropertyWithValue(new VFXProperty(param.type, param.exposedName));
                }
            }
        }
        protected override IEnumerable<VFXPropertyWithValue> outputProperties
        {
            get {
                foreach (var param in GetParameters(t => OutputPredicate(t)))
                {
                    yield return new VFXPropertyWithValue(new VFXProperty(param.type, param.exposedName));
                }
            }
        }

        static bool InputPredicate(VFXParameter param)
        {
            return param.exposed && !param.isOutput;
        }

        static bool OutputPredicate(VFXParameter param)
        {
            return param.isOutput;
        }

        IEnumerable<VFXParameter> GetParameters(Func<VFXParameter,bool> predicate)
        {

            if (m_SubAsset == null)
                return Enumerable.Empty<VFXParameter>();
            VFXGraph graph = m_SubAsset.GetResource().GetOrCreateGraph();
            return graph.children.OfType<VFXParameter>().Where(t => predicate(t)).OrderBy(t => t.order);
        }

        public override void CollectDependencies(HashSet<ScriptableObject> objs,bool compileOnly = false)
        {
            base.CollectDependencies(objs,compileOnly);

            if (!compileOnly || m_SubAsset == null)
                return;

            m_SubAsset.GetResource().GetOrCreateGraph().CollectDependencies(objs,true);
        }

        protected override VFXExpression[] BuildExpression(VFXExpression[] inputExpression)
        {
            if (m_SubAsset == null)
                return new VFXExpression[0];
            VFXGraph graph = m_SubAsset.GetResource().GetOrCreateGraph();
            int cptSlot = 0;

            // Change all the inputExpressions of the parameters.
            foreach (var param in GetParameters(t => InputPredicate(t)))
            {
                VFXSlot[] inputSlots = param.outputSlots[0].GetVFXValueTypeSlots().ToArray();
                for(int i = 0; i < inputSlots.Length; ++i)
                {
                    inputSlots[i].SetExpression(inputExpression[cptSlot + i]);
                }

                cptSlot += inputSlots.Length;
            }

            List<VFXExpression> outputExpressions = new List<VFXExpression>();
            foreach (var param in GetParameters(t => OutputPredicate(t)))
            {
                outputExpressions.AddRange(param.inputSlots[0].GetVFXValueTypeSlots().Select(t => t.GetExpression()));
            }

            foreach (var param in GetParameters(t => InputPredicate(t)))
            {
                param.ResetOutputValueExpression();
            }

            return outputExpressions.ToArray();
        }
    }
}
