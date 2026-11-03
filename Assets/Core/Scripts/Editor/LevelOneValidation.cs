using System;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace Blocks.Gameplay.Core.LevelOneAuthoring
{
    public static class LevelOneValidation
    {
        public static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("LVL1 validation failed: " + message);
        }

        public static void ValidateBoss()
        {
            LevelOneSceneBuilder.FindEncounter();
            var boss = LevelOneSceneBuilder.Boss;
            var route = boss.route;
            Check(route.waypoints.Length > 2 && route.Length > 100, "authored route");
            Check(boss.checkpoints.Length == 5 && boss.bossVisual != null, "boss and checkpoints wired");
            Check(boss.footsteps.clip != null, "Platformer impact audio assigned");
            foreach(var cp in boss.checkpoints) Check(cp.dad != null && cp.child != null, "paired checkpoint");
            // Exercise duplicate nodes, overshoot and projection independently of scene geometry.
            var go = new GameObject("RouteTest");
            try
            {
                var test=go.AddComponent<ChaseWaypointRoute>();
                test.waypoints = new Transform[4];
                var positions = new[] { Vector3.zero, Vector3.zero, new Vector3(3,0,0), new Vector3(3,0,4) };
                for(int i=0;i<4;i++) { var p=new GameObject("point").transform; p.parent=go.transform;p.position=positions[i];test.waypoints[i]=p; }
                test.Rebuild();
                Check(Mathf.Approximately(test.Length,7),"route arc length");
                Check(Vector3.Distance(test.Sample(5,out _),new Vector3(3,0,2))<0.001f,"constant speed through corner");
                Check(test.Sample(100,out _)==positions[3],"no endpoint overshoot");
                Check(Mathf.Abs(test.Project(new Vector3(4,0,2))-5)<0.001f,"checkpoint projection");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
            Directory.CreateDirectory("AI_Reports/tests/LVL1");
            File.WriteAllText("AI_Reports/tests/LVL1/boss-editmode.txt", "PASS: scene references, 5 paired checkpoints, audio reuse, route length, duplicate waypoints, corner sampling, endpoint clamp, projection.\n");
            Debug.Log("[LVL1] Boss edit-mode validation PASS");
        }
    }
}
