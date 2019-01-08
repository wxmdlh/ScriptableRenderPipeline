using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.VFX;
using UnityEditor.Experimental.VFX;

namespace UnityEditor.VFX
{   
    class VFXSubgraphContext : VFXContext
    {
        public const string triggerEventName = "Trigger";

        [VFXSetting,SerializeField]
        protected VisualEffectSubgraphContext m_SubGraph;
        
        VFXModel[] m_SubChildren;

        public VisualEffectSubgraphContext subGraph
        {
            get { return m_SubGraph; }
        }

        public VFXSubgraphContext():base(VFXContextType.None, VFXDataType.SpawnEvent, VFXDataType.None)
        {
        }
        protected override int inputFlowCount { get { return 3; } }

        public sealed override string name { get { return m_SubGraph!= null ? m_SubGraph.name : "Subgraph"; } }

        protected override IEnumerable<VFXPropertyWithValue> inputProperties
        {
            get {
                if(m_SubChildren == null && m_SubGraph != null) // if the subasset exists but the subchildren has not been recreated yet, return the existing slots
                {
                    foreach (var slot in inputSlots)
                    {
                        yield return new VFXPropertyWithValue(slot.property);
                    }
                }

                foreach ( var param in GetParameters(t=> InputPredicate(t)))
                {
                    yield return new VFXPropertyWithValue(new VFXProperty(param.type, param.exposedName));
                }
            }
        }

        public override VFXExpressionMapper GetExpressionMapper(VFXDeviceTarget target)
        {
            PatchInputExpressions();
            return null;
        }

        public override bool CanBeCompiled()
        {
            return subGraph != null;
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
            if (m_SubChildren == null) return Enumerable.Empty<VFXParameter>();
            return m_SubChildren.OfType<VFXParameter>().Where(t => predicate(t)).OrderBy(t => t.order);
        }

        private new void OnEnable()
        {
            base.OnEnable();
            RecreateCopy();
        }

        void SubChildrenOnInvalidate(VFXModel model, InvalidationCause cause)
        {
            Invalidate(this, cause);
        }


        private void OnDisable()
        {
            DetachFromOriginal();
        }


        public void RecreateCopy()
        {
            DetachFromOriginal();

            if (m_SubGraph == null)
            {
                m_SubChildren = null;
                return;
            }

            var graph = m_SubGraph.GetResource().GetOrCreateGraph();
            HashSet<ScriptableObject> dependencies = new HashSet<ScriptableObject>();
            graph.CollectDependencies(dependencies, false);

            var duplicated = VFXMemorySerializer.DuplicateObjects(dependencies.ToArray());
            m_SubChildren = duplicated.OfType<VFXModel>().Where(t => t is VFXContext || t is VFXOperator || t is VFXParameter).ToArray();

            foreach (var child in duplicated.Zip(dependencies, (a, b) => new { copy = a, original = b }))
            {
                if (child.copy is VFXSlot)
                {
                    var original = child.original as VFXSlot;
                    var copy = child.copy as VFXSlot;
                    if (original.direction == VFXSlot.Direction.kInput || original.owner is VFXParameter)
                    {
                        m_OriginalToCopy[original] = copy;
                        original.onInvalidateDelegate += OnOriginalSlotModified;
                    }
                }
            }
        }

        private void DetachFromOriginal()
        {
            if (m_SubChildren != null)
            {
                HashSet<VFXData> datas = new HashSet<VFXData>();
                foreach (var child in m_SubChildren)
                {
                    if (child != null)
                    {
                        child.onInvalidateDelegate -= SubChildrenOnInvalidate;
                        if (child is VFXContext)
                        {
                            datas.Add((child as VFXContext).GetData());
                        }
                        ScriptableObject.DestroyImmediate(child, true);
                    }
                }
                foreach (var data in datas)
                {
                    ScriptableObject.DestroyImmediate(data, true);
                }

                foreach (var kv in m_OriginalToCopy)
                {
                    kv.Key.onInvalidateDelegate -= OnOriginalSlotModified;
                }
                m_OriginalToCopy.Clear();
            }
        }

        public void OnOriginalSlotModified(VFXModel original,InvalidationCause cause)
        {
            if (cause == InvalidationCause.kParamChanged)
            {
                m_OriginalToCopy[original as VFXSlot].value = (original as VFXSlot).value;
                Invalidate(InvalidationCause.kParamChanged);
            }
        }

        Dictionary<VFXSlot, VFXSlot> m_OriginalToCopy = new Dictionary<VFXSlot, VFXSlot>();

        void PatchInputExpressions()
        {
            if (m_SubChildren == null) return;

            var toInvalidate = new HashSet<VFXSlot>();

            var inputExpressions = new List<VFXExpression>();

            foreach (var slot in inputSlots.SelectMany(t => t.GetVFXValueTypeSlots()))
            {
                inputExpressions.Add(slot.GetExpression());
            }

            int cptSlot = 0;
            // Change all the inputExpressions of the parameters.
            foreach (var param in GetParameters(t => InputPredicate(t)))
            {
                VFXSlot[] inputSlots = param.outputSlots[0].GetVFXValueTypeSlots().ToArray();

                for (int i = 0; i < inputSlots.Length; ++i)
                {
                    if (inputExpressions.Count() <= cptSlot + i) break;
                    inputSlots[i].SetOutExpression(inputExpressions[cptSlot + i], toInvalidate);
                }

                cptSlot += inputSlots.Length;
            }
            foreach (var slot in toInvalidate)
            {
                slot.InvalidateExpressionTree();
            }
        }

        protected override void OnInvalidate(VFXModel model, InvalidationCause cause)
        {
            if( cause == InvalidationCause.kSettingChanged || cause == InvalidationCause.kExpressionInvalidated)
            {
                if( cause == InvalidationCause.kSettingChanged && (m_SubGraph != null || object.ReferenceEquals(m_SubGraph,null))) // do not recreate subchildren if the subgraph is not available but is not null
                {
                    RecreateCopy();
                }

                base.OnInvalidate(model, cause);
                PatchInputExpressions();
            }
            else
            {
                base.OnInvalidate(model, cause);
            }
        }

        public VFXModel[] subChildren
        {
            get { return m_SubChildren; }
        }

        public override void CollectDependencies(HashSet<ScriptableObject> objs,bool compileOnly = false)
        {
            base.CollectDependencies(objs,compileOnly);

            if (m_SubChildren == null || ! compileOnly)
                return;

            foreach (var child in m_SubChildren)
            {
                if( ! (child is VFXParameter) )
                {
                    objs.Add(child);

                    if (child is VFXModel)
                        (child as VFXModel).CollectDependencies(objs, true);
                }
            }
        }
    }
}
