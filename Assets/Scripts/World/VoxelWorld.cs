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
        private readonly Queue<Vector2Int> pendingLight = new();
        private readonly HashSet<Vector2Int> dirtyLight = new();
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
            // A roof changes its whole column; sideways propagation reaches at most 14 cells.
            for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
            {
                var affected = coord + new Vector2Int(dx,dz);
                bool rebuilt = (dx == 0 && dz == 0) ||
                    (dz == 0 && ((dx == -1 && lx == 0) || (dx == 1 && lx == ChunkData.Size-1))) ||
                    (dx == 0 && ((dz == -1 && lz == 0) || (dz == 1 && lz == ChunkData.Size-1)));
                if (!rebuilt && chunks.ContainsKey(affected) && dirtyLight.Add(affected)) pendingLight.Enqueue(affected);
            }
            return true;
        }
        private void Rebuild(Vector2Int coord) { if (chunks.TryGetValue(coord, out var chunk)) chunk.Rebuild(); }
        public VoxelSkylight BuildSkylight(Vector2Int coordinate)
        {
            // Resolve nine chunk references once instead of doing dictionary/catalog lookups
            // for every cell of the padded lighting volume.
            var data = new ChunkData[9];
            for (int z = -1; z <= 1; z++) for (int x = -1; x <= 1; x++)
            {
                var coord = coordinate + new Vector2Int(x,z);
                data[x+1+3*(z+1)] = chunks.TryGetValue(coord,out var chunk) ? chunk.Data : ReadChunkData(coord);
            }
            var opaque = new bool[256];
            foreach (var definition in catalog.blocks) opaque[(byte)definition.id] = definition.opaque;
            int ox = coordinate.x * ChunkData.Size, oz = coordinate.y * ChunkData.Size;
            return new VoxelSkylight(ox,oz,ChunkData.Size,TerrainGenerator.Height,(x,y,z) =>
            {
                int sx = x-ox+ChunkData.Size, sz = z-oz+ChunkData.Size;
                return opaque[(byte)data[sx/ChunkData.Size+3*(sz/ChunkData.Size)].Get(sx%ChunkData.Size,y,sz%ChunkData.Size)];
            });
        }

        private ChunkData ReadChunkData(Vector2Int coord)
        {
            var data = new ChunkData(coord, Generator);
            if (chunkEdits.TryGetValue(coord, out var changes))
                foreach (var change in changes)
                    data.Set(ChunkData.Local(change.Key.x), change.Key.y, ChunkData.Local(change.Key.z), change.Value);
            return data;
        }
        public void LoadChunk(Vector2Int coord)
        {
            if (chunks.ContainsKey(coord)) return;
            var data = ReadChunkData(coord);
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
            // Budget one neighboring light update per frame; the edited chunk updates immediately.
            while (pendingLight.Count > 0)
            {
                var coord = pendingLight.Dequeue(); dirtyLight.Remove(coord);
                if (!chunks.TryGetValue(coord,out var chunk)) continue;
                chunk.RefreshLighting(); break;
            }
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
