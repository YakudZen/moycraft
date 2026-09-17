using UnityEngine;

namespace VoxelSurvival
{
    public sealed class BlockInteractor : MonoBehaviour
    {
        public VoxelWorld world;
        public FirstPersonPlayer player;
        public Hotbar hotbar;
        public Material outlineMaterial;
        public float reach = 5f;
        public bool HasTarget { get; private set; }
        public Vector3Int Target { get; private set; }
        public float Progress { get; private set; }
        public string Message { get; private set; }
        private Vector3Int miningTarget;
        private BlockId miningId;
        private int miningSlot = -1;
        private float miningTime, messageUntil;
        private Transform outline;
        private Mesh outlineMesh;
        private void Start()
        {
            var go = new GameObject("Block selection"); outline = go.transform;
            go.AddComponent<MeshRenderer>().sharedMaterial = outlineMaterial;
            outlineMesh = new Mesh { name = "Selection wire cube" };
            outlineMesh.vertices = new[] {new Vector3(0,0,0),new(1,0,0),new(1,1,0),new(0,1,0),new(0,0,1),new(1,0,1),new(1,1,1),new(0,1,1)};
            outlineMesh.SetIndices(new[] {0,1,1,2,2,3,3,0,4,5,5,6,6,7,7,4,0,4,1,5,2,6,3,7}, MeshTopology.Lines, 0);
            outlineMesh.RecalculateBounds(); go.AddComponent<MeshFilter>().sharedMesh = outlineMesh;
            outline.localScale = Vector3.one * 1.004f; go.SetActive(false);
        }
        private void Notify(string value) { Message = value; messageUntil = Time.unscaledTime + 2; }
        public bool TryBreak(Vector3Int p)
        {
            if (!world.IsLoaded(p) || Vector3.Distance(player.eyes.transform.position, p + Vector3.one * 0.5f) > reach + 0.87f) return false;
            var block = world.catalog.Get(world.GetBlock(p));
            if (block.id == BlockId.Air || p.y == 0) { Notify("Foundation cannot be mined"); return false; }
            if (!block.CanHarvest(hotbar.Current.tool, hotbar.Current.tier)) { Notify("Requires " + block.preferredTool + " tier " + block.minimumToolTier); return false; }
            if (!hotbar.CanAdd(block.drop)) { Notify("Hotbar is full"); return false; }
            if (!world.SetBlock(p, BlockId.Air)) return false;
            hotbar.Add(block.drop); return true;
        }
        public bool TryPlace(Vector3Int p)
        {
            if (hotbar.Current.Empty || hotbar.Current.IsTool || world.GetBlock(p) != BlockId.Air) return false;
            if (Vector3.Distance(player.eyes.transform.position, p + Vector3.one * 0.5f) > reach + 0.87f) return false;
            if (!world.catalog.Get(hotbar.Current.block).placeable) return false;
            // A slightly inset box allows placement directly under the feet without clipping the capsule.
            var bounds = new Bounds(p + Vector3.one * 0.5f, Vector3.one * 0.998f);
            if (bounds.Intersects(player.Controller.bounds)) { Notify("Cannot place inside the player"); return false; }
            if (!world.SetBlock(p, hotbar.Current.block)) return false;
            hotbar.ConsumeSelected(); return true;
        }
        private void ResetMining() { miningTime = 0; Progress = 0; miningSlot = -1; }
        private void Update()
        {
            if (Time.unscaledTime > messageUntil) Message = "";
            if (!player.CanAct) { HasTarget = false; outline.gameObject.SetActive(false); ResetMining(); return; }
            for (int i = 0; i < Hotbar.Capacity; i++)
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1+i))) hotbar.Select(i);
            if (Input.mouseScrollDelta.y != 0) hotbar.Select(hotbar.Selected - (int)Mathf.Sign(Input.mouseScrollDelta.y));
            var ray = new Ray(player.eyes.transform.position, player.eyes.transform.forward);
            HasTarget = Physics.Raycast(ray, out var hit, reach, ~0, QueryTriggerInteraction.Ignore) && hit.collider.GetComponent<VoxelChunk>() != null;
            outline.gameObject.SetActive(HasTarget);
            if (!HasTarget) { ResetMining(); return; }
            Target = Vector3Int.FloorToInt(hit.point - hit.normal * 0.01f);
            outline.position = (Vector3)Target - Vector3.one * 0.002f;
            if (Input.GetMouseButtonDown(1)) { TryPlace(Vector3Int.FloorToInt(hit.point + hit.normal * 0.01f)); ResetMining(); return; }
            if (!Input.GetMouseButton(0)) { ResetMining(); return; }
            var id = world.GetBlock(Target);
            if (miningTarget != Target || miningSlot != hotbar.Selected || miningId != id)
            { ResetMining(); miningTarget = Target; miningSlot = hotbar.Selected; miningId = id; }
            var definition = world.catalog.Get(id);
            if (!definition.CanHarvest(hotbar.Current.tool, hotbar.Current.tier) || Target.y == 0 || !hotbar.CanAdd(definition.drop))
            { TryBreak(Target); ResetMining(); return; }
            miningTime += Time.deltaTime;
            Progress = Mathf.Clamp01(miningTime / definition.MiningSeconds(hotbar.Current.tool, hotbar.Current.tier));
            if (Progress >= 1) { TryBreak(Target); ResetMining(); }
        }
        private void OnDestroy()
        {
            if (outline != null) Destroy(outline.gameObject);
            if (outlineMesh != null) Destroy(outlineMesh);
        }
    }
}
