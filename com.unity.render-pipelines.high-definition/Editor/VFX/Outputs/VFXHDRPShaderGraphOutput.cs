using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Text;
using UnityEditor.VFX;
using UnityEngine.Experimental.VFX;

namespace UnityEditor.Experimental.Rendering.HDPipeline.VFXSG
{
    [VFXInfo]
    class VFXHDRPShaderGraphOutput : VFXAbstractParticleOutput, ISpecificGenerationOutput
    {
        public override string name { get { return "Shader Graph Mesh Output"; } }
        public override string codeGeneratorTemplate { get { return RenderPipeTemplate("VFXParticleLitMesh"); } }
        public override VFXTaskType taskType { get { return VFXTaskType.ParticleMeshOutput; } }
        public override bool supportsUV { get { return true; } }
        public override CullMode defaultCullMode { get { return CullMode.Back; } }

        [VFXSetting(VFXSettingAttribute.VisibleFlags.InGraph), SerializeField]
        protected Shader m_ShaderGraph;

        public Shader shaderGraph
        {
            get { return m_ShaderGraph; }
        }

        public override IEnumerable<VFXAttributeInfo> attributes
        {
            get
            {
                yield return new VFXAttributeInfo(VFXAttribute.Position, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.Color, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.Alpha, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.Alive, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.AxisX, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.AxisY, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.AxisZ, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.AngleX, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.AngleY, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.AngleZ, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.PivotX, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.PivotY, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.PivotZ, VFXAttributeMode.Read);

                yield return new VFXAttributeInfo(VFXAttribute.Size, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.ScaleX, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.ScaleY, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.ScaleZ, VFXAttributeMode.Read);
            }
        }

        public class InputProperties
        {
            [Tooltip("Mesh to be used for particle rendering.")]
            public Mesh mesh = VFXResources.defaultResources.mesh;
            [Tooltip("Define a bitmask to control which submeshes are rendered.")]
            public uint subMeshMask = 0xffffffff;
        }


        static readonly Dictionary<string, Type> s_shaderTypeToType = new Dictionary<string, Type>
        {
            { "Vector1" , typeof(float) },
            { "Vector2", typeof(Vector2) },
            { "Vector3", typeof(Vector3) },
            { "Vector4", typeof(Vector4) },
            { "Color" , typeof(Color) },
            { "Texture2D" , typeof(Texture2D) },
            { "Texture2DArray" , typeof(Texture2DArray) },
            { "Texture3D" , typeof(Texture3D) },
            { "Cubemap" , typeof(Cubemap) },
            { "Bool" , typeof(bool) },
            { "Matrix4" , typeof(Matrix4x4) },
            { "Gradient" , typeof(Gradient) },
        };

        public StringBuilder GenerateShader(ref VFXInfos infos)
        {
            if (shaderGraph == null)
                return null;

            var sb = new StringBuilder();
            sb.Append(VFXSGHDRPShaderGenerator.NewGenerateShader(shaderGraph, ref infos));
            return sb;
        }

        protected override IEnumerable<VFXPropertyWithValue> inputProperties
        {
            get {
                if( shaderGraph != null)
                {
                    var graph = VFXSGHDRPShaderGenerator.LoadShaderGraph(shaderGraph);
                    if( graph != null)
                    {
                        List<string> sgDeclarations = VFXSGHDRPShaderGenerator.GetPropertiesExcept(graph,attributes.Select(t => t.attrib.name).ToList());

                        foreach (var decl in sgDeclarations)
                        {
                            int lastSpace = decl.LastIndexOfAny(new char[] { '\t', ' ' });
                            string variable = decl.Substring(lastSpace + 1);
                            string typeName = decl.Substring(0, lastSpace).Trim();
                            Type type;
                            if(s_shaderTypeToType.TryGetValue(typeName,out type))
                                yield return new VFXPropertyWithValue(new VFXProperty(type, variable));
                        }
                    }

                }


                foreach ( var prop in PropertiesFromType(GetInputPropertiesTypeName()))
                {
                    yield return prop;
                }
        } }
        protected override IEnumerable<VFXNamedExpression> CollectGPUExpressions(IEnumerable<VFXNamedExpression> slotExpressions)
        {
            foreach (var exp in base.CollectGPUExpressions(slotExpressions))
                yield return exp;
            if (shaderGraph != null)
            {
                var graph = VFXSGHDRPShaderGenerator.LoadShaderGraph(shaderGraph);
                if (graph != null)
                {
                    List<string> sgDeclarations = VFXSGHDRPShaderGenerator.GetPropertiesExcept(graph, attributes.Select(t => t.attrib.name).ToList());

                    foreach (var decl in sgDeclarations)
                    {
                        int lastSpace = decl.LastIndexOfAny(new char[] { '\t', ' ' });
                        string variable = decl.Substring(lastSpace + 1);
                        string typeName = decl.Substring(0, lastSpace).Trim();
                        Type type;
                        if (s_shaderTypeToType.TryGetValue(typeName, out type))
                        {
                            VFXNamedExpression expression = slotExpressions.FirstOrDefault(o => o.name == variable);
                            if( expression.exp != null)
                                yield return expression;
                        }   
                    }
                }

            }

        }

        public override VFXExpressionMapper GetExpressionMapper(VFXDeviceTarget target)
        {
            var mapper = base.GetExpressionMapper(target);

            switch (target)
            {
                case VFXDeviceTarget.CPU:
                {
                    mapper.AddExpression(inputSlots.First(s => s.name == "mesh").GetExpression(), "mesh", -1);
                    mapper.AddExpression(inputSlots.First(s => s.name == "subMeshMask").GetExpression(), "subMeshMask", -1);
                    break;
                }
                default:
                {
                    break;
                }
            }

            return mapper;
        }
    }
}
