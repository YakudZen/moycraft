using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace VoxelSurvival
{
    // Opt-in executable checks: Moycraft.exe -smokeTests -smokeOutput <folder>.
    public sealed class RuntimeSmokeTests : MonoBehaviour
    {
        private bool failed;
        private int checks;
        private string output;
        private PrototypeBootstrap game;
        private void Check(bool condition,string message)
        {
            if(!condition) {failed=true; Debug.LogError("RUNTIME_CHECK_FAILED: "+message);}
            else {checks++; Debug.Log("RUNTIME_PASS: "+message);}
        }
        private void OnLog(string text,string trace,LogType type)
        {
            if(type==LogType.Exception || type==LogType.Error || type==LogType.Assert) failed=true;
        }
        private IEnumerator Start()
        {
            Application.logMessageReceived+=OnLog;
            game=GetComponent<PrototypeBootstrap>(); game.Player.InputEnabled=false; game.Player.SetPaused(false);
            Application.runInBackground=true;
            var args=Environment.GetCommandLineArgs(); int index=Array.IndexOf(args,"-smokeOutput");
            output=index>=0 && index+1<args.Length ? args[index+1] : Application.persistentDataPath;
            Directory.CreateDirectory(output);
            for(int i=0;i<80;i++) { game.Player.Move(Vector2.zero,false,false,1f/60); yield return null; }
            Check(game.World.LoadedCount>=49,"Streaming loaded the view radius");
            Check(game.Player.Controller.isGrounded,"Player settles on terrain");
            float startY=game.Player.transform.position.y;
            game.Player.Move(Vector2.zero,true,false,1f/60);
            for(int i=0;i<8;i++) { game.Player.Move(Vector2.zero,false,false,1f/60); yield return null; }
            Check(game.Player.transform.position.y>startY+0.3f,"Jump rises above terrain");
            for(int i=0;i<80;i++) { game.Player.Move(Vector2.zero,false,false,1f/60); yield return null; }
            Check(game.Player.Controller.isGrounded,"Jump lands on the mesh collider");
            var feet=Vector3Int.FloorToInt(game.Player.transform.position+Vector3.up*0.1f);
            game.Inventory.Select(0);
            Check(!game.Interaction.TryPlace(feet),"Placement cannot overlap the player");
            var target=new Vector3Int(10,45,8);
            game.Player.Teleport(new Vector3(8.5f,45,8.5f)); Physics.SyncTransforms();
            game.Inventory.Select(2); int before=game.Inventory.Current.count;
            Check(game.Interaction.TryPlace(target),"Player places a stone block");
            Check(game.Inventory.Current.count==before-1,"Placement consumes one item");
            Check(!game.Interaction.TryBreak(target),"Bare hand cannot harvest stone");
            game.Inventory.Select(7);
            Check(game.Interaction.TryBreak(target),"Pickaxe harvests stone");
            Check(game.Inventory.Slots[2].count==before,"Mining returns the block to inventory");
            game.Inventory.Select(0);
            Check(!game.Interaction.TryPlace(new Vector3Int(15,45,15)),"Out-of-reach placement is rejected");
            // Exercise collision against a wall at the exact seam between two chunks.
            for(int y=45;y<=48;y++) for(int z=7;z<=9;z++) game.World.SetBlock(new Vector3Int(16,y,z),BlockId.Stone);
            for(int x=13;x<=17;x++) for(int z=7;z<=9;z++) game.World.SetBlock(new Vector3Int(x,44,z),BlockId.Stone);
            game.Player.Teleport(new Vector3(14.5f,45.05f,8.5f)); game.Player.transform.rotation=Quaternion.Euler(0,90,0);
            Physics.SyncTransforms();
            for(int i=0;i<50;i++) {game.Player.Move(Vector2.up,false,false,1f/60); yield return null;}
            Check(game.Player.transform.position.x<15.8f && game.Player.transform.position.x>15,"Chunk seam wall blocks walking");
            for(int y=45;y<=48;y++) for(int z=7;z<=9;z++) game.World.SetBlock(new Vector3Int(16,y,z),BlockId.Air);
            for(int i=0;i<20;i++) {game.Player.Move(Vector2.up,false,false,1f/60); yield return null;}
            Check(game.Player.transform.position.x>16,"Removing wall updates collision across chunk seam");
            // Restore temporary test structures before the visual capture.
            for(int x=13;x<=17;x++) for(int z=7;z<=9;z++) game.World.SetBlock(new Vector3Int(x,44,z),BlockId.Air);
            int ground=game.World.Generator.Surface(8,8);
            game.Player.Teleport(new Vector3(8.5f,ground+1.05f,8.5f)); game.Player.transform.rotation=Quaternion.Euler(0,35,0);
            game.Player.eyes.transform.localRotation=Quaternion.Euler(16,0,0);
            game.Inventory.Select(0);
            for(int i=0;i<12;i++) yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"prototype.png"));
            for(int i=0;i<12;i++) yield return null;
            File.WriteAllText(Path.Combine(output,"runtime-results.json"),"{\"passed\":"+(!failed?"true":"false")+",\"checks\":"+checks+"}");
            Debug.Log("RUNTIME_SMOKE_"+(failed?"FAILED":"OK")+": "+checks);
            Application.Quit(failed?1:0);
        }
        private void OnDestroy() => Application.logMessageReceived-=OnLog;
    }
}
