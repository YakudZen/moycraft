using System;
using UnityEngine;

namespace VoxelSurvival
{
    public sealed class DaylightCycle : MonoBehaviour
    {
        [Min(30)] public float cycleSeconds = 1200;
        [Range(0,1)] public float timeOfDay = 0.3833333f;
        public bool running = true;
        private Light sun;
        private LightShadows originalShadows;
        private Quaternion originalRotation;
        private Color originalColor, originalFog;
        private float originalIntensity, exposure;
        private Material originalSky, runtimeSky;
        private static readonly int NightBlend = Shader.PropertyToID("_MoycraftNightBlend");

        // Kept independent of Unity native calls for deterministic boundary tests.
        public static float WrapTime(double time) => (float)(time - Math.Floor(time));
        public static float Daylight(float time)
        {
            double elevation = Math.Sin((WrapTime(time)-0.25) * Math.PI * 2);
            double t = Math.Max(0,Math.Min(1,elevation/0.25));
            return (float)(t*t*(3-2*t));
        }

        private void Awake()
        {
            sun = RenderSettings.sun;
            if (sun == null) { enabled = false; return; }
            originalRotation = sun.transform.rotation;
            originalShadows = sun.shadows;
            sun.shadows = LightShadows.None;
            originalColor = sun.color; originalIntensity = sun.intensity;
            originalFog = RenderSettings.fogColor;
            originalSky = RenderSettings.skybox;
            if (originalSky != null)
            {
                runtimeSky = new Material(originalSky);
                if (runtimeSky.HasProperty("_Exposure")) exposure = runtimeSky.GetFloat("_Exposure");
                RenderSettings.skybox = runtimeSky;
            }
            Apply();
        }

        private void Update()
        {
            if (running) timeOfDay = WrapTime(timeOfDay + Time.deltaTime / Math.Max(30,cycleSeconds));
            Apply();
        }

        private void Apply()
        {
            if (sun == null) return;
            float daylight = Daylight(timeOfDay);
            sun.transform.rotation = Quaternion.Euler((timeOfDay-0.25f)*360,-35,0);
            sun.intensity = originalIntensity * daylight;
            sun.color = Color.Lerp(new Color(1,0.48f,0.22f),originalColor,daylight);
            Shader.SetGlobalFloat(NightBlend,1-daylight);
            RenderSettings.fogColor = Color.Lerp(new Color(0.002f,0.004f,0.01f),originalFog,daylight);
            if (runtimeSky != null && runtimeSky.HasProperty("_Exposure"))
                runtimeSky.SetFloat("_Exposure",Mathf.Lerp(0.025f,exposure,daylight));
        }

        private void OnDestroy()
        {
            Shader.SetGlobalFloat(NightBlend,0);
            if (sun != null)
            {
                sun.transform.rotation = originalRotation;
                sun.shadows = originalShadows;
                sun.intensity = originalIntensity; sun.color = originalColor;
            }
            if (runtimeSky != null)
            {
                RenderSettings.skybox = originalSky;
                Destroy(runtimeSky);
            }
            if (sun != null) RenderSettings.fogColor = originalFog;
        }
    }
}
