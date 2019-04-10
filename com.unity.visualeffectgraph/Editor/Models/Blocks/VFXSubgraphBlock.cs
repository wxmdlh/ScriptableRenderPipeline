
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.VFX;
using UnityEditor.Experimental.VFX;

namespace UnityEditor.VFX
{
    interface IValidable
    {
        bool isValid { get; }
    }
    [VFXInfo(category = "Subgraph Block")]
    class VFXSubgraphBlock : VFXBlock, IValidable
    {
        [VFXSetting(VFXSettingAttribute.VisibleFlags.InInspector), SerializeField]
        protected VisualEffectSubgraphBlock m_Subgraph;
        
        VFXModel[] m_SubChildren;
        VFXBlock[] m_SubBlocks;

        public bool isValid
        {
            get
            {
                if (GetParent() == null) return true; // a bock is invalid only if added to incompatible context.
                return m_Subgraph != null && (m_Subgraph.GetResource().GetOrCreateGraph().children.OfType<VFXBlockSubgraphContext>().First().compatibleContextType & GetParent().contextType) == GetParent().contextType;
            }
        }

        public VisualEffectSubgraphBlock subgraph
        {
            get {

                if(! isValid )
                    return null;

                return m_Subgraph;
            }
        }

        public sealed override string name { get { return subgraph != null ? m_Subgraph.name : "Empty Subgraph Block"; } }

        protected override IEnumerable<VFXPropertyWithValue> inputProperties
        {
            get {
                if(m_SubChildren == null && subgraph != null) // if the subasset exists but the subchildren has not been recreated yet, return the existing slots
                {
                    foreach (var slot in inputSlots)
                    {
                        yield return new VFXPropertyWithValue(slot.property);
                    }
                }

                foreach ( var param in GetParameters(t=> InputPredicate(t)))
                {
                    if (!string.IsNullOrEmpty(param.tooltip))
                        yield return new VFXPropertyWithValue(new VFXProperty(param.type, param.exposedName, new VFXPropertyAttribute(VFXPropertyAttribute.Type.kTooltip, param.tooltip)), param.value);
                    else
                        yield return new VFXPropertyWithValue(new VFXProperty(param.type, param.exposedName), param.value);
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
        public override IEnumerable<VFXAttributeInfo> attributes
        {
            get
            {
                foreach( var block in m_SubBlocks)
                {
                    foreach (var attribute in block.attributes)
                        yield return attribute;
                }
            }
        }
        public override string source
        {
            get
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();

                foreach( var block in m_SubBlocks)
                {
                    sb.AppendLine("{");
                    sb.AppendLine(block.source);
                    sb.AppendLine("}");
                }

                return sb.ToString();
            }
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

            if (subgraph == null)
            {
                m_SubChildren = null;
                m_SubBlocks = null;
                return;
            }

            var graph = m_Subgraph.GetResource().GetOrCreateGraph();
            HashSet<ScriptableObject> dependencies = new HashSet<ScriptableObject>();

            var context = graph.children.OfType<VFXBlockSubgraphContext>().FirstOrDefault();

            if( context == null)
            {
                m_SubChildren = null;
                m_SubBlocks = null;
                return;
            }

            foreach ( var child in graph.children.Where(t=> t is VFXOperator || t is VFXParameter))
            {
                dependencies.Add(child);
                child.CollectDependencies(dependencies, false);
            }

            foreach( var block in context.children)
            {
                dependencies.Add(block);
                block.CollectDependencies(dependencies, false);
            }

            m_SubChildren = VFXMemorySerializer.DuplicateObjects(dependencies.ToArray()).OfType<VFXModel>().Where(t => t is VFXBlock || t is VFXOperator || t is VFXParameter).ToArray();
            m_SubBlocks = m_SubChildren.OfType<VFXBlock>().ToArray();
            foreach (var child in m_SubChildren)
            {
                child.onInvalidateDelegate += SubChildrenOnInvalidate;

            }
            PatchInputExpressions();
        }
        
        void PatchInputExpressions()
        {
            if (m_SubChildren == null) return;

            var inputExpressions = new List<VFXExpression>();

            foreach (var slot in inputSlots.SelectMany(t => t.GetVFXValueTypeSlots()))
            {
                inputExpressions.Add(slot.GetExpression());
            }

            VFXSubgraphUtility.TransferExpressionToParameters(inputExpressions, GetParameters(t => VFXSubgraphUtility.InputPredicate(t)));
        }

        protected override void OnInvalidate(VFXModel model, InvalidationCause cause)
        {
            if (cause == InvalidationCause.kSettingChanged && (subgraph != null || object.ReferenceEquals(m_Subgraph, null)))
            {
                RecreateCopy();
            }

            base.OnInvalidate(model, cause);
        }

        public VFXModel[] subChildren
        {
            get { return m_SubChildren; }
        }
        public VFXBlock[] subBlocks
        {
            get { return m_SubBlocks; }
        }

        public IEnumerable<VFXBlock> recusiveSubBlocks
        {
            get
            {
                return m_SubBlocks == null || !isValid? Enumerable.Empty<VFXBlock>() : (m_SubBlocks.SelectMany(t => t is VFXSubgraphBlock ? (t as VFXSubgraphBlock).recusiveSubBlocks : Enumerable.Repeat(t, 1)));
            }
        }

        public override VFXContextType compatibleContexts { get { return (subgraph != null) ? subgraph.GetResource().GetOrCreateGraph().children.OfType<VFXBlockSubgraphContext>().First().compatibleContextType:VFXContextType.All; } }
        public override VFXDataType compatibleData { get { return (subgraph != null) ? subgraph.GetResource().GetOrCreateGraph().children.OfType<VFXBlockSubgraphContext>().First().ownedType : VFXDataType.Particle | VFXDataType.SpawnEvent; } }

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

        protected override void Invalidate(VFXModel model, InvalidationCause cause)
        {
            if (cause == InvalidationCause.kSettingChanged)
            {
                var graph = GetGraph();

                if (graph != null && subgraph != null && m_Subgraph.GetResource() != null)
                {
                    var otherGraph = m_Subgraph.GetResource().GetOrCreateGraph();
                    if (otherGraph == graph || otherGraph.subgraphDependencies.Contains(graph.GetResource().visualEffectObject))
                        m_Subgraph = null; // prevent cyclic dependencies.
                    if (graph.GetResource().isSubgraph) // BuildSubgraphDependenciesis called for vfx by recompilation, but in subgraph we must call it explicitely
                        graph.BuildSubgraphDependencies();
                }

            }

            base.Invalidate(model, cause);
        }
    }
}
