using System;
using System.Diagnostics;
using VoxelSurvival;

public static partial class ManagedChecks
{
    private static void LightingChecks()
    {
        bool roofOpen = false, windowOpen = false;
        bool Box(int x,int y,int z)
        {
            if (x < 0 || x > 4 || y < 10 || y > 14 || z < 0 || z > 4) return false;
            if (roofOpen && x == 2 && y == 14 && z == 2) return false;
            if (windowOpen && x == 0 && y == 12 && z == 2) return false;
            return x == 0 || x == 4 || y == 10 || y == 14 || z == 0 || z == 4;
        }
        var closed = new VoxelSkylight(0,0,16,32,Box);
        Check(closed.Get(2,12,2) == 0, "Sealed room has exactly zero skylight");
        Check(closed.Get(-1,12,2) == 15, "Outdoor skylight is unaffected by an enclosed room");
        roofOpen = true;
        var roof = new VoxelSkylight(0,0,16,32,Box);
        Check(roof.Get(2,12,2) == 15 && roof.Get(1,12,2) > 0, "Breaking roof admits direct and sideways skylight");
        roofOpen = false; windowOpen = true;
        var window = new VoxelSkylight(0,0,16,32,Box);
        Check(window.Get(1,12,2) > window.Get(2,12,2) && window.Get(2,12,2) > 0,
            "Side opening admits attenuated light under a closed roof");
        windowOpen = false;
        var resealed = new VoxelSkylight(0,0,16,32,Box);
        Check(resealed.Get(2,12,2) == 0, "Replacing the last opening removes stale light");
        Check(resealed.GetAmbient(2,12,2) == 0 && window.GetAmbient(2,12,2) > 0,
            "Diffuse lighting is zero when sealed and returns through a side window");

        float previous = 1;
        for (int walls = 0; walls <= 4; walls++)
        {
            bool Building(int x,int y,int z)
            {
                if (x < 0 || x > 4 || y < 10 || y > 14 || z < 0 || z > 4) return false;
                return y == 10 || (walls >= 1 && x == 0) || (walls >= 2 && x == 4)
                    || (walls >= 3 && z == 0) || (walls >= 4 && z == 4);
            }
            var building = new VoxelSkylight(0,0,16,32,Building);
            float value = building.GetAmbient(2,12,2);
            if (walls > 0) Check(value < previous, "Adding wall " + walls + " dims the room even before closing its roof");
            previous = value;
        }
        previous = 1;
        foreach (int opening in new[] {5,3,1,0})
        {
            bool ApertureBox(int x,int y,int z)
            {
                if (x < 0 || x > 6 || y < 10 || y > 15 || z < 0 || z > 6) return false;
                if (z == 0 && y >= 11 && y <= 14 && Math.Abs(x-3) <= opening/2 && opening > 0) return false;
                return x == 0 || x == 6 || y == 10 || y == 15 || z == 0 || z == 6;
            }
            var room = new VoxelSkylight(0,0,16,32,ApertureBox);
            float value = room.GetAmbient(3,12,3);
            Check(value < previous && (opening > 0 ? value > 0 : value == 0),
                "Room gets darker as opening shrinks to " + opening + " blocks");
            Console.WriteLine("APERTURE_METRIC: width=" + opening + " ambient=" + value);
            previous = value;
        }

        bool Tunnel(int x,int y,int z) => !(z == 2 && ((x == 0 && y >= 10) || (x >= 0 && x <= 31 && y == 10)));
        var tunnel = new VoxelSkylight(0,0,16,32,Tunnel);
        Check(tunnel.Get(0,10,2) == 15 && tunnel.Get(14,10,2) == 1 && tunnel.Get(15,10,2) == 0,
            "Indirect light has a bounded range and cannot wrap through volume borders");
        var diagonal = new VoxelSkylight(0,0,16,32,(x,y,z) => !(x == 2 && y == 12 && z == 2));
        Check(diagonal.Get(2,12,2) == 0, "Solid faces block light without diagonal corner leaks");

        foreach (int offset in new[] {-16,0,16})
        {
            roofOpen = true;
            bool SeamBox(int x,int y,int z) => Box(x-offset-14,y,z);
            var left = new VoxelSkylight(offset,0,16,32,SeamBox);
            var right = new VoxelSkylight(offset+16,0,16,32,SeamBox);
            for (int x = offset+15; x <= offset+16; x++) for (int y = 10; y <= 15; y++) for(int z=0;z<=4;z++)
                if (left.Get(x,y,z) != right.Get(x,y,z) || Math.Abs(left.GetAmbient(x,y,z)-right.GetAmbient(x,y,z)) > 0.00001f)
                    throw new Exception("Skylight discontinuity at chunk seam");
            roofOpen = false;
            var changed = new VoxelSkylight(offset+16,0,16,32,SeamBox);
            if (changed.Get(offset+16,12,2) != 0) throw new Exception("Seam room stayed lit after closing roof");
        }
        Check(true, "Neighbor halos agree across positive and negative seams, including roof edits");
        var openAir = new VoxelSkylight(0,0,16,32,(x,y,z) => false);
        Check(Math.Abs(openAir.GetAmbient(8,16,8)-1) < 0.00001f, "Open air keeps full diffuse daylight");
        bool Canopy(int x,int y,int z) => y == 10 || (y == 16 && x >= 6 && x <= 10 && z >= 6 && z <= 10);
        var treeShade = new VoxelSkylight(0,0,16,32,Canopy);
        Check(treeShade.GetAmbient(8,11,8) < treeShade.GetAmbient(2,11,8), "Canopy darkens the ground directly below it");
        Check(Math.Abs(treeShade.GetAmbient(2,11,8)-treeShade.GetAmbient(14,11,8)) < 0.00001f,
            "Canopy shade has no preferred sideways projection");
        var removedCanopy = new VoxelSkylight(0,0,16,32,(x,y,z) => y == 10);
        Check(removedCanopy.GetAmbient(8,11,8) > treeShade.GetAmbient(8,11,8), "Removing the canopy restores ground light");

        var generation = new TerrainGenerator(17391);
        var timer = Stopwatch.StartNew();
        var terrain = new VoxelSkylight(0,0,16,TerrainGenerator.Height,(x,y,z) => generation.Sample(x,y,z) != BlockId.Air);
        timer.Stop();
        Check(terrain.Get(8,TerrainGenerator.Height,8) == 15 && terrain.Get(8,-1,8) == 0,
            "World top is sky and the bottom boundary is dark");
        Console.WriteLine("SKYLIGHT_METRICS: paddedChunkMs=" + timer.ElapsedMilliseconds);
        var cached = new ChunkData[9];
        for (int z=-1;z<=1;z++) for(int x=-1;x<=1;x++) cached[x+1+3*(z+1)] = new ChunkData(new UnityEngine.Vector2Int(x,z),generation);
        timer.Restart();
        var snapshot = new VoxelSkylight(0,0,16,TerrainGenerator.Height,(x,y,z) =>
            cached[(x+16)/16+3*((z+16)/16)].Get((x+16)%16,y,(z+16)%16) != BlockId.Air);
        timer.Stop();
        for (int x=-1;x<=16;x++) for(int z=-1;z<=16;z++) for(int y=0;y<=TerrainGenerator.Height;y++)
            if (snapshot.Get(x,y,z) != terrain.Get(x,y,z)) throw new Exception("Snapshot light differs from direct world sampling");
        Check(true,"Chunk snapshot lighting matches world sampling on every rendered cell and seam");
        Console.WriteLine("SKYLIGHT_METRICS: snapshotChunkMs=" + timer.ElapsedMilliseconds);
    }
}
