using UnityEngine;

namespace VoxelSurvival
{
    public sealed class ChunkData
    {
        public const int Size = 16;
        public readonly Vector2Int coordinate;
        // One byte per cell. Definitions and future sparse state live separately.
        private readonly byte[] cells = new byte[Size * TerrainGenerator.Height * Size];
        public ChunkData(Vector2Int coordinate, TerrainGenerator generator)
        {
            this.coordinate = coordinate;
            for (int z = 0; z < Size; z++)
                for (int x = 0; x < Size; x++)
                {
                    int surface = generator.Surface(coordinate.x * Size + x, coordinate.y * Size + z);
                    for (int y = 0; y <= surface; y++)
                        Set(x, y, z, y == surface ? BlockId.Grass : y >= surface - 3 ? BlockId.Dirt : BlockId.Stone);
                }
        }
        public static Vector2Int Coordinate(int x, int z) => new Vector2Int(
            Mathf.FloorToInt(x / (float)Size), Mathf.FloorToInt(z / (float)Size));
        public static int Local(int value) => ((value % Size) + Size) % Size;
        private static int Index(int x, int y, int z) => x + Size * (z + Size * y);
        public BlockId Get(int x, int y, int z) => (BlockId)cells[Index(x, y, z)];
        public void Set(int x, int y, int z, BlockId id) => cells[Index(x, y, z)] = (byte)id;
    }
}
