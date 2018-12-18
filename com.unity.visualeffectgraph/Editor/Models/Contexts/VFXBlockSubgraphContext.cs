using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.VFX;
using UnityEditor.Experimental.VFX;

namespace UnityEditor.VFX
{
    [VFXInfo]
    class VFXBlockSubgraphContext : VFXContext
    {
        public VFXBlockSubgraphContext():base(VFXContextType.kNone, VFXDataType.kNone, VFXDataType.kNone)
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
        VFXContextType m_CompatibleContextType = VFXContextType.kInitAndUpdateAndOutput;


        public VFXContextType compatibleContextType
        {
            get
            {
                return m_CompatibleContextType;
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
            return ((block.compatibleContexts & m_CompatibleContextType) == m_CompatibleContextType);
        }

        public override bool CanBeCompiled()
        {
            return false;
        }
    }
}
