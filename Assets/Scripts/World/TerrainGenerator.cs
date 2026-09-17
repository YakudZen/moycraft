using UnityEngine;

namespace VoxelSurvival
{
    public sealed class TerrainGenerator
    {
        public const int Height = 64;
        private readonly float offsetX, offsetZ;
        public TerrainGenerator(int seed)
        {
            var random = new System.Random(seed);
            offsetX = random.Next(1000, 100000);
            offsetZ = random.Next(1000, 100000);
        }
        public int Surface(int x, int z)
        {
            float broad = Mathf.PerlinNoise((x + offsetX) * 0.012f, (z + offsetZ) * 0.012f);
            float detail = Mathf.PerlinNoise((x + offsetX) * 0.045f, (z + offsetZ) * 0.045f);
            return Mathf.Clamp(14 + Mathf.FloorToInt(broad * 16 + detail * 5), 4, Height - 8);
        }
        public BlockId Sample(int x, int y, int z)
        {
            if (y < 0 || y >= Height) return BlockId.Air;
            int surface = Surface(x, z);
            if (y > surface) return BlockId.Air;
            if (y == surface) return BlockId.Grass;
            return y >= surface - 3 ? BlockId.Dirt : BlockId.Stone;
        }
    }
}
