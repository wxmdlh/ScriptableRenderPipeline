using NUnit.Framework;
using System.Collections;
using UnityEngine.Experimental.Rendering.LWRP;
using UnityEngine.TestTools;

namespace UnityEngine.Rendering.LWRP.Tests
{
    [TestFixture]
    class SingleLight2DTests
    {
        Light2DManager m_LightManager;
        GameObject m_TestObject;

        [SetUp]
        public void Setup()
        {
            m_LightManager = new Light2DManager();
            m_TestObject = new GameObject("Test Object");
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(m_TestObject);
            m_LightManager.Dispose();
        }

        [Test]
        public void SetupCullingSetsBoundingSpheresAndCullingIndices()
        {
            var light = m_TestObject.AddComponent<Light2D>();

            Light2D.SetupCulling(Camera.main);

            Assert.NotNull(Light2DManager.boundingSpheres);
            Assert.AreEqual(1024, Light2DManager.boundingSpheres.Length);
            Assert.AreEqual(0, light.lightCullingIndex);
        }

        [UnityTest]
        public IEnumerator ChangingBlendStyleMovesTheLightToTheCorrectListInLightManager()
        {
            var light = m_TestObject.AddComponent<Light2D>();

            light.blendStyleIndex = 1;

            // LightManager update happens in LateUpdate(). So we must test the result in the next frame.
            yield return null;

            Assert.AreEqual(0, Light2DManager.lights[0].Count);
            Assert.AreSame(light, Light2DManager.lights[1][0]);
        }
    }

    [TestFixture]
    class MultipleLight2DTests
    {
        Light2DManager m_LightManager;
        GameObject m_TestObject1;
        GameObject m_TestObject2;
        GameObject m_TestObject3;

        [SetUp]
        public void Setup()
        {
            m_LightManager = new Light2DManager();
            m_TestObject1 = new GameObject("Test Object 1");
            m_TestObject2 = new GameObject("Test Object 2");
            m_TestObject3 = new GameObject("Test Object 3");
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(m_TestObject3);
            Object.DestroyImmediate(m_TestObject2);
            Object.DestroyImmediate(m_TestObject1);
            m_LightManager.Dispose();
        }

        [UnityTest]
        public IEnumerator LightsAreSortedByLightOrder()
        {
            var light1 = m_TestObject1.AddComponent<Light2D>();
            var light2 = m_TestObject2.AddComponent<Light2D>();
            var light3 = m_TestObject3.AddComponent<Light2D>();

            light1.lightOrder = 1;
            light2.lightOrder = 2;
            light3.lightOrder = 0;

            // Sorting happens in LateUpdate() after light order has changed.
            // So we must test the result in the next frame.
            yield return null;

            Assert.AreSame(light3, Light2DManager.lights[0][0]);
            Assert.AreSame(light1, Light2DManager.lights[0][1]);
            Assert.AreSame(light2, Light2DManager.lights[0][2]);
        }
    }
}
