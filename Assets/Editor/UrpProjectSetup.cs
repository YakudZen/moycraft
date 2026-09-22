using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VoxelSurvival.Editor
{
    public static class UrpProjectSetup
    {
        public const string PipelinePath = "Assets/Settings/MoycraftURP.asset";
        public const string RendererPath = "Assets/Settings/MoycraftURP_Renderer.asset";

        // Reopening the project must not reset subsequent manual lighting adjustments.
        public static UniversalRenderPipelineAsset EnsurePipeline()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Settings")) AssetDatabase.CreateFolder("Assets", "Settings");
            var pipeline = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null) pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
                if (renderer == null)
                {
                    renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(renderer, RendererPath);
                }
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
                ConfigureShadows(pipeline);
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            return pipeline;
        }

        public static Shader RequireShader(string name) => Shader.Find(name)
            ?? throw new InvalidOperationException("Required shader missing: " + name + ". Check the Universal RP package.");

        public static void UpgradeMaterials(Material blocks, Material outline)
        {
            if (blocks.shader.name == "Standard" || blocks.shader.name == "Hidden/InternalErrorShader" ||
                blocks.shader.name == "Universal Render Pipeline/Lit")
            {
                string map = blocks.shader.name == "Universal Render Pipeline/Lit" ? "_BaseMap" : "_MainTex";
                string tint = map == "_BaseMap" ? "_BaseColor" : "_Color";
                var texture = blocks.HasProperty(map) ? blocks.GetTexture(map) : null;
                var color = blocks.HasProperty(tint) ? blocks.GetColor(tint) : Color.white;
                var scale = blocks.HasProperty(map) ? blocks.GetTextureScale(map) : Vector2.one;
                var offset = blocks.HasProperty(map) ? blocks.GetTextureOffset(map) : Vector2.zero;
                blocks.shader = RequireShader("Moycraft/Voxel Lit");
                blocks.shaderKeywords = Array.Empty<string>();
                blocks.SetTexture("_BaseMap", texture);
                blocks.SetTextureScale("_BaseMap", scale);
                blocks.SetTextureOffset("_BaseMap", offset);
                blocks.SetColor("_BaseColor", color);
                blocks.SetFloat("_Smoothness", 0);
                blocks.SetFloat("_Metallic", 0);
                EditorUtility.SetDirty(blocks);
            }
            if (outline.shader.name == "Unlit/Color" || outline.shader.name == "Hidden/InternalErrorShader")
            {
                var color = outline.HasProperty("_Color") ? outline.GetColor("_Color") : new Color(0.91f, 1, 0.64f);
                outline.shader = RequireShader("Universal Render Pipeline/Unlit");
                outline.shaderKeywords = Array.Empty<string>();
                outline.SetColor("_BaseColor", color);
                EditorUtility.SetDirty(outline);
            }
        }

        public static void ConfigureShadows(UniversalRenderPipelineAsset pipeline)
        {
            // URP exposes these switches read-only in its public API.
            var serialized = new SerializedObject(pipeline);
            // Compact voxel shade is computed from sky access; only local lamps use shadow maps.
            serialized.FindProperty("m_MainLightShadowsSupported").boolValue = false;
            serialized.FindProperty("m_AdditionalLightsRenderingMode").intValue = 1;
            serialized.FindProperty("m_AdditionalLightShadowsSupported").boolValue = true;
            serialized.FindProperty("m_AnyShadowsSupported").boolValue = true;
            serialized.FindProperty("m_SoftShadowsSupported").boolValue = true;
            serialized.FindProperty("m_SoftShadowQuality").intValue = (int)SoftShadowQuality.Medium;
            serialized.FindProperty("m_SupportsDynamicBatching").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            pipeline.mainLightShadowmapResolution = 2048;
            pipeline.shadowDistance = 64;
            pipeline.shadowCascadeCount = 4;
            // Reserve detail for the five-block interaction range; distant shadows fade into fog.
            pipeline.cascade4Split = new Vector3(0.12f, 0.30f, 0.60f);
            pipeline.cascadeBorder = 0.15f;
            pipeline.shadowDepthBias = 0.5f;
            // Voxel corners share positions but have different face normals. Normal
            // offsets separate those positions in the shadow pass and can leak light.
            // A directional depth offset stays identical across adjoining faces.
            pipeline.shadowNormalBias = 0;
            pipeline.msaaSampleCount = 4;
            pipeline.useSRPBatcher = true;
            EditorUtility.SetDirty(pipeline);
        }

        public static void ConfigureSceneLighting()
        {
            var sun = RenderSettings.sun;
            if (sun == null) throw new InvalidOperationException("Assign the scene's directional Sun before applying lighting defaults.");
            Undo.RecordObject(sun, "Moycraft lighting defaults");
            sun.shadows = LightShadows.None;
            sun.shadowStrength = 1;
            sun.intensity = 1.15f;
            var lightData = sun.GetComponent<UniversalAdditionalLightData>();
            if (lightData == null) lightData = sun.gameObject.AddComponent<UniversalAdditionalLightData>();
            lightData.usePipelineSettings = true;
            lightData.softShadowQuality = SoftShadowQuality.Medium;
            // Keep shaded faces readable without the old, almost daylight-strength ambient fill.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.38f, 0.46f, 0.58f);
            RenderSettings.ambientEquatorColor = new Color(0.23f, 0.27f, 0.30f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.14f, 0.17f);
            EditorSceneManager.MarkSceneDirty(sun.gameObject.scene);
        }

        [MenuItem("Moycraft/Apply URP lighting defaults")]
        public static void ApplyLightingDefaults()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before changing rendering assets.");
            var pipeline = EnsurePipeline();
            ConfigureShadows(pipeline);
            // Quality overrides must get the same treatment, without changing which asset they use.
            for (int i = 0; i < QualitySettings.names.Length; i++)
                if (QualitySettings.GetRenderPipelineAssetAt(i) is UniversalRenderPipelineAsset qualityPipeline && qualityPipeline != pipeline)
                    ConfigureShadows(qualityPipeline);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            ConfigureSceneLighting();
            AssetDatabase.SaveAssets();
            Debug.Log("URP_LIGHTING_DEFAULTS_OK: save the scene to keep its lighting changes.");
        }

        // Explicit migration entry point for batch-mode validation and existing projects.
        public static void UpgradeProject()
        {
            ProjectSetup.CreateProject();
            ApplyLightingDefaults();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }
    }
}
