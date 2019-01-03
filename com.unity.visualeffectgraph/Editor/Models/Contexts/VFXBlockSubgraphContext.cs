using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.VFX;
using UnityEditor.Experimental.VFX;

namespace UnityEditor.VFX
{
    class VFXBlockSubgraphContext : VFXContext
    {
        public VFXBlockSubgraphContext():base(VFXContextType.None, VFXDataType.None, VFXDataType.None)
        {
        }
        protected override int inputFlowCount { get { return 0; } }

        public sealed override string name { get { return "Block Subgraph"; } }

        protected override IEnumerable<VFXPropertyWithValue> inputProperties
        {
            get {
                yield break;
            }
        }

        [VFXSetting]
        VFXContextType m_SuitableContexts = VFXContextType.InitAndUpdateAndOutput;


        public VFXContextType compatibleContextType
        {
            get
            {
                return m_SuitableContexts;
            }
        }


        protected override void OnInvalidate(VFXModel model, InvalidationCause cause)
        {
            base.OnInvalidate(model, cause);

            if (cause == InvalidationCause.kSettingChanged)
            {
                //Delete incompatible blocks

                foreach (var block in children.ToList())
                {
                    if (!Accept(block))
                        RemoveChild(block);
                }
            }
        }
        public override bool Accept(VFXBlock block, int index = -1)
        {
            return ((block.compatibleContexts & m_SuitableContexts) == m_SuitableContexts);
        }

        public override bool CanBeCompiled()
        {
            return false;
        }
    }
}
