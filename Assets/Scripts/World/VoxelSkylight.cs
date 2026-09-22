using System;

namespace VoxelSurvival
{
    // A finite halo is sufficient: indirect skylight loses one level on every step.
    // All columns are sampled from world data (including unloaded chunks and edits),
    // so streaming order cannot turn an enclosed room into a light source.
    public sealed class VoxelSkylight
    {
        public const byte Maximum = 15;
        private readonly int minX, minZ, width, height, layer;
        private readonly byte[] light;
        private readonly float[] ambient;

        public VoxelSkylight(int originX, int originZ, int size, int worldHeight, Func<int,int,int,bool> opaque)
        {
            minX = originX - Maximum; minZ = originZ - Maximum;
            width = size + Maximum * 2; height = worldHeight + 1; layer = width * width;
            light = new byte[layer * height];
            var blocked = new bool[light.Length];
            var queue = new int[light.Length];
            int head = 0, tail = 0, highestSolid = -1;
            for (int z = 0; z < width; z++) for (int x = 0; x < width; x++)
            {
                bool sky = true;
                for (int y = worldHeight; y >= 0; y--)
                {
                    int index = x + width * z + layer * y;
                    bool solid = y < worldHeight && opaque(minX + x,y,minZ + z);
                    blocked[index] = solid;
                    if (solid && y > highestSolid) highestSolid = y;
                    if (solid) sky = false;
                    if (!sky) continue;
                    light[index] = Maximum; queue[tail++] = index;
                }
            }
            // Multi-source BFS visits levels in descending order, enqueuing each dark cell once.
            while (head < tail)
            {
                int index = queue[head++];
                byte next = (byte)(light[index] - 1);
                if (next == 0) continue;
                int y = index / layer, column = index % layer, z = column / width, x = column % width;
                if (x > 0) Spread(index-1,next);
                if (x+1 < width) Spread(index+1,next);
                if (z > 0) Spread(index-width,next);
                if (z+1 < width) Spread(index+width,next);
                if (y > 0) Spread(index-layer,next);
                if (y+1 < height) Spread(index+layer,next);
            }
            void Spread(int index, byte value)
            {
                if (blocked[index] || light[index] >= value) return;
                light[index] = value; queue[tail++] = index;
            }
            // Connectivity alone uses the brightest path, so a tiny opening keeps a
            // small room almost fully lit. Diffuse light instead gathers energy from
            // all six neighbors; walls absorb it and smaller openings supply less.
            // Twelve Jacobi steps stay inside the 15-cell halo, even at face samples
            // one cell outside the chunk. Every rebuild starts from current geometry.
            ambient = new float[light.Length];
            var nextAmbient = new float[light.Length];
            for (int i = 0; i < light.Length; i++)
                if (light[i] == Maximum) ambient[i] = nextAmbient[i] = 1;
            // Reuse the flood queue as a compact list. Empty sky more than twelve
            // cells above terrain cannot change within twelve iterations.
            int count = 0;
            for (int y = 0; y <= Math.Min(worldHeight-1,highestSolid+12); y++)
            for (int z = 1; z < width-1; z++)
            for (int x = 1; x < width-1; x++)
            {
                int i = x + width*z + layer*y;
                if (light[i] > 0) queue[count++] = i;
            }
            var current = ambient;
            for (int step = 0; step < 12; step++)
            {
                for (int cell = 0; cell < count; cell++)
                {
                    int i = queue[cell];
                    float sum = current[i-1] + current[i+1] + current[i-width] + current[i+width]
                        + current[i+layer] + (i >= layer ? current[i-layer] : 0);
                    bool directSky = light[i] == Maximum;
                    nextAmbient[i] = (directSky ? 0.2f : 0) + sum * (directSky ? 0.8f/6 : 0.9f/6);
                }
                var swap = current; current = nextAmbient; nextAmbient = swap;
            }
            ambient = current;
        }

        public byte Get(int x, int y, int z)
        {
            x -= minX; z -= minZ;
            if (y < 0 || x < 0 || z < 0 || x >= width || z >= width) return 0;
            if (y >= height) return Maximum;
            return light[x + width*z + layer*y];
        }

        public float GetAmbient(int x, int y, int z)
        {
            x -= minX; z -= minZ;
            if (y < 0 || x < 0 || z < 0 || x >= width || z >= width) return 0;
            if (y >= height) return 1;
            return ambient[x + width*z + layer*y];
        }
    }
}
