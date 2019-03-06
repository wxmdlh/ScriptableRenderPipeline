using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor.Experimental.VFX;
using UnityEngine;

namespace UnityEditor.VFX
{
    static class VFXSubgraphUtility
    {
        public static int TransferExpressionToParameters(IList<VFXExpression> inputExpression, IEnumerable<VFXParameter> parameters)
        {
            int cptSlot = 0;
            foreach (var param in parameters)
            {
                VFXSlot[] inputSlots = param.outputSlots[0].GetVFXValueTypeSlots().ToArray();

                param.subgraphMode = true;
                for (int i = 0; i < inputSlots.Length; ++i)
                {
                    if (inputExpression.Count > cptSlot + i)
                    {
                        inputSlots[i].SetExpression(inputExpression[cptSlot + i]);
                    }

                }

                cptSlot += inputSlots.Length;
            }

            return cptSlot;
        }
        public static bool InputPredicate(VFXParameter param)
        {
            return param.exposed && !param.isOutput;
        }

        public static bool OutputPredicate(VFXParameter param)
        {
            return param.isOutput;
        }

        public static IEnumerable<VFXParameter> GetParameters(IEnumerable<VFXModel> models, Func<VFXParameter, bool> predicate)
        {
            return models.OfType<VFXParameter>().Where(t => predicate(t)).OrderBy(t => t.order);
        }
    }

    class VFXSubgraphOperator : VFXOperator
    {
        [VFXSetting, SerializeField]
        protected VisualEffectSubgraphOperator m_Subgraph;

        public VisualEffectSubgraphOperator subgraph
        {
            get { return m_Subgraph; }
        }

        public VFXSubgraphOperator()
        {
        }

        public sealed override string name { get { return m_Subgraph != null ? m_Subgraph.name : "Subgraph"; } }

        protected override IEnumerable<VFXPropertyWithValue> inputProperties
        {
            get {
                foreach (var param in GetParameters(t => VFXSubgraphUtility.InputPredicate(t)))
                {
                    yield return new VFXPropertyWithValue(new VFXProperty(param.type, param.exposedName));
                }
            }
        }
        protected override IEnumerable<VFXPropertyWithValue> outputProperties
        {
            get {
                foreach (var param in GetParameters(t => VFXSubgraphUtility.OutputPredicate(t)))
                {
                    yield return new VFXPropertyWithValue(new VFXProperty(param.type, param.exposedName));
                }
            }
        }

        IEnumerable<VFXParameter> GetParameters(Func<VFXParameter, bool> predicate)
        {
            if (m_Subgraph == null)
                return Enumerable.Empty<VFXParameter>();
            VFXGraph graph = m_Subgraph.GetResource().GetOrCreateGraph();
            return VFXSubgraphUtility.GetParameters(graph.children,predicate);
        }

        public override void CollectDependencies(HashSet<ScriptableObject> objs, bool compileOnly = false)
        {
            base.CollectDependencies(objs, compileOnly);

            if (!compileOnly || m_Subgraph == null)
                return;

            m_Subgraph.GetResource().GetOrCreateGraph().CollectDependencies(objs, true);
        }

        protected override VFXExpression[] BuildExpression(VFXExpression[] inputExpression)
        {
            if (m_Subgraph == null)
                return new VFXExpression[0];
            VFXGraph graph = m_Subgraph.GetResource().GetOrCreateGraph();

            // Change all the inputExpressions of the parameters.
            var parameters = GetParameters(t => VFXSubgraphUtility.InputPredicate(t));
            VFXSubgraphUtility.TransferExpressionToParameters(inputExpression, parameters);

            List<VFXExpression> outputExpressions = new List<VFXExpression>();
            foreach (var param in GetParameters(t => VFXSubgraphUtility.OutputPredicate(t)))
            {
                outputExpressions.AddRange(param.inputSlots[0].GetVFXValueTypeSlots().Select(t => t.GetExpression()));
            }

            foreach (var param in GetParameters(t => VFXSubgraphUtility.InputPredicate(t)))
            {
                param.ResetOutputValueExpression();
            }

            return outputExpressions.ToArray();
        }
    }
}
