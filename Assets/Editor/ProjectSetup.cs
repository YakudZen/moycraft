using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelSurvival.Editor
{
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/Scenes/Prototype.unity";
        [InitializeOnLoadMethod]
        private static void FirstImport()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (PlayerSettings.productName != "Moycraft")
                {
                    PlayerSettings.productName = "Moycraft";
                    AssetDatabase.SaveAssets();
                }
                if (!File.Exists(ScenePath)) CreateProject();
            };
        }
        [MenuItem("Moycraft/Create or open prototype")]
        public static void CreateProject()
        {
            Directory.CreateDirectory("Assets/Scenes"); Directory.CreateDirectory("Assets/Data/Blocks");
            Directory.CreateDirectory("Assets/Art"); AssetDatabase.Refresh();
            var catalog = AssetDatabase.LoadAssetAtPath<BlockCatalog>("Assets/Data/BlockCatalog.asset");
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<BlockCatalog>();
                var names = Enum.GetNames(typeof(BlockId)); catalog.blocks = new BlockDefinition[names.Length];
                for (int i = 0; i < names.Length; i++)
                {
                    var block = ScriptableObject.CreateInstance<BlockDefinition>();
                    block.id = (BlockId)i; block.displayName = names[i]; block.drop = block.id;
                    block.topTile = block.sideTile = block.bottomTile = Mathf.Min(i+1,15);
                    block.mapColor = Palette(i+1);
                    block.solid = block.opaque = block.placeable = i > 0 && i < 9;
                    block.hardness = 0.5f;
                    if (block.id == BlockId.Grass) { block.topTile=0; block.sideTile=1; block.bottomTile=2; block.drop=BlockId.Dirt; }
                    if (block.id == BlockId.Dirt) block.topTile=block.sideTile=block.bottomTile=2;
                    if (block.id == BlockId.Stone) { block.topTile=block.sideTile=block.bottomTile=3; block.hardness=1.8f; block.minimumToolTier=1; block.preferredTool=ToolKind.Pickaxe; }
                    if (block.id == BlockId.Wood) { block.topTile=block.bottomTile=5; block.sideTile=4; block.hardness=1.2f; block.preferredTool=ToolKind.Axe; block.flammability=0.8f; block.burnSeconds=8; }
                    if (block.id == BlockId.Leaves) { block.topTile=block.sideTile=block.bottomTile=6; block.hardness=0.2f; block.flammability=1; block.burnSeconds=3; }
                    if (block.id == BlockId.Sand) block.topTile=block.sideTile=block.bottomTile=7;
                    if (block.id == BlockId.Gravel) block.topTile=block.sideTile=block.bottomTile=8;
                    if (block.id == BlockId.Ice) { block.topTile=block.sideTile=block.bottomTile=9; block.preferredTool=ToolKind.Pickaxe; block.minimumToolTier=2; block.hardness=1; }
                    if (block.id == BlockId.Grass || block.id == BlockId.Dirt || block.id == BlockId.Sand || block.id == BlockId.Gravel) block.preferredTool=ToolKind.Shovel;
                    AssetDatabase.CreateAsset(block,"Assets/Data/Blocks/"+names[i]+".asset"); catalog.blocks[i]=block;
                }
                AssetDatabase.CreateAsset(catalog,"Assets/Data/BlockCatalog.asset");
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Blocks.mat");
            if (material == null)
            {
                var atlas = new Texture2D(64,64,TextureFormat.RGBA32,false);
                var random = new System.Random(8241);
                for (int tile=0;tile<16;tile++) for (int y=0;y<16;y++) for (int x=0;x<16;x++)
                {
                    Color color=Palette(tile); float noise=(float)random.NextDouble()*0.2f+0.88f;
                    if (tile==1) color=y>=12-(x%3) ? Palette(0)*0.85f : Palette(2);
                    if (tile==4) noise*=x%4==0?0.67f:1f;
                    if (tile==5) noise*=Mathf.Max(Mathf.Abs(x-7),Mathf.Abs(y-7))%3==0?0.70f:1;
                    if (tile==3 || tile==8) noise*=((x/3+y/3)%3==0)?0.87f:1f;
                    if (tile==6) noise*=((x/3+y/2)%3==0)?0.72f:1f;
                    atlas.SetPixel(tile%4*16+x,tile/4*16+y,new Color(color.r*noise,color.g*noise,color.b*noise,1));
                }
                atlas.Apply(); File.WriteAllBytes("Assets/Art/BlockAtlas.png",atlas.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(atlas);
                AssetDatabase.ImportAsset("Assets/Art/BlockAtlas.png");
                var importer=(TextureImporter)AssetImporter.GetAtPath("Assets/Art/BlockAtlas.png");
                importer.filterMode=FilterMode.Point; importer.mipmapEnabled=false; importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.wrapMode=TextureWrapMode.Clamp; importer.SaveAndReimport();
                material=new Material(Shader.Find("Standard")); material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/BlockAtlas.png");
                material.SetFloat("_Glossiness",0); material.SetFloat("_Metallic",0);
                AssetDatabase.CreateAsset(material,"Assets/Art/Blocks.mat");
            }
            var outline=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Outline.mat");
            if (outline==null)
            {
                outline=new Material(Shader.Find("Unlit/Color")); outline.color=new Color(0.91f,1,0.64f);
                AssetDatabase.CreateAsset(outline,"Assets/Art/Outline.mat");
            }
            if (!File.Exists(ScenePath))
            {
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var root=new GameObject("Prototype bootstrap").AddComponent<PrototypeBootstrap>();
                root.catalog=catalog; root.blockMaterial=material; root.outlineMaterial=outline;
                var sun=new GameObject("Sun").AddComponent<Light>(); sun.type=LightType.Directional; sun.intensity=1.15f;
                sun.color=new Color(1,0.94f,0.82f); sun.shadows=LightShadows.Soft; sun.shadowBias=0.03f;
                sun.transform.rotation=Quaternion.Euler(48,-35,0); RenderSettings.sun=sun;
                RenderSettings.ambientMode=AmbientMode.Trilight;
                RenderSettings.ambientSkyColor=new Color(0.65f,0.76f,0.89f);
                RenderSettings.ambientEquatorColor=new Color(0.46f,0.53f,0.46f);
                RenderSettings.ambientGroundColor=new Color(0.25f,0.27f,0.30f);
                RenderSettings.fog=true; RenderSettings.fogMode=FogMode.Linear;
                RenderSettings.fogStartDistance=36; RenderSettings.fogEndDistance=64;
                RenderSettings.fogColor=new Color(0.64f,0.77f,0.85f);
                var sky=new Material(Shader.Find("Skybox/Procedural")); sky.SetFloat("_AtmosphereThickness",0.8f);
                sky.SetColor("_SkyTint",new Color(0.47f,0.58f,0.68f)); sky.SetColor("_GroundColor",RenderSettings.fogColor);
                AssetDatabase.CreateAsset(sky,"Assets/Art/Sky.mat"); RenderSettings.skybox=sky;
                EditorSceneManager.SaveScene(scene,ScenePath);
            }
            else EditorSceneManager.OpenScene(ScenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            PlayerSettings.companyName="YakudZen"; PlayerSettings.productName="Moycraft";
            PlayerSettings.bundleVersion="0.1.0"; PlayerSettings.defaultScreenWidth=1280; PlayerSettings.defaultScreenHeight=720;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed; PlayerSettings.runInBackground=false;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            QualitySettings.vSyncCount=0; QualitySettings.shadowDistance=45; QualitySettings.antiAliasing=2;
            var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var input=settings.FindProperty("activeInputHandler"); if(input!=null) {input.intValue=0; settings.ApplyModifiedPropertiesWithoutUndo();}
            AssetDatabase.SaveAssets();
            Debug.Log("PROJECT_SETUP_OK: " + ScenePath);
        }
        private static Color Palette(int tile) => tile switch
        {
            0=>new Color(0.42f,0.65f,0.25f),1=>new Color(0.48f,0.32f,0.19f),2=>new Color(0.49f,0.33f,0.22f),
            3=>new Color(0.52f,0.55f,0.55f),4=>new Color(0.46f,0.31f,0.17f),5=>new Color(0.67f,0.50f,0.28f),
            6=>new Color(0.29f,0.52f,0.20f),7=>new Color(0.82f,0.75f,0.51f),8=>new Color(0.50f,0.48f,0.43f),
            9=>new Color(0.56f,0.82f,0.91f),10=>new Color(0.19f,0.46f,0.74f),11=>new Color(0.94f,0.32f,0.07f),
            12=>new Color(1,0.64f,0.12f),_=>new Color(0.85f,0.30f,0.65f)
        };
        public static void ValidateAndBuild()
        {
            CreateProject(); PrototypeChecks.Run();
            string output=Environment.GetEnvironmentVariable("VOXEL_BUILD_PATH");
            if(string.IsNullOrEmpty(output)) output="Builds/Windows/Moycraft.exe";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                scenes=new[]{ScenePath},locationPathName=output,target=BuildTarget.StandaloneWindows64,
                options=BuildOptions.Development
            });
            if(report.summary.result!=BuildResult.Succeeded) throw new Exception("Build failed: "+report.summary.result);
            Debug.Log("BUILD_OK: "+output);
        }
    }
}
