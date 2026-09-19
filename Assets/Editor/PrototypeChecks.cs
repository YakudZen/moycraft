using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoxelSurvival.Editor
{
    public static class PrototypeChecks
    {
        private static int count;
        private static void Check(bool condition,string description)
        {
            if(!condition) throw new Exception("CHECK FAILED: "+description);
            count++; Debug.Log("PASS: "+description);
        }
        [MenuItem("Moycraft/Run foundation checks")]
        public static void Run()
        {
            count=0;
            var catalog=AssetDatabase.LoadAssetAtPath<BlockCatalog>("Assets/Data/BlockCatalog.asset"); catalog.Initialize();
            Check(catalog.blocks.Length==12,"All stable block IDs have definitions");
            Check(!catalog.Get(BlockId.Stone).CanHarvest(ToolKind.Hand,0),"Stone rejects bare hands");
            Check(catalog.Get(BlockId.Stone).CanHarvest(ToolKind.Pickaxe,1),"Stone accepts tier 1 pickaxe");
            Check(!catalog.Get(BlockId.Ice).CanHarvest(ToolKind.Pickaxe,1) && catalog.Get(BlockId.Ice).CanHarvest(ToolKind.Pickaxe,2),"Minimum tool tier is enforced");
            Check(!catalog.Get(BlockId.Stone).CanHarvest(ToolKind.Shovel,2),"Wrong tool cannot bypass tier requirement");
            Check(catalog.Get(BlockId.Stone).MiningSeconds(ToolKind.Pickaxe,2)<catalog.Get(BlockId.Stone).MiningSeconds(ToolKind.Pickaxe,1),"Higher tier mines faster");
            var a=new TerrainGenerator(17391); var b=new TerrainGenerator(17391); var c=new TerrainGenerator(456);
            bool different=false;
            for(int x=-50;x<=50;x+=7) for(int z=-50;z<=50;z+=9)
            {
                if(a.Surface(x,z)!=b.Surface(x,z)) throw new Exception("Seed is not deterministic");
                different |= a.Surface(x,z)!=c.Surface(x,z);
            }
            Check(different,"Seed is repeatable and different seeds change the terrain");
            Check(ChunkData.Coordinate(-1,-17)==new Vector2Int(-1,-2) && ChunkData.Local(-1)==15 && ChunkData.Local(-16)==0,"Negative chunk coordinates use floor division");
            var go=new GameObject("Test world");
            var world=go.AddComponent<VoxelWorld>(); world.catalog=catalog;
            world.blockMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Blocks.mat"); world.Initialize();
            var inventoryGo=new GameObject("Test inventory"); var inventory=inventoryGo.AddComponent<Hotbar>(); inventory.Initialize(false);
            try
            {
                world.LoadChunk(Vector2Int.zero); world.LoadChunk(Vector2Int.right);
                var left=Find(world,Vector2Int.zero); var right=Find(world,Vector2Int.right);
                int leftBefore=left.GetComponent<MeshFilter>().sharedMesh.vertexCount;
                int rightBefore=right.GetComponent<MeshFilter>().sharedMesh.vertexCount;
                var p=new Vector3Int(15,50,8); var q=new Vector3Int(16,50,8);
                Check(world.SetBlock(p,BlockId.Stone),"Place a block on a chunk boundary");
                Check(left.GetComponent<MeshFilter>().sharedMesh.vertexCount==leftBefore+24,"Isolated cube has exactly six faces");
                world.SetBlock(q,BlockId.Stone);
                Check(left.GetComponent<MeshFilter>().sharedMesh.vertexCount==leftBefore+20 && right.GetComponent<MeshFilter>().sharedMesh.vertexCount==rightBefore+20,"Adjacent chunks cull their shared face");
                world.SetBlock(q,BlockId.Air);
                Check(left.GetComponent<MeshFilter>().sharedMesh.vertexCount==leftBefore+24,"Mining on a boundary restores neighbor geometry");
                Check(world.ModifiedCount==1,"Restoring generated state removes redundant edits");
                world.UnloadChunk(Vector2Int.zero); world.LoadChunk(Vector2Int.zero);
                Check(world.GetBlock(p)==BlockId.Stone,"Block edits survive chunk unload and reload");
                Check(!world.SetBlock(new Vector3Int(1,0,1),BlockId.Air),"World foundation cannot be removed");
                Check(!world.SetBlock(new Vector3Int(1,64,1),BlockId.Stone),"Height limit is enforced");
                Check(!world.SetBlock(new Vector3Int(3000,40,3000),BlockId.Stone),"Unloaded chunks cannot be edited");
                Check(!world.SetBlock(new Vector3Int(1,50,1),BlockId.Water),"Unimplemented fluids cannot be placed");
                world.LoadChunk(new Vector2Int(-1,-1)); var negative=new Vector3Int(-1,50,-1);
                world.SetBlock(negative,BlockId.Wood); world.UnloadChunk(new Vector2Int(-1,-1)); world.LoadChunk(new Vector2Int(-1,-1));
                Check(world.GetBlock(negative)==BlockId.Wood,"Negative-coordinate edits survive reload");
                Physics.SyncTransforms();
                Check(Physics.Raycast(new Vector3(15.5f,55,8.5f),Vector3.down,out var hit,8) && Mathf.Abs(hit.point.y-51)<0.01f,"Mesh collider follows edited blocks");
                for(int i=0;i<Hotbar.Capacity*Hotbar.StackLimit;i++)
                    if(!inventory.Add(BlockId.Dirt)) throw new Exception("Stack filled prematurely");
                Check(!inventory.CanAdd(BlockId.Dirt) && !inventory.Add(BlockId.Stone),"Full inventory rejects overflow");
                inventory.Select(-1); Check(inventory.Selected==8,"Hotbar selection wraps");
                inventory.ConsumeSelected(); Check(inventory.Add(BlockId.Dirt) && inventory.Current.count==64,"Freed stack space can be reused");
                inventory.Initialize(); inventory.Select(7); Check(!inventory.ConsumeSelected(),"Tools cannot be consumed as blocks");
                Check(inventory.Slots[6].Empty,"Starter loadout leaves a free pickup slot");
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(inventoryGo); }
            Debug.Log("FOUNDATION_CHECKS_OK: "+count);
        }
        private static VoxelChunk Find(VoxelWorld world,Vector2Int coordinate)
        {
            foreach(var chunk in world.GetComponentsInChildren<VoxelChunk>()) if(chunk.Data.coordinate==coordinate) return chunk;
            throw new Exception("Chunk not found");
        }
    }
}
