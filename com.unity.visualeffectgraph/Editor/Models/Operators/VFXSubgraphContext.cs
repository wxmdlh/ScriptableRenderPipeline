using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.VFX;
using UnityEditor.Experimental.VFX;

namespace UnityEditor.VFX
{
    /*
    class VFXShadowContext : VFXContext
    {
        VFXSubgraphContext m_SubGraph;
        VFXContext m_Context;

        VFXExpressionMapper m_CPUExpressionMapper;
        VFXExpressionMapper m_GPUExpressionMapper;

        public VFXShadowContext():base(VFXContextType.kNone,VFXDataType.kNone, VFXDataType.kNone)
        { }

        public void Create(VFXSubgraphContext subGraph,VFXContext context)
        {
            m_SubGraph = subGraph;
            m_Context = context;

            m_CPUExpressionMapper = m_Context.GetExpressionMapper(VFXDeviceTarget.CPU);
            m_GPUExpressionMapper = m_Context.GetExpressionMapper(VFXDeviceTarget.GPU);
        }
        public override bool CanBeCompiled()
        {
            return true;
        }

        public override VFXExpressionMapper GetExpressionMapper(VFXDeviceTarget target)
        {
            if (target == VFXDeviceTarget.GPU)
                return m_GPUExpressionMapper;

            return m_CPUExpressionMapper;
        }
    }*/
    
    class VFXSubgraphContext : VFXContext
    {
        [VFXSetting,SerializeField]
        protected VisualEffectAsset m_SubAsset;

        ScriptableObject[] m_SubChildren;

        public VisualEffectAsset subAsset
        {
            get { return m_SubAsset; }
        }

        public VFXSubgraphContext():base(VFXContextType.kNone, VFXDataType.kSpawnEvent, VFXDataType.kNone)
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

        public override VFXExpressionMapper GetExpressionMapper(VFXDeviceTarget target)
        {
            return null;
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
            return m_SubChildren.OfType<VFXParameter>().Where(t => predicate(t)).OrderBy(t => t.order);
        }

        private new void OnEnable()
        {
            base.OnEnable();

            OnInvalidate(this, InvalidationCause.kSettingChanged);
        }

        protected override void OnInvalidate(VFXModel model, InvalidationCause cause)
        {
            if( cause == InvalidationCause.kSettingChanged || cause == InvalidationCause.kConnectionChanged)
            {
                if( cause == InvalidationCause.kSettingChanged )
                {
                    if (m_SubChildren != null)
                    {
                        foreach (var context in m_SubChildren)
                            ScriptableObject.DestroyImmediate(context);
                    }

                    if (m_SubAsset == null) return;

                    var graph = m_SubAsset.GetResource().GetOrCreateGraph();
                    HashSet<ScriptableObject> dependencies = new HashSet<ScriptableObject>();
                    graph.CollectDependencies(dependencies, true);
                    m_SubChildren = VFXMemorySerializer.DuplicateObjects(dependencies.ToArray());
                }
                if (m_SubAsset == null) return;

                var toInvalidate = new HashSet<VFXSlot>();

                 var inputExpressions = new List<VFXExpression>();

                 foreach(var slot in inputSlots.SelectMany(t=>t.GetVFXValueTypeSlots()))
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
                        inputSlots[i].SetOutExpression(inputExpressions[cptSlot + i], toInvalidate);
                    }

                    cptSlot += inputSlots.Length;
                }
                foreach (var slot in toInvalidate)
                {
                    slot.InvalidateExpressionTree();
                }
            }
        }

        public override void CollectDependencies(HashSet<ScriptableObject> objs,bool compileOnly = false)
        {
            base.CollectDependencies(objs,compileOnly);

            if (!compileOnly || m_SubAsset == null)
                return;

            foreach (var child in m_SubChildren)
            {
                if( ! (child is VFXParameter))
                {
                    objs.Add(child);

                    if (child is VFXModel)
                        (child as VFXModel).CollectDependencies(objs, true);
                }
            }
        }
    }
}
