using UnityEngine;

namespace VoxelSurvival
{
    // IDs are permanent: never reorder or reuse them after worlds have been saved.
    public enum BlockId : byte { Air, Grass, Dirt, Stone, Wood, Leaves, Sand, Gravel, Ice, Water, Lava, Fire }
    public enum ToolKind : byte { Hand, Pickaxe, Axe, Shovel }

    [CreateAssetMenu(menuName = "Voxel Survival/Block")]
    public sealed class BlockDefinition : ScriptableObject
    {
        public BlockId id;
        public string displayName;
        public bool solid = true;
        public bool opaque = true;
        public bool placeable = true;
        [Min(0.05f)] public float hardness = 0.6f;
        public ToolKind preferredTool;
        [Min(0)] public int minimumToolTier;
        public BlockId drop;
        [Range(0, 1)] public float flammability;
        [Min(0)] public float burnSeconds;
        [Range(0, 15)] public int topTile;
        [Range(0, 15)] public int sideTile;
        [Range(0, 15)] public int bottomTile;
        public Color mapColor = Color.white;

        public bool CanHarvest(ToolKind tool, int tier) => minimumToolTier == 0 ||
            (tool == preferredTool && tier >= minimumToolTier);
        public float MiningSeconds(ToolKind tool, int tier) => hardness /
            (tool != ToolKind.Hand && tool == preferredTool ? 1.5f + tier * 0.75f : 1f);
        public int Tile(int face) => face == 2 ? topTile : face == 3 ? bottomTile : sideTile;
    }
}
