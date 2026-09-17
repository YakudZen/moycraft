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
            GetComponent<MeshRenderer>().sharedMaterial = owner.blockMaterial;
            mesh = new Mesh { name = "Chunk " + data.coordinate, indexFormat = IndexFormat.UInt32 };
            GetComponent<MeshFilter>().sharedMesh = mesh;
            Rebuild();
        }
        public void Rebuild()
        {
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
            if (vertices.Count > 0) GetComponent<MeshCollider>().sharedMesh = mesh;
        }
        private void OnDestroy()
        {
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
        }
    }
}
