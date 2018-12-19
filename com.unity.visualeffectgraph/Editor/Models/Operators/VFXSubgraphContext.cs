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
        protected VisualEffectAsset m_SubAsset;
        
        VFXModel[] m_SubChildren;

        public VisualEffectAsset subAsset
        {
            get { return m_SubAsset; }
        }

        public VFXSubgraphContext():base(VFXContextType.None, VFXDataType.SpawnEvent, VFXDataType.None)
        {
        }
        protected override int inputFlowCount { get { return 3; } }

        public sealed override string name { get { return m_SubAsset!= null ? m_SubAsset.name : "Subgraph"; } }

        protected override IEnumerable<VFXPropertyWithValue> inputProperties
        {
            get {
                if(m_SubChildren == null && m_SubAsset != null) // if the subasset exists but the subchildren has not been recreated yet, return the existing slots
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
            return subAsset != null;
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


        public void RecreateCopy()
        {
            if (m_SubChildren != null)
            {
                foreach (var child in m_SubChildren)
                {
                    if (child != null)
                    {
                        child.onInvalidateDelegate -= SubChildrenOnInvalidate;
                        ScriptableObject.DestroyImmediate(child, true);
                    }
                }
            }

            if (m_SubAsset == null)
            {
                m_SubChildren = null;
                return;
            }

            var graph = m_SubAsset.GetResource().GetOrCreateGraph();
            HashSet<ScriptableObject> dependencies = new HashSet<ScriptableObject>();
            graph.CollectDependencies(dependencies, false);
            m_SubChildren = VFXMemorySerializer.DuplicateObjects(dependencies.ToArray()).OfType<VFXModel>().Where(t => t is VFXContext || t is VFXOperator || t is VFXParameter).ToArray();

            foreach (var child in m_SubChildren)
            {
                child.onInvalidateDelegate += SubChildrenOnInvalidate;

            }
        }

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
                if( cause == InvalidationCause.kSettingChanged && (m_SubAsset != null || object.ReferenceEquals(m_SubAsset,null))) // do not recreate subchildren if the subgraph is not available but is not null
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
