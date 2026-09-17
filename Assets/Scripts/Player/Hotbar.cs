using UnityEngine;

namespace VoxelSurvival
{
    [System.Serializable]
    public sealed class InventorySlot
    {
        public BlockId block;
        public int count;
        public ToolKind tool;
        public int tier;
        public bool IsTool => tool != ToolKind.Hand;
        public bool Empty => !IsTool && count <= 0;
    }
    public sealed class Hotbar : MonoBehaviour
    {
        public const int Capacity = 9, StackLimit = 64;
        public InventorySlot[] Slots { get; private set; }
        public int Selected { get; private set; }
        public InventorySlot Current => Slots[Selected];
        public void Initialize(bool starter = true)
        {
            Slots = new InventorySlot[Capacity];
            for (int i = 0; i < Capacity; i++) Slots[i] = new InventorySlot();
            if (!starter) return;
            var initial = new[] { BlockId.Grass, BlockId.Dirt, BlockId.Stone, BlockId.Wood, BlockId.Sand, BlockId.Gravel };
            for (int i = 0; i < initial.Length; i++) { Slots[i].block = initial[i]; Slots[i].count = 32; }
            Slots[7].tool = ToolKind.Pickaxe; Slots[7].tier = 1;
            Slots[8].tool = ToolKind.Pickaxe; Slots[8].tier = 2;
        }
        private void Awake() { if (Slots == null) Initialize(); }
        public void Select(int index) => Selected = (index % Capacity + Capacity) % Capacity;
        public bool CanAdd(BlockId block)
        {
            if (block == BlockId.Air) return true;
            foreach (var slot in Slots)
                if (slot.Empty || (!slot.IsTool && slot.block == block && slot.count < StackLimit)) return true;
            return false;
        }
        public bool Add(BlockId block)
        {
            if (block == BlockId.Air) return true;
            foreach (var slot in Slots)
                if (!slot.IsTool && slot.block == block && slot.count > 0 && slot.count < StackLimit)
                { slot.count++; return true; }
            foreach (var slot in Slots)
                if (slot.Empty) { slot.block = block; slot.count = 1; return true; }
            return false;
        }
        public bool ConsumeSelected()
        {
            if (Current.IsTool || Current.Empty) return false;
            Current.count--;
            if (Current.count == 0) Current.block = BlockId.Air;
            return true;
        }
    }
}
