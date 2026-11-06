using LordsLegacy.Game;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;

namespace LordsLegacy.Tests.PlayMode
{
    public class SunLightControllerTests
    {
        [Test]
        public void SteadyLightKeepsRotationConstantWhileTicksAdvance()
        {
            var hostGo = new GameObject("SimHost");
            var lightGo = new GameObject("Sun");
            try
            {
                var host = hostGo.AddComponent<SimHost>();
                host.AutoUpdate = false;
                host.Initialize(new SimTuning(), SyntheticMap.CreateRamp());
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                var ctl = lightGo.AddComponent<SunLightController>();
                ctl.Configure(host, light);
                ctl.SetViewSettings(new ViewSettings { SteadyLight = true });

                ctl.Apply();
                Quaternion start = light.transform.rotation;
                long t0 = host.World.Tick;
                for (int i = 0; i < 600; i++) { host.Tick(0.1f); ctl.Apply(); }
                Assert.Greater(host.World.Tick, t0);
                Assert.AreEqual(start, light.transform.rotation);
            }
            finally
            {
                Object.DestroyImmediate(lightGo);
                Object.DestroyImmediate(hostGo);
            }
        }
    }
}
