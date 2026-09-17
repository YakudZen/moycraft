using System;
using UnityEngine;

namespace VoxelSurvival
{
    [CreateAssetMenu(menuName = "Voxel Survival/Block Catalog")]
    public sealed class BlockCatalog : ScriptableObject
    {
        public BlockDefinition[] blocks;
        [NonSerialized] private BlockDefinition[] lookup;
        public void Initialize()
        {
            lookup = new BlockDefinition[256];
            foreach (var block in blocks)
            {
                if (block == null || lookup[(byte)block.id] != null)
                    throw new InvalidOperationException("Missing or duplicate block definition.");
                lookup[(byte)block.id] = block;
            }
            foreach (BlockId id in Enum.GetValues(typeof(BlockId)))
                if (lookup[(byte)id] == null) throw new InvalidOperationException("Missing block: " + id);
        }
        public BlockDefinition Get(BlockId id)
        {
            if (lookup == null) Initialize();
            return lookup[(byte)id] ?? throw new ArgumentOutOfRangeException(nameof(id));
        }
        private void OnValidate() => lookup = null;
    }
}
