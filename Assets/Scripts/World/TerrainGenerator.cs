using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelSurvival
{
    public sealed class TerrainGenerator
    {
        public const int Height = 64;
        public const int Version = 2;
        private const int CaveSpacing = 48, TreeSpacing = 10, CacheLimit = 96;
        private readonly int seed;
        private readonly Dictionary<Vector2Int, byte[]> cache = new();
        private readonly Queue<Vector2Int> cacheOrder = new();

        public TerrainGenerator(int seed) => this.seed = seed;

        public int Surface(int x, int z) => 16 + (int)(Noise(x * 0.018, z * 0.018, 11) * 13 +
            Noise(x * 0.055, z * 0.055, 37) * 4);

        public BlockId Sample(int x, int y, int z)
        {
            if (y < 0 || y >= Height) return BlockId.Air;
            byte[] cells = Generated(ChunkData.Coordinate(x, z));
            return (BlockId)cells[ChunkData.Index(ChunkData.Local(x), y, ChunkData.Local(z))];
        }

        // Immutable base data cache: copy before player edits. Bounded to 1.5 MiB.
        public byte[] CopyChunk(Vector2Int coordinate) => (byte[])Generated(coordinate).Clone();
        private byte[] Generated(Vector2Int coordinate)
        {
            if (cache.TryGetValue(coordinate, out var found)) return found;
            var result = Generate(coordinate);
            if (cache.Count >= CacheLimit) cache.Remove(cacheOrder.Dequeue());
            cache.Add(coordinate, result); cacheOrder.Enqueue(coordinate);
            return result;
        }

        private byte[] Generate(Vector2Int coordinate)
        {
            int ox = coordinate.x * ChunkData.Size, oz = coordinate.y * ChunkData.Size;
            var cells = new byte[ChunkData.Size * Height * ChunkData.Size];
            var heights = new int[ChunkData.Size * ChunkData.Size];
            for (int z = 0; z < ChunkData.Size; z++) for (int x = 0; x < ChunkData.Size; x++)
            {
                int surface = Surface(ox + x, oz + z); heights[x + z * ChunkData.Size] = surface;
                for (int y = 0; y <= surface; y++) cells[ChunkData.Index(x,y,z)] =
                    (byte)(y == surface ? BlockId.Grass : y >= surface - 3 ? BlockId.Dirt : BlockId.Stone);
            }

            // Carve bounded capsules instead of evaluating noise for every voxel.
            var tunnels = CaveFeatures(ox, oz, ox + ChunkData.Size - 1, oz + ChunkData.Size - 1);
            foreach (var tunnel in tunnels)
            {
                int minX = Math.Max(ox, tunnel.minX), maxX = Math.Min(ox + ChunkData.Size - 1, tunnel.maxX);
                int minZ = Math.Max(oz, tunnel.minZ), maxZ = Math.Min(oz + ChunkData.Size - 1, tunnel.maxZ);
                for (int z = minZ; z <= maxZ; z++) for (int x = minX; x <= maxX; x++)
                {
                    int surface = heights[x - ox + (z - oz) * ChunkData.Size];
                    int top = Math.Min(surface, tunnel.maxY);
                    for (int y = Math.Max(2, tunnel.minY); y <= top; y++)
                        if (!ProtectedRoof(x, y, z, surface) && tunnel.Contains(x,y,z))
                            cells[ChunkData.Index(x-ox,y,z-oz)] = (byte)BlockId.Air;
                }
            }

            // Include roots outside the chunk; canopy seams cannot depend on load order.
            for (int gz = FloorDiv(oz - 2, TreeSpacing); gz <= FloorDiv(oz + ChunkData.Size + 1, TreeSpacing); gz++)
                for (int gx = FloorDiv(ox - 2, TreeSpacing); gx <= FloorDiv(ox + ChunkData.Size + 1, TreeSpacing); gx++)
                {
                    uint hash = Hash(gx, gz, 103);
                    if (hash % 100 >= 72) continue;
                    int tx = gx * TreeSpacing + 2 + (int)((hash >> 8) % 6);
                    int tz = gz * TreeSpacing + 2 + (int)((hash >> 16) % 6);
                    if (NearSpawn(tx,tz)) continue;
                    int ground = Surface(tx,tz);
                    if (IsCave(tx,ground,tz)) continue;
                    if (Math.Abs(Surface(tx+1,tz)-ground)>1 || Math.Abs(Surface(tx,tz+1)-ground)>1) continue;
                    int top = ground + 4 + (int)((hash >> 24) % 3);
                    for (int y = ground + 1; y <= top; y++) Stamp(cells,ox,oz,tx,y,tz,BlockId.Wood);
                    for (int y = top-2; y <= top+1; y++)
                    {
                        int radius = y == top+1 ? 1 : 2;
                        for (int z = tz-radius; z <= tz+radius; z++) for (int x = tx-radius; x <= tx+radius; x++)
                        {
                            if (Math.Abs(x-tx)==radius && Math.Abs(z-tz)==radius && radius==2) continue;
                            Stamp(cells,ox,oz,x,y,z,BlockId.Leaves);
                        }
                    }
                }
            return cells;
        }

        private static void Stamp(byte[] cells,int ox,int oz,int x,int y,int z,BlockId block)
        {
            if (x<ox || x>=ox+ChunkData.Size || z<oz || z>=oz+ChunkData.Size || y<1 || y>=Height) return;
            int index=ChunkData.Index(x-ox,y,z-oz);
            if (cells[index]==(byte)BlockId.Air) cells[index]=(byte)block;
        }
        private static bool NearSpawn(int x,int z) => (long)(x-8)*(x-8)+(long)(z-8)*(z-8)<=16;
        private static bool ProtectedRoof(int x,int y,int z,int surface) => NearSpawn(x,z) && y>=surface-3;

        private bool IsCave(int x,int y,int z)
        {
            if (y<2 || ProtectedRoof(x,y,z,Surface(x,z))) return false;
            foreach (var tunnel in CaveFeatures(x,z,x,z)) if (tunnel.Contains(x,y,z)) return true;
            return false;
        }
        private List<Tunnel> CaveFeatures(int minX,int minZ,int maxX,int maxZ)
        {
            var tunnels=new List<Tunnel>(48);
            for (int gz=FloorDiv(minZ,CaveSpacing)-1; gz<=FloorDiv(maxZ,CaveSpacing)+1; gz++)
                for (int gx=FloorDiv(minX,CaveSpacing)-1; gx<=FloorDiv(maxX,CaveSpacing)+1; gx++)
                {
                    var node=CaveNode(gx,gz);
                    // Shared nodes connect the underground network across chunk/region borders.
                    tunnels.Add(new Tunnel(node,CaveNode(gx+1,gz),2.25f));
                    tunnels.Add(new Tunnel(node,CaveNode(gx,gz+1),2.25f));
                    tunnels.Add(new Tunnel(node,node,4.2f));
                    int ex=(int)node.x+12, ez=(int)node.z+7;
                    var entrance=new Vector3(ex,Surface(ex,ez)+1,ez);
                    var bend=new Vector3(ex-3,node.y+3,ez);
                    tunnels.Add(new Tunnel(node,bend,2.6f));
                    tunnels.Add(new Tunnel(bend,entrance,2.6f));
                }
            return tunnels;
        }
        private Vector3 CaveNode(int gx,int gz)
        {
            uint hash=Hash(gx,gz,701);
            return new Vector3(gx*CaveSpacing+16+(int)(hash%12),7+(int)((hash>>12)%7),gz*CaveSpacing+16+(int)((hash>>20)%12));
        }
        private readonly struct Tunnel
        {
            private readonly Vector3 start, delta;
            private readonly float radiusSquared, lengthSquared;
            public readonly int minX,minY,minZ,maxX,maxY,maxZ;
            public Tunnel(Vector3 a,Vector3 b,float radius)
            {
                start=a; delta=b-a; radiusSquared=radius*radius; lengthSquared=delta.sqrMagnitude;
                minX=(int)Math.Floor(Math.Min(a.x,b.x)-radius); maxX=(int)Math.Ceiling(Math.Max(a.x,b.x)+radius);
                minY=(int)Math.Floor(Math.Min(a.y,b.y)-radius); maxY=(int)Math.Ceiling(Math.Max(a.y,b.y)+radius);
                minZ=(int)Math.Floor(Math.Min(a.z,b.z)-radius); maxZ=(int)Math.Ceiling(Math.Max(a.z,b.z)+radius);
            }
            public bool Contains(int x,int y,int z)
            {
                if(x<minX || x>maxX || y<minY || y>maxY || z<minZ || z>maxZ) return false;
                var offset=new Vector3(x+0.5f,y+0.5f,z+0.5f)-start;
                float t=lengthSquared==0?0:Math.Max(0,Math.Min(1,Vector3.Dot(offset,delta)/lengthSquared));
                return (offset-delta*t).sqrMagnitude<radiusSquared;
            }
        }

        private static int FloorDiv(int value,int size) => (int)Math.Floor(value/(double)size);
        private uint Hash(int x,int z,uint salt)
        {
            unchecked
            {
                uint h=(uint)x*374761393u+(uint)z*668265263u+(uint)seed*1442695041u+salt*2246822519u;
                h=(h^(h>>13))*1274126177u;
                return h^(h>>16);
            }
        }
        private double Noise(double x,double z,uint salt)
        {
            int ix=(int)Math.Floor(x), iz=(int)Math.Floor(z);
            double fx=x-ix, fz=z-iz; fx=fx*fx*(3-2*fx); fz=fz*fz*(3-2*fz);
            double a=Hash(ix,iz,salt)/(double)uint.MaxValue, b=Hash(ix+1,iz,salt)/(double)uint.MaxValue;
            double c=Hash(ix,iz+1,salt)/(double)uint.MaxValue, d=Hash(ix+1,iz+1,salt)/(double)uint.MaxValue;
            return (a+(b-a)*fx)*(1-fz)+(c+(d-c)*fx)*fz;
        }
    }
}
