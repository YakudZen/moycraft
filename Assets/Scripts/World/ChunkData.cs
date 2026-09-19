using UnityEngine;

namespace VoxelSurvival
{
    public sealed class ChunkData
    {
        public const int Size = 16;
        public readonly Vector2Int coordinate;
        // One byte per cell. Definitions and future sparse state live separately.
        private readonly byte[] cells;
        public ChunkData(Vector2Int coordinate, TerrainGenerator generator)
        {
            this.coordinate = coordinate;
            cells = generator.CopyChunk(coordinate);
        }
        public static Vector2Int Coordinate(int x, int z) => new Vector2Int(
            Mathf.FloorToInt(x / (float)Size), Mathf.FloorToInt(z / (float)Size));
        public static int Local(int value) => ((value % Size) + Size) % Size;
        internal static int Index(int x, int y, int z) => x + Size * (z + Size * y);
        public BlockId Get(int x, int y, int z) => (BlockId)cells[Index(x, y, z)];
        public void Set(int x, int y, int z, BlockId id) => cells[Index(x, y, z)] = (byte)id;
    }
}
