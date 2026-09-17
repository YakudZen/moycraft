using UnityEngine;

namespace VoxelSurvival
{
    public sealed class PrototypeBootstrap : MonoBehaviour
    {
        public BlockCatalog catalog;
        public Material blockMaterial;
        public Material outlineMaterial;
        public int seed = 17391;
        [Range(1,6)] public int viewDistance = 3;
        public VoxelWorld World { get; private set; }
        public FirstPersonPlayer Player { get; private set; }
        public BlockInteractor Interaction { get; private set; }
        public Hotbar Inventory { get; private set; }
        private void Awake()
        {
            Application.targetFrameRate = 120;
            World = new GameObject("Voxel world").AddComponent<VoxelWorld>();
            // AddComponent invokes Awake immediately, so Initialize is deferred until configured.
            World.catalog = catalog; World.blockMaterial = blockMaterial; World.seed = seed; World.viewDistance = viewDistance;
            World.Initialize();
            var spawn = new Vector3(8.5f,World.Generator.Surface(8,8)+1.1f,8.5f);
            World.LoadSpawn(spawn);
            var go = new GameObject("Player");
            var controller = go.AddComponent<CharacterController>();
            controller.height = 1.8f; controller.radius = 0.3f; controller.center = new Vector3(0,0.9f,0);
            controller.stepOffset = 0.3f; controller.skinWidth = 0.03f; controller.minMoveDistance = 0;
            Player = go.AddComponent<FirstPersonPlayer>(); Player.world = World;
            var cameraGo = new GameObject("First person camera"); cameraGo.tag = "MainCamera";
            cameraGo.transform.SetParent(go.transform,false); cameraGo.transform.localPosition = new Vector3(0,1.62f,0);
            Player.eyes = cameraGo.AddComponent<Camera>(); Player.eyes.nearClipPlane = 0.04f;
            Player.eyes.farClipPlane = 180; Player.eyes.fieldOfView = 75; cameraGo.AddComponent<AudioListener>();
            Inventory = go.AddComponent<Hotbar>();
            Interaction = go.AddComponent<BlockInteractor>(); Interaction.world = World; Interaction.player = Player;
            Interaction.hotbar = Inventory; Interaction.outlineMaterial = outlineMaterial;
            var hud = go.AddComponent<PrototypeHud>(); hud.world = World; hud.player = Player; hud.hotbar = Inventory; hud.interaction = Interaction;
            World.viewer = go.transform; Player.Initialize(spawn);
            go.transform.rotation = Quaternion.Euler(0,35,0);
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-smokeTests") >= 0)
                gameObject.AddComponent<RuntimeSmokeTests>();
        }
    }
}
