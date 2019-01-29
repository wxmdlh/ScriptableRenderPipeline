using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor.Experimental.VFX;
using UnityEngine;

namespace UnityEditor.VFX
{
    class VFXSubgraphOperator : VFXOperator
    {
        [VFXSetting,SerializeField]
        protected VisualEffectSubgraphOperator m_Subgraph;

        public VisualEffectSubgraphOperator subgraph
        {
            get { return m_Subgraph; }
        }

        public VFXSubgraphOperator()
        {
        }

        public sealed override string name { get { return m_Subgraph!= null ? m_Subgraph.name : "Subgraph"; } }

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

            if (m_Subgraph == null)
                return Enumerable.Empty<VFXParameter>();
            VFXGraph graph = m_Subgraph.GetResource().GetOrCreateGraph();
            return graph.children.OfType<VFXParameter>().Where(t => predicate(t)).OrderBy(t => t.order);
        }

        public override void CollectDependencies(HashSet<ScriptableObject> objs,bool compileOnly = false)
        {
            base.CollectDependencies(objs,compileOnly);

            if (!compileOnly || m_Subgraph == null)
                return;

            m_Subgraph.GetResource().GetOrCreateGraph().CollectDependencies(objs,true);
        }

        protected override VFXExpression[] BuildExpression(VFXExpression[] inputExpression)
        {
            if (m_Subgraph == null)
                return new VFXExpression[0];
            VFXGraph graph = m_Subgraph.GetResource().GetOrCreateGraph();
            int cptSlot = 0;

            var toInvalidate = new HashSet<VFXSlot>();

            // Change all the inputExpressions of the parameters.
            foreach (var param in GetParameters(t => InputPredicate(t)))
            {
                VFXSlot[] inputSlots = param.outputSlots[0].GetVFXValueTypeSlots().ToArray();

                
                for(int i = 0; i < inputSlots.Length; ++i)
                {
                    inputSlots[i].SetOutExpression(inputExpression[cptSlot + i], toInvalidate);
                    
                }

                cptSlot += inputSlots.Length;
            }
            foreach (var slot in toInvalidate)
            {
                slot.InvalidateExpressionTree();
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
