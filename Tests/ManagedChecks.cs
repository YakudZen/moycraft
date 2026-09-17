using System;
using System.Reflection;
using System.Runtime.Serialization;
using VoxelSurvival;
using UnityEngine;

public static class ManagedChecks
{
    static int passed;
    static void Check(bool condition,string text)
    {
        if (!condition) throw new Exception(text);
        passed++; Console.WriteLine("PASS " + text);
    }
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) => {
            string file = System.IO.Path.Combine(args[0], new AssemblyName(e.Name).Name + ".dll");
            return System.IO.File.Exists(file) ? Assembly.LoadFrom(file) : null;
        };
        Run();
    }
    static void Run()
    {
        // Only pure managed methods are exercised. No Unity engine/native calls are emulated.
        var inventory = (Hotbar)FormatterServices.GetUninitializedObject(typeof(Hotbar));
        inventory.Initialize(false);
        Check(inventory.Slots.Length == 9, "nine slots");
        for (int i = 0; i < 576; i++) if(!inventory.Add(BlockId.Dirt)) throw new Exception("early full");
        Check(!inventory.CanAdd(BlockId.Dirt) && !inventory.Add(BlockId.Stone), "overflow refused");
        inventory.Select(-1); Check(inventory.Selected == 8, "negative selection wraps");
        inventory.Select(9); Check(inventory.Selected == 0, "positive selection wraps");
        Check(inventory.ConsumeSelected() && inventory.Current.count == 63, "consumption decrements one");
        Check(inventory.Add(BlockId.Dirt) && inventory.Current.count == 64, "partial stack reused");
        inventory.Initialize(); inventory.Select(7);
        Check(inventory.Current.IsTool && !inventory.ConsumeSelected(), "tools cannot be placed");
        Check(inventory.Slots[6].Empty, "starter pickup slot free");
        inventory.Select(0);
        for (int i=0;i<32;i++) inventory.ConsumeSelected();
        Check(inventory.Current.Empty && inventory.Current.block == BlockId.Air, "empty slot cleared");
        Check(inventory.Add(BlockId.Ice) && inventory.Current.block == BlockId.Ice && inventory.Current.count == 1, "empty slot reused");
        var block = (BlockDefinition)FormatterServices.GetUninitializedObject(typeof(BlockDefinition));
        block.hardness=2; block.preferredTool=ToolKind.Pickaxe; block.minimumToolTier=2;
        Check(!block.CanHarvest(ToolKind.Hand,0), "required tool blocks hands");
        Check(!block.CanHarvest(ToolKind.Pickaxe,1), "insufficient tier rejected");
        Check(!block.CanHarvest(ToolKind.Axe,3), "wrong tool rejected regardless of tier");
        Check(block.CanHarvest(ToolKind.Pickaxe,2), "matching tier accepted");
        Check(block.MiningSeconds(ToolKind.Pickaxe,2)<block.MiningSeconds(ToolKind.Pickaxe,1), "higher tier faster");
        block.minimumToolTier=0; Check(block.CanHarvest(ToolKind.Hand,0), "unrestricted block accepts hands");
        Check(ChunkData.Coordinate(-1,-17) == new Vector2Int(-1,-2), "negative coordinates floor correctly");
        Check(ChunkData.Local(-1)==15 && ChunkData.Local(-16)==0 && ChunkData.Local(-17)==15, "negative local coordinates wrap");
        var corners=(Vector3[,])typeof(VoxelChunk).GetField("Corners",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        for(int face=0;face<6;face++)
        {
            Vector3 cross=Vector3.Cross(corners[face,1]-corners[face,0],corners[face,2]-corners[face,0]);
            Check(Vector3.Dot(cross,VoxelChunk.Neighbors[face])>0.99f,"face "+face+" winding points outward");
        }
        Console.WriteLine("MANAGED_CHECKS_OK: " + passed);
    }
}
