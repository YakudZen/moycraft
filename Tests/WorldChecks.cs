using System;
using System.Collections.Generic;
using System.Diagnostics;
using VoxelSurvival;
using UnityEngine;

public static partial class ManagedChecks
{
    private sealed class GridQuery : IVoxelRayQuery
    {
        public readonly HashSet<Vector3Int> blocks = new();
        public Func<Vector3Int,bool> loaded = _ => true;
        public bool TryGetSolid(Vector3Int p,out bool solid)
        { solid=blocks.Contains(p); return loaded(p); }
    }
    private static void RayChecks()
    {
        var grid=new GridQuery();
        var axes=new[]{Vector3Int.right,Vector3Int.left,Vector3Int.up,Vector3Int.down,Vector3Int.forward,Vector3Int.back};
        int rays=0;
        foreach(int x in new[]{-33,-32,-17,-16,-1,0,15,16,31,32})
            foreach(int z in new[]{-17,-16,-1,0,15,16,31}) foreach(var axis in axes)
            {
                var cell=new Vector3Int(x,20,z); grid.blocks.Clear(); grid.blocks.Add(cell);
                Vector3 eye=cell+Vector3.one*0.5f+(Vector3)axis*3.25f;
                if(!VoxelRaycaster.Cast(grid,eye,-(Vector3)axis,5,out var hit) || hit.cell!=cell ||
                    hit.normal!=axis || hit.Adjacent!=cell+axis || !VoxelRaycaster.WithinReach(eye,hit.cell,5))
                    throw new Exception("Ray selection changed at chunk position "+cell+" face "+axis);
                rays++;
            }
        Check(rays==420,"420 rays select all faces across positive/negative chunk seams");
        grid.blocks.Clear(); grid.blocks.Add(new Vector3Int(5,0,0));
        Check(VoxelRaycaster.Cast(grid,new Vector3(0.5f,0.5f,0.5f),Vector3.right,4.5f,out var exact) && exact.distance==4.5f,"exact reach measured to face");
        Check(!VoxelRaycaster.Cast(grid,new Vector3(0.5f,0.5f,0.5f),Vector3.right,4.49f,out _),"beyond reach rejected");
        Check(VoxelRaycaster.Cast(grid,new Vector3(0.5f,0.5f,0.5f),Vector3.right*12,4.5f,out _),"ray direction normalized");
        grid.blocks.Add(new Vector3Int(2,0,0));
        Check(VoxelRaycaster.Cast(grid,new Vector3(0.5f,0.5f,0.5f),Vector3.right,8,out var near) && near.cell.x==2,"nearest solid blocks occlude farther blocks");
        grid.loaded=p=>p.x<1;
        Check(!VoxelRaycaster.Cast(grid,new Vector3(0.5f,0.5f,0.5f),Vector3.right,8,out _),"ray stops at unloaded cells");
        grid.loaded=_=>true; grid.blocks.Clear(); grid.blocks.Add(new Vector3Int(-1,2,3));
        Check(VoxelRaycaster.Cast(grid,new Vector3(0,2.5f,3.5f),Vector3.left,5,out var boundary) && boundary.cell.x==-1 && boundary.normal==Vector3Int.right,"negative-facing ray starting on a grid plane");
        grid.blocks.Clear(); grid.blocks.Add(new Vector3Int(0,2,3));
        Check(VoxelRaycaster.Cast(grid,new Vector3(0,2.5f,3.5f),Vector3.right,5,out boundary) && boundary.normal==Vector3Int.left,"positive-facing ray starting on a grid plane");
        Check(!VoxelRaycaster.Cast(grid,Vector3.zero,Vector3.zero,5,out _),"zero direction rejected");
        grid.blocks.Clear(); grid.blocks.Add(new Vector3Int(3,3,3)); grid.blocks.Add(new Vector3Int(1,0,0));
        Check(VoxelRaycaster.Cast(grid,Vector3.one*0.5f,Vector3.one,5,out var diagonal) && diagonal.cell==new Vector3Int(3,3,3),"diagonal corner ties select the traversed cell");
        var random=new System.Random(99271);
        for(int i=0;i<1000;i++)
        {
            var cell=new Vector3Int(random.Next(-64,64),random.Next(3,40),random.Next(-64,64));
            var axis=axes[random.Next(6)];
            Vector3 point=cell+new Vector3(0.1f+(float)random.NextDouble()*0.8f,0.1f+(float)random.NextDouble()*0.8f,0.1f+(float)random.NextDouble()*0.8f);
            Vector3 eye=point+(Vector3)axis*3+new Vector3((float)random.NextDouble()-0.5f,(float)random.NextDouble()-0.5f,(float)random.NextDouble()-0.5f);
            grid.blocks.Clear(); grid.blocks.Add(cell);
            if(!VoxelRaycaster.Cast(grid,eye,point-eye,5,out var hit) || hit.cell!=cell || !VoxelRaycaster.WithinReach(eye,cell,5))
                throw new Exception("Oblique selection failed at "+cell);
        }
        Check(true,"1000 oblique rays remain selectable independently of player position");
    }

