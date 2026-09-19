using UnityEngine;

namespace VoxelSurvival
{
    public sealed class PrototypeHud : MonoBehaviour
    {
        public VoxelWorld world;
        public FirstPersonPlayer player;
        public Hotbar hotbar;
        public BlockInteractor interaction;
        private GUIStyle title, small, label, centered, number;
        private readonly Color ink = new(0.055f,0.075f,0.085f,0.92f);
        private readonly Color accent = new(0.76f,0.94f,0.45f);
        private void Styles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 25, fontStyle = FontStyle.Bold };
            small = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            label = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            centered = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
            number = new GUIStyle(small) { alignment = TextAnchor.LowerRight, fontStyle = FontStyle.Bold };
            title.normal.textColor = Color.white; small.normal.textColor = new Color(0.76f,0.82f,0.81f);
            label.normal.textColor = Color.white; centered.normal.textColor = Color.white; number.normal.textColor = Color.white;
        }
        private static void Box(Rect rect, Color color)
        {
            var old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old;
        }
        private void OnGUI()
        {
            Styles();
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
            float w = Screen.width / scale, h = Screen.height / scale;
            Box(new Rect(24,24,260,85), ink); Box(new Rect(24,24,4,85),accent);
            GUI.Label(new Rect(40,32,230,33), "Moycraft", title);
            GUI.Label(new Rect(40,69,240,25), "FOUNDATION PROTOTYPE   /   0.1", small);
            GUI.Label(new Rect(26,116,360,22), "SEED " + world.seed + "     CHUNKS " + world.LoadedCount, small);
            GUI.Label(new Rect(26,137,360,22), "WASD move  /  SHIFT run  /  SPACE jump", small);
            GUI.Label(new Rect(26,157,400,22), "LMB hold to mine  /  RMB place  /  ESC pause", small);
            Box(new Rect(w/2-6,h/2-1,12,2),Color.white); Box(new Rect(w/2-1,h/2-6,2,12),Color.white);
            if (interaction.HasTarget)
            {
                var block = world.catalog.Get(world.GetBlock(interaction.Target));
                GUI.Label(new Rect(w/2-170,h/2+22,340,24), block.displayName + (block.minimumToolTier > 0 ? "  /  Pickaxe T"+block.minimumToolTier : ""),centered);
                if (interaction.Progress > 0)
                {
                    Box(new Rect(w/2-70,h/2+53,140,5),ink);
                    Box(new Rect(w/2-70,h/2+53,140*interaction.Progress,5),accent);
                }
            }
            const float cell = 64, gap = 6;
            float width = Hotbar.Capacity * (cell+gap)-gap, left = (w-width)/2;
            GUI.Label(new Rect(left,h-150,width,24), "1 - 9 / SCROLL   " + SlotName(hotbar.Current), centered);
            Box(new Rect(left-12,h-117,width+24,90), ink);
            for (int i = 0; i < Hotbar.Capacity; i++)
            {
                float x = left+i*(cell+gap), y = h-105;
                Box(new Rect(x,y,cell,cell), i == hotbar.Selected ? accent : new Color(0.24f,0.29f,0.30f,0.95f));
                Box(new Rect(x+2,y+2,cell-4,cell-4), new Color(0.10f,0.14f,0.15f));
                var slot = hotbar.Slots[i];
                if (!slot.Empty)
                {
                    if (slot.IsTool)
                    {
                        Box(new Rect(x+29,y+20,6,29),new Color(0.49f,0.32f,0.18f));
                        Box(new Rect(x+18,y+17,31,9),slot.tier == 1 ? new Color(0.67f,0.46f,0.26f) : new Color(0.67f,0.73f,0.73f));
                    }
                    else
                    {
                        var def = world.catalog.Get(slot.block);
                        Texture texture = world.blockMaterial.mainTexture;
                        var old = GUI.color; GUI.color = Color.white;
                        GUI.DrawTextureWithTexCoords(new Rect(x+16,y+15,32,32),texture,
                            new Rect(def.sideTile%4*0.25f,def.sideTile/4*0.25f,0.25f,0.25f));
                        GUI.color = old;
                    }
                    GUI.Label(new Rect(x+15,y+39,43,20),slot.IsTool ? "T"+slot.tier : slot.count.ToString(),number);
                }
                GUI.Label(new Rect(x+6,y+3,20,20),(i+1).ToString(),small);
            }
            if (!string.IsNullOrEmpty(interaction.Message)) GUI.Label(new Rect(w/2-250,h-185,500,26),interaction.Message,centered);
            if (player.Paused)
            {
                Box(new Rect(0,0,w,h),new Color(0.02f,0.04f,0.05f,0.78f));
                GUI.Label(new Rect(w/2-170,h/2-100,340,45),"PAUSED",new GUIStyle(title){alignment=TextAnchor.MiddleCenter});
                if (GUI.Button(new Rect(w/2-110,h/2-30,220,40),"Resume")) player.SetPaused(false);
                GUI.Label(new Rect(w/2-230,h/2+25,460,55),"Changes are kept for this session.\nDisk saves arrive in the next milestone.",centered);
                if (GUI.Button(new Rect(w/2-110,h/2+95,220,35),"Quit prototype"))
                {
                    #if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
                    #else
                    Application.Quit();
                    #endif
                }
            }
            GUI.matrix = Matrix4x4.identity;
        }
        private string SlotName(InventorySlot slot) => slot.Empty ? "Empty" : slot.IsTool ?
            (slot.tier == 1 ? "Wooden pickaxe" : "Stone pickaxe") : world.catalog.Get(slot.block).displayName;
    }
}
