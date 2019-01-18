using System.Collections;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Experimental.VFX;
using UnityEngine.Experimental.VFX;
using UnityEditor;
using UnityEditor.VFX;
using UnityEditor.VFX.UI;
using UnityEditor.ProjectWindowCallback;

namespace UnityEditor
{
    [InitializeOnLoad]
    public static class VisualEffectAssetEditorUtility
    {
        private static string m_TemplatePath = null;

        public static string templatePath
        {
            get
            {
                if (m_TemplatePath == null)
                {
                    m_TemplatePath = VisualEffectGraphPackageInfo.assetPackagePath + "/Editor/Templates/";
                }
                return m_TemplatePath;
            }
        }


        public const string templateAssetName = "Simple Particle System.vfx";
        public const string templateBlockSubgraphAssetName = "Default Subgraph Block.subvfxblock";
        public const string templateContextSubgraphAssetName = "Default Subgraph Context.subvfxcontext";
        public const string templateOperatorSubgraphAssetName = "Default Subgraph Operator.subvfxoperator";

        [MenuItem("GameObject/Visual Effects/Visual Effect", false, 10)]
        public static void CreateVisualEffectGameObject(MenuCommand menuCommand)
        {
            GameObject go = new GameObject("Visual Effect");
            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
            var vfxComp = go.AddComponent<VisualEffect>();

            if (Selection.activeObject != null && Selection.activeObject is VisualEffectAsset)
            {
                vfxComp.visualEffectAsset = Selection.activeObject as VisualEffectAsset;
                vfxComp.startSeed = (uint)Random.Range(int.MinValue, int.MaxValue);
            }

            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
        }


        public static VisualEffectAsset CreateNewAsset(string path)
        {
            string emptyAsset = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!2058629511 &1\nVisualEffectResource:\n";

            File.WriteAllText(path, emptyAsset);

            AssetDatabase.ImportAsset(path);

            return AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(path);
        }

        [MenuItem("Assets/Create/Visual Effects/Visual Effect Graph", false, 306)]
        public static void CreateVisualEffectAsset()
        {
            string templateString = "";
            try
            {
                templateString = System.IO.File.ReadAllText(templatePath + templateAssetName);
            }
            catch (System.Exception e)
            {
                Debug.LogError("Couldn't read template for new vfx asset : " + e.Message);
                return;
            }

            ProjectWindowUtil.CreateAssetWithContent("New VFX.vfx", templateString,EditorGUIUtility.FindTexture(typeof(VisualEffectAsset)));
        }

        internal class DoCreateNewSubgraphOperator : EndNameEditAction
        {
            public override void Action(int instanceId, string pathName, string resourceFile)
            {
                var sg = VisualEffectResource.CreateNewSubgraphOperator(pathName);
                ProjectWindowUtil.FrameObjectInProjectWindow(sg.GetInstanceID());
            }
        }

        internal class DoCreateNewSubgraphBlock : EndNameEditAction
        {
            public override void Action(int instanceId, string pathName, string resourceFile)
            {
                var sg = VisualEffectResource.CreateNewSubgraphBlock(pathName);
                ProjectWindowUtil.FrameObjectInProjectWindow(sg.GetInstanceID());
            }
        }

        [MenuItem("Assets/Create/Visual Effects/Visual Effect Subgraph Operator", false, 308)]
        public static void CreateVisualEffectSubgraphOperator()
        {
            string fileName = "New VFX Subgraph Operator.subvfxoperator";

            CreateVisualEffectSubgraph<VisualEffectSubgraphOperator, DoCreateNewSubgraphOperator>(fileName, templateOperatorSubgraphAssetName);
        }

        [MenuItem("Assets/Create/Visual Effects/Visual Effect Subgraph Block", false, 309)]
        public static void CreateVisualEffectSubgraphBlock()
        {
            string fileName = "New VFX Subgraph Block.subvfxblock";

            CreateVisualEffectSubgraph<VisualEffectSubgraphBlock, DoCreateNewSubgraphBlock>(fileName, templateBlockSubgraphAssetName);
        }
        public static void CreateVisualEffectSubgraph<T,U>(string fileName,string templateName) where U : EndNameEditAction
        {
            string templateString = "";

            Texture2D texture = EditorGUIUtility.FindTexture(typeof(T));
            try // try with the template
            {
                templateString = System.IO.File.ReadAllText(templatePath + templateName);

                ProjectWindowUtil.CreateAssetWithContent(fileName, templateString,texture);
            }
            catch (System.Exception e)
            {
                Debug.LogError("Couldn't read template for new visual effect subgraph : " + e.Message);
                var action = ScriptableObject.CreateInstance<U>();

                ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, action, fileName, texture, null);

                return;
            }

        }
    }
}
