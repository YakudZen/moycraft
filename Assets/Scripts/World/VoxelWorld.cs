using System.Collections.Generic;
using UnityEngine;

namespace VoxelSurvival
{
    public sealed class VoxelWorld : MonoBehaviour, IVoxelRayQuery
    {
        public BlockCatalog catalog;
        public Material blockMaterial;
        public int seed = 17391;
        [Range(1, 6)] public int viewDistance = 3;
        public Transform viewer;
        public TerrainGenerator Generator { get; private set; }
        public int LoadedCount => chunks.Count;
        public int ModifiedCount => edits.Count;
        private readonly Dictionary<Vector2Int, VoxelChunk> chunks = new();
        private readonly Dictionary<Vector3Int, BlockId> edits = new();
        private readonly Dictionary<Vector2Int, Dictionary<Vector3Int, BlockId>> chunkEdits = new();
        private readonly Queue<Vector2Int> pending = new();
        private readonly List<Vector2Int> removal = new();
        private Vector2Int center = new(int.MaxValue, int.MaxValue);
        private int lastDistance;

        public void Initialize()
        {
            if (Generator != null) return;
            catalog.Initialize(); Generator = new TerrainGenerator(seed);
        }
        private void Awake() { if (catalog != null) Initialize(); }
        public bool IsLoaded(Vector3Int position) => chunks.ContainsKey(ChunkData.Coordinate(position.x, position.z));
        public bool TryGetSolid(Vector3Int position, out bool solid)
        {
            solid = false;
            if (!IsLoaded(position)) return false;
            solid = catalog.Get(GetBlock(position)).solid;
            return true;
        }
        public BlockId GetBlock(Vector3Int p)
        {
            if (p.y < 0 || p.y >= TerrainGenerator.Height) return BlockId.Air;
            if (edits.TryGetValue(p, out var changed)) return changed;
            if (chunks.TryGetValue(ChunkData.Coordinate(p.x, p.z), out var chunk))
                return chunk.Data.Get(ChunkData.Local(p.x), p.y, ChunkData.Local(p.z));
            return Generator.Sample(p.x, p.y, p.z);
        }
        public bool SetBlock(Vector3Int p, BlockId id)
        {
            if (p.y <= 0 || p.y >= TerrainGenerator.Height || !IsLoaded(p)) return false;
            if (id != BlockId.Air && !catalog.Get(id).placeable) return false;
            if (GetBlock(p) == id) return false;
            var coord = ChunkData.Coordinate(p.x, p.z);
            if (!chunkEdits.TryGetValue(coord, out var changes)) chunkEdits[coord] = changes = new();
            if (id == Generator.Sample(p.x, p.y, p.z)) { edits.Remove(p); changes.Remove(p); }
            else { edits[p] = id; changes[p] = id; }
            if (changes.Count == 0) chunkEdits.Remove(coord);
            var chunk = chunks[coord];
            chunk.Data.Set(ChunkData.Local(p.x), p.y, ChunkData.Local(p.z), id); chunk.Rebuild();
            int lx = ChunkData.Local(p.x), lz = ChunkData.Local(p.z);
            if (lx == 0) Rebuild(coord + Vector2Int.left);
            if (lx == ChunkData.Size-1) Rebuild(coord + Vector2Int.right);
            if (lz == 0) Rebuild(coord + Vector2Int.down);
            if (lz == ChunkData.Size-1) Rebuild(coord + Vector2Int.up);
            return true;
        }
        private void Rebuild(Vector2Int coord) { if (chunks.TryGetValue(coord, out var chunk)) chunk.Rebuild(); }
        public void LoadChunk(Vector2Int coord)
        {
            if (chunks.ContainsKey(coord)) return;
            var data = new ChunkData(coord, Generator);
            if (chunkEdits.TryGetValue(coord, out var changes))
                foreach (var change in changes)
                    data.Set(ChunkData.Local(change.Key.x), change.Key.y, ChunkData.Local(change.Key.z), change.Value);
            var go = new GameObject("Chunk " + coord.x + ", " + coord.y);
            go.transform.SetParent(transform, false);
            var chunk = go.AddComponent<VoxelChunk>();
            chunks.Add(coord, chunk); chunk.Initialize(this, data);
        }
        public void UnloadChunk(Vector2Int coord)
        {
            if (!chunks.Remove(coord, out var chunk)) return;
            chunk.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(chunk.gameObject); else DestroyImmediate(chunk.gameObject);
        }
        public void LoadSpawn(Vector3 position)
        {
            var coord = ChunkData.Coordinate(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.z));
            for (int z = -1; z <= 1; z++) for (int x = -1; x <= 1; x++) LoadChunk(coord + new Vector2Int(x,z));
        }
        private void Update()
        {
            if (viewer == null) return;
            var next = ChunkData.Coordinate(Mathf.FloorToInt(viewer.position.x), Mathf.FloorToInt(viewer.position.z));
            if (next != center || lastDistance != viewDistance)
            {
                center = next; lastDistance = viewDistance; pending.Clear();
                // Nearest chunks first. Stale work is discarded whenever the viewer crosses a chunk.
                for (int r = 0; r <= viewDistance; r++)
                    for (int z = -r; z <= r; z++) for (int x = -r; x <= r; x++)
                        if (Mathf.Max(Mathf.Abs(x),Mathf.Abs(z)) == r && !chunks.ContainsKey(center + new Vector2Int(x,z)))
                            pending.Enqueue(center + new Vector2Int(x,z));
                removal.Clear();
                foreach (var entry in chunks)
                    if (Mathf.Max(Mathf.Abs(entry.Key.x-center.x),Mathf.Abs(entry.Key.y-center.y)) > viewDistance + 1)
                        removal.Add(entry.Key);
                foreach (var coord in removal) UnloadChunk(coord);
            }
            if (pending.Count > 0) LoadChunk(pending.Dequeue());
        }
    }
}
