using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace VoxelSurvival
{
    // A portable prototype light; block torches and fuel are separate inventory features.
    public sealed class PlayerLantern : MonoBehaviour
    {
        public FirstPersonPlayer player;
        private Light lamp;
        public bool IsOn => lamp != null && lamp.enabled;
        private void Awake()
        {
            lamp = gameObject.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = new Color(1,0.78f,0.46f);
            lamp.intensity = 3; lamp.range = 12;
            lamp.shadows = LightShadows.Soft;
            lamp.shadowBias = 0.05f; lamp.shadowNormalBias = 0;
            lamp.shadowNearPlane = 0.05f;
            var data = gameObject.AddComponent<UniversalAdditionalLightData>();
            data.usePipelineSettings = false;
            lamp.enabled = false;
        }
        private void Update()
        {
            if (player != null && player.CanAct && Input.GetKeyDown(KeyCode.F)) lamp.enabled = !lamp.enabled;
        }
    }
}
