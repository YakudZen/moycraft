using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace VoxelSurvival.Editor
{
    // Requires a graphics device: do not pass -nographics for these checks.
    public static class RenderingChecks
    {
        public static void CaptureBefore()
        {
            EditorSceneManager.OpenScene(ProjectSetup.ScenePath);
            Capture("before");
        }

        [MenuItem("Moycraft/Capture sun-independent shade (Play mode)")]
        public static void CaptureShadowComparison()
        {
            Require(Application.isPlaying && Camera.main != null, "Run Play mode and face the shadow to compare");
            var light = RenderSettings.sun;
            Require(light != null, "Daylight sun is assigned");
            var rotation = light.transform.rotation;
            const string output = "Builds/RenderingReview";
            Directory.CreateDirectory(output);
            try
            {
                // Synchronous captures keep time, brightness, camera and geometry fixed.
                // The sun disk can move in the sky; shade on voxel surfaces must not.
                light.transform.rotation = Quaternion.Euler(25,-35,0);
                Render(Camera.main, Path.Combine(output,"shade-sun-east.png"));
                light.transform.rotation = Quaternion.Euler(25,145,0);
                Render(Camera.main, Path.Combine(output,"shade-sun-west.png"));
                Debug.Log("COMPACT_SHADE_CAPTURED: compare voxel surfaces in " + Path.GetFullPath(output));
            }
            finally { light.transform.rotation = rotation; }
        }

        public static void UpgradeAndValidate()
        {
            UrpProjectSetup.UpgradeProject();
            Validate();
            Capture("after");
            PrototypeChecks.Run();
            Debug.Log("RENDERING_CHECKS_OK");
        }

        [MenuItem("Moycraft/Validate URP setup")]
        public static void Validate()
        {
            CheckMaterialMigration();
            var pipeline = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            Require(pipeline != null && pipeline.scriptableRenderer != null, "URP has a valid renderer");
            Require(pipeline.additionalLightsRenderingMode == LightRenderingMode.PerPixel && pipeline.supportsAdditionalLightShadows,
                "Portable lights use per-pixel lighting and realtime shadows");
            foreach (string name in new[] { "Blocks", "Outline" })
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/" + name + ".mat");
                Require(material != null && material.shader != null && material.shader.isSupported &&
                    (material.shader.name.StartsWith("Universal Render Pipeline/") || material.shader.name == "Moycraft/Voxel Lit"), name + " uses a supported URP shader");
                ShaderUtil.CompilePass(material,0,true);
                Require(ShaderUtil.GetShaderMessages(material.shader).Length == 0, name + " shader has no compiler messages");
            }
            var blocks = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Blocks.mat");
            Require(blocks.GetTexture("_BaseMap") != null, "Block atlas survives conversion");
            Require(RenderSettings.skybox != null && RenderSettings.skybox.shader.isSupported, "Skybox is supported");
            Require(RenderSettings.sun != null && RenderSettings.sun.type == LightType.Directional, "Daylight sun is assigned");
            Require(!pipeline.supportsMainLightShadows, "Voxel daylight uses compact shade instead of projected sun shadows");
            // Check reopening leaves an artist's tuned pipeline untouched.
            float distance = pipeline.shadowDistance;
            pipeline.shadowDistance = 57;
            try { Require(UrpProjectSetup.EnsurePipeline().shadowDistance == 57, "Opening preserves manual shadow tuning"); }
            finally { pipeline.shadowDistance = distance; EditorUtility.SetDirty(pipeline); AssetDatabase.SaveAssets(); }
        }

        private static void CheckMaterialMigration()
        {
            var blocks = new Material(UrpProjectSetup.RequireShader("Standard"));
            var outline = new Material(UrpProjectSetup.RequireShader("Unlit/Color"));
            var tint = new Color(0.4f,0.6f,0.2f,1);
            try
            {
                blocks.mainTexture = Texture2D.whiteTexture;
                blocks.mainTextureScale = new Vector2(2,3);
                blocks.mainTextureOffset = new Vector2(0.1f,0.2f);
                blocks.color = tint; outline.color = tint;
                UrpProjectSetup.UpgradeMaterials(blocks,outline);
                Require(blocks.mainTexture == Texture2D.whiteTexture && blocks.color == tint &&
                    blocks.mainTextureScale == new Vector2(2,3) && blocks.mainTextureOffset == new Vector2(0.1f,0.2f),
                    "Legacy block conversion preserves texture, UV transform and tint");
                Require(outline.color == tint, "Legacy outline conversion preserves tint");
                blocks.SetFloat("_Smoothness",0.27f);
                UrpProjectSetup.UpgradeMaterials(blocks,outline);
                Require(Mathf.Approximately(blocks.GetFloat("_Smoothness"),0.27f), "Repeated migration preserves URP material tuning");
            }
            finally { Object.DestroyImmediate(blocks); Object.DestroyImmediate(outline); }
        }

        private static void Require(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("RENDER_CHECK_FAILED: " + description);
            Debug.Log("RENDER_PASS: " + description);
        }

        private static void Capture(string suffix)
        {
            string output = "Builds/RenderingReview";
            Directory.CreateDirectory(output);
            var root = new GameObject("Render review world");
            var cameraObject = new GameObject("Render review camera");
            try
            {
                var world = root.AddComponent<VoxelWorld>();
                world.catalog = AssetDatabase.LoadAssetAtPath<BlockCatalog>("Assets/Data/BlockCatalog.asset");
                world.blockMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Blocks.mat");
                world.seed = 17391; world.Initialize();
                for (int z = -3; z <= 3; z++) for (int x = -3; x <= 3; x++) world.LoadChunk(new Vector2Int(x,z));
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 65; camera.nearClipPlane = 0.1f; camera.farClipPlane = 180;
                camera.allowMSAA = true;
                camera.GetUniversalAdditionalCameraData().renderShadows = true;
                int surface = world.Generator.Surface(8,8);
                camera.transform.position = new Vector3(8.5f, surface + 8, -6);
                camera.transform.LookAt(new Vector3(18, surface, 18));
                Render(camera, Path.Combine(output, "terrain-" + suffix + ".png"));

                // A flat block floor, steps, overhang and pillars crossing a chunk seam.
                foreach (var chunk in root.GetComponentsInChildren<VoxelChunk>())
                {
                    int ox = chunk.Data.coordinate.x * ChunkData.Size, oz = chunk.Data.coordinate.y * ChunkData.Size;
                    for (int x = 0; x < ChunkData.Size; x++) for (int z = 0; z < ChunkData.Size; z++)
                    {
                        int wx = ox + x, wz = oz + z;
                        if (wx < 0 || wx > 23 || wz < 0 || wz > 15) continue;
                        for (int y = 44; y < TerrainGenerator.Height; y++)
                        {
                            bool solid = y == 44 || ((wx == 6 || wx == 16) && wz == 7 && y < 49)
                                || (wx >= 10 && wx <= 14 && wz >= 9 && wz <= 11 && y < 45 + wx - 9)
                                || (wx >= 15 && wx <= 19 && wz >= 7 && wz <= 10 && y == 49);
                            chunk.Data.Set(x,y,z,solid ? BlockId.Stone : BlockId.Air);
                        }
                    }
                }
                foreach (var chunk in root.GetComponentsInChildren<VoxelChunk>()) chunk.Rebuild();
                camera.transform.position = new Vector3(10,50,0);
                camera.transform.LookAt(new Vector3(13,45,9));
                Render(camera, Path.Combine(output, "shadows-" + suffix + ".png"));
            }
            finally { Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(root); }
        }

        private static void Render(Camera camera, string path)
        {
            var target = new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var pixels = new Texture2D(1280,720,TextureFormat.RGB24,false);
            var previous = RenderTexture.active;
            try
            {
                target.Create();
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0,0,1280,720),0,0); pixels.Apply();
                File.WriteAllBytes(path,pixels.EncodeToPNG());
                Debug.Log("RENDER_CAPTURE: " + path);
            }
            finally
            {
                RenderTexture.active = previous; target.Release();
                Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            }
        }
    }
}
