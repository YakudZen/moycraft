using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelSurvival
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public sealed class VoxelChunk : MonoBehaviour
    {
        public ChunkData Data { get; private set; }
        private Mesh mesh;
        private VoxelWorld world;
        private readonly List<Vector3Int> lightCells = new();
        private readonly List<Color32> lightColors = new();
        public static readonly Vector3Int[] Neighbors = {
            Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down,
            Vector3Int.forward, Vector3Int.back
        };
        private static readonly Vector3[,] Corners = {
            {new(1,0,0),new(1,1,0),new(1,1,1),new(1,0,1)},
            {new(0,0,1),new(0,1,1),new(0,1,0),new(0,0,0)},
            {new(0,1,1),new(1,1,1),new(1,1,0),new(0,1,0)},
            {new(0,0,0),new(1,0,0),new(1,0,1),new(0,0,1)},
            {new(1,0,1),new(1,1,1),new(0,1,1),new(0,0,1)},
            {new(0,0,0),new(0,1,0),new(1,1,0),new(1,0,0)}
        };
        public void Initialize(VoxelWorld owner, ChunkData data)
        {
            world = owner; Data = data;
            transform.position = new Vector3(data.coordinate.x * ChunkData.Size, 0, data.coordinate.y * ChunkData.Size);
            var renderer = GetComponent<MeshRenderer>();
            renderer.sharedMaterial = owner.blockMaterial;
            // Hidden faces are culled, including faces at chunk boundaries. Cast from
            // either side of the remaining shell without making the visible material two-sided.
            renderer.shadowCastingMode = ShadowCastingMode.TwoSided;
            renderer.receiveShadows = true;
            mesh = new Mesh { name = "Chunk " + data.coordinate, indexFormat = IndexFormat.UInt32 };
            GetComponent<MeshFilter>().sharedMesh = mesh;
            Rebuild();
        }
        public void Rebuild()
        {
            lightCells.Clear();
            var vertices = new List<Vector3>(); var normals = new List<Vector3>();
            var uvs = new List<Vector2>(); var triangles = new List<int>();
            int ox = Data.coordinate.x * ChunkData.Size, oz = Data.coordinate.y * ChunkData.Size;
            for (int y = 0; y < TerrainGenerator.Height; y++)
                for (int z = 0; z < ChunkData.Size; z++)
                    for (int x = 0; x < ChunkData.Size; x++)
                    {
                        var id = Data.Get(x, y, z);
                        if (id == BlockId.Air) continue;
                        var definition = world.catalog.Get(id);
                        // Non-solid fluid and fire rendering belongs to the next milestone.
                        if (!definition.solid) continue;
                        for (int face = 0; face < 6; face++)
                        {
                            var n = Neighbors[face];
                            var neighbor = world.GetBlock(new Vector3Int(ox + x + n.x, y + n.y, oz + z + n.z));
                            if (world.catalog.Get(neighbor).opaque) continue;
                            int first = vertices.Count;
                            lightCells.Add(new Vector3Int(ox+x+n.x,y+n.y,oz+z+n.z));
                            int tile = definition.Tile(face);
                            float u = tile % 4 * 0.25f, v = tile / 4 * 0.25f;
                            const float inset = 0.5f / 64f;
                            for (int i = 0; i < 4; i++)
                            {
                                vertices.Add(new Vector3(x,y,z) + Corners[face,i]); normals.Add(n);
                                uvs.Add(new Vector2(u + (i >= 2 ? 0.25f - inset : inset),
                                    v + (i == 1 || i == 2 ? 0.25f - inset : inset)));
                            }
                            triangles.Add(first); triangles.Add(first+1); triangles.Add(first+2);
                            triangles.Add(first); triangles.Add(first+2); triangles.Add(first+3);
                        }
                    }
            GetComponent<MeshCollider>().sharedMesh = null;
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            RefreshLighting();
            if (vertices.Count > 0) GetComponent<MeshCollider>().sharedMesh = mesh;
        }
        public void RefreshLighting()
        {
            if (mesh == null) return;
            var sky = world.BuildSkylight(Data.coordinate);
            lightColors.Clear();
            foreach (var cell in lightCells)
            {
                byte level = (byte)(sky.Get(cell.x,cell.y,cell.z) * 17);
                // Encode the display curve before quantizing so small openings do not
                // round straight to black in the 8-bit mesh color channel.
                byte ambient = (byte)Mathf.RoundToInt(Mathf.Sqrt(sky.GetAmbient(cell.x,cell.y,cell.z)) * 255);
                var color = new Color32(ambient,level,0,255);
                for (int i = 0; i < 4; i++) lightColors.Add(color);
            }
            // Updating light does not recook the collider or regenerate the geometry.
            mesh.SetColors(lightColors);
        }
        private void OnDestroy()
        {
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
        }
    }
}