    private static void GenerationChecks()
    {
        var timer=Stopwatch.StartNew();
        int wood=0,leaves=0,caveAir=0,mouths=0,woodAtSeam=0;
        var generator=new TerrainGenerator(17391); var reverse=new TerrainGenerator(17391);
        var coords=new List<Vector2Int>();
        for(int z=-3;z<=3;z++) for(int x=-3;x<=3;x++) coords.Add(new Vector2Int(x,z));
        var expected=new Dictionary<Vector2Int,byte[]>();
        foreach(var coord in coords) expected.Add(coord,generator.CopyChunk(coord));
        coords.Reverse();
        foreach(var coord in coords)
        {
            var data=new ChunkData(coord,reverse); var original=expected[coord];
            int ox=coord.x*16,oz=coord.y*16;
            for(int z=0;z<16;z++) for(int x=0;x<16;x++)
            {
                int surface=generator.Surface(ox+x,oz+z);
                for(int y=0;y<TerrainGenerator.Height;y++)
                {
                    var block=data.Get(x,y,z);
                    if((byte)block!=original[ChunkData.Index(x,y,z)]) throw new Exception("Load order changed generation");
                    if((x==0 || x==15 || z==0 || z==15) && block!=generator.Sample(ox+x,y,oz+z)) throw new Exception("Sample and chunk disagree at seam");
                    if(block==BlockId.Wood) wood++;
                    if(block==BlockId.Leaves) {leaves++; if(x==0 || x==15 || z==0 || z==15) woodAtSeam++;}
                    if(block==BlockId.Air && y>=2 && y<surface-3) caveAir++;
                    if(block==BlockId.Air && y==surface) mouths++;
                    if(y<=1 && block==BlockId.Air) throw new Exception("Cave cut the foundation");
                }
            }
        }
        Check(wood>100 && leaves>500,"trees generate trunks and leaf crowns");
        Check(woodAtSeam>100,"tree crowns exist on chunk borders");
        Check(caveAir>1000 && mouths>20,"underground caves and surface mouths generate");
        Check(true,"all 49 chunks match reverse load order and border sampling");
        Check(true,"caves preserve the two foundation layers");
        int spawnSurface=generator.Surface(8,8);
        Check(generator.Sample(8,spawnSurface,8)==BlockId.Grass && generator.Sample(8,spawnSurface+1,8)==BlockId.Air && generator.Sample(8,spawnSurface+2,8)==BlockId.Air,"spawn ground and player headroom are safe");
        var mutated=new ChunkData(Vector2Int.zero,generator); mutated.Set(8,spawnSurface,8,BlockId.Air);
        Check(generator.Sample(8,spawnSurface,8)==BlockId.Grass,"chunk edits do not mutate cached base generation");
        var anotherSeed=new TerrainGenerator(456); bool changed=false;
        var other=anotherSeed.CopyChunk(Vector2Int.zero); var originalZero=expected[Vector2Int.zero];
        for(int i=0;i<other.Length;i++) if(other[i]!=originalZero[i]) {changed=true;break;}
        Check(changed,"different seeds change generated terrain and features");
        for(int i=0;i<100;i++) generator.CopyChunk(new Vector2Int(200+i,100));
        byte[] regenerated=generator.CopyChunk(Vector2Int.zero);
        for(int i=0;i<regenerated.Length;i++) if(regenerated[i]!=originalZero[i]) throw new Exception("Cache eviction changed generation");
        Check(true,"base chunks regenerate identically after cache eviction");
        var cacheField=typeof(TerrainGenerator).GetField("cache",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        Check(((Dictionary<Vector2Int,byte[]>)cacheField.GetValue(generator)).Count<=96,"generation cache stays bounded");
        // Follow an actual surface mouth into a deep cave via six-connected air cells.
        Vector3Int mouth=default; bool found=false;
        for(int z=16;z<45 && !found;z++) for(int x=16;x<45 && !found;x++)
        {
            int surface=generator.Surface(x,z);
            if(generator.Sample(x,surface,z)==BlockId.Air) {mouth=new Vector3Int(x,surface,z);found=true;}
        }
        var visited=new HashSet<Vector3Int>(); var queue=new Queue<Vector3Int>(); queue.Enqueue(mouth); visited.Add(mouth);
        bool reachedDepth=false;
        while(found && queue.Count>0 && visited.Count<100000)
        {
            var cell=queue.Dequeue(); if(cell.y<12) {reachedDepth=true;break;}
            foreach(var direction in VoxelChunk.Neighbors)
            {
                var next=cell+direction;
                if(next.x<0 || next.x>=64 || next.z<0 || next.z>=64 || next.y<2 || next.y>generator.Surface(next.x,next.z)) continue;
                if(generator.Sample(next.x,next.y,next.z)==BlockId.Air && visited.Add(next)) queue.Enqueue(next);
            }
        }
        Check(found && reachedDepth,"surface entrance connects to a deep cave via contiguous air");
        Console.WriteLine("GENERATION_METRICS: trunks="+wood+" leaves="+leaves+" caveAir="+caveAir+" mouths="+mouths+" suiteMs="+timer.ElapsedMilliseconds);
    }
}
