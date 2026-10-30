using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

public static class StudyRoomChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Assert(bool value, string reason) { if (!value) throw new Exception("Room: " + reason); }
    public static void Run()
    {
        var host = new GameObject("Room checks"); host.SetActive(false);
        var game = host.AddComponent<FindObjectGameManager>();
        var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var center = new Vector3(100, 3.2f, -100);
        var rotation = Quaternion.Euler(0,37,0);
        try
        {
            typeof(FindObjectGameManager).GetField("m_SpawnCenter",Private).SetValue(game,center);
            typeof(FindObjectGameManager).GetField("m_SeatedRotation",Private).SetValue(game,rotation);
            typeof(FindObjectGameManager).GetField("m_FloorHeight",Private).SetValue(game,2f);
            Assert(!game.DegreeGuidesVisible,"Participant defaults hide debug outlines");
            var ray = new Ray(center,rotation*Vector3.forward);
            Assert(game.TryEyeDotPoint(ray.origin,ray.direction,out var before,out var beforePlane),"Layout exists without visual room");
            typeof(FindObjectGameManager).GetMethod("CreateRotationalFrames",Private).Invoke(game,null);
            var shelves = (List<GameObject>)typeof(FindObjectGameManager).GetField("m_ShelfObjects",Private).GetValue(game);
            var room = shelves[0];
            Assert(room.GetComponentsInChildren<Collider>(true).Length==0,"No room collider, including disabled children");
            Assert(room.GetComponentsInChildren<Renderer>(true).Length==5,"Floor and four distant walls");
            Assert(room.transform.position==new Vector3(100,2,-100),"Existing floor height and seated horizontal origin");
            Assert(room.transform.rotation==rotation,"Room follows seated yaw");
            foreach(var renderer in room.GetComponentsInChildren<Renderer>())
            {
                Assert(renderer.gameObject.layer==2,"Surfaces also use Ignore Raycast layer");
                Assert(renderer.shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.Off,"No new cast shadows");
            }
            for(int plane=0;plane<8;plane++)
            {
                Assert(!shelves[plane+1].activeSelf,"Every search frame starts hidden");
                for(int height=0;height<2;height++)
                {
                    var direction=rotation*Quaternion.Euler(0,plane*45,0)*Vector3.forward;
                    target.transform.position=new Vector3(center.x,2+(height==0?RotationalSearchLayout.DefaultMinHeight:RotationalSearchLayout.DefaultMaxHeight),center.z)+direction*1.5f;
                    target.transform.localScale=Vector3.one*.1f;
                    target.layer=8; Physics.SyncTransforms();
                    // All layers is stricter than either existing object-only search mask.
                    Assert(Physics.Raycast(center,(target.transform.position-center).normalized,out var hit,20,~0) &&
                        hit.collider.gameObject==target,"Unfiltered ray still reaches top/bottom object on all eight planes");
                }
            }
            foreach(bool visible in new[]{true,false})
            {
                game.SetDegreeGuidesVisible(visible);
                Assert(room.activeSelf,"Debug walls toggle leaves room visible");
                Assert(game.TryEyeDotPoint(ray.origin,ray.direction,out var after,out var plane) &&
                    before==after && plane==beforePlane,"Readiness/eye-dot search plane is independent of room and guides");
            }
            Assert(!Physics.Raycast(center,Vector3.down,20,~0),"Floor cannot intercept any physics ray");
            foreach(bool alreadyHidden in new[]{false,true})
            {
                var renderer=target.GetComponent<Renderer>(); renderer.forceRenderingOff=alreadyHidden;
                var temporary=StudyRoomEnvironment.Create(center,rotation,2f);
                temporary.GetComponent<StudyRoomEnvironment>().HideBackdrop(new[]{renderer});
                Assert(renderer.forceRenderingOff,"Template backdrop cannot occlude the new room");
                Object.DestroyImmediate(temporary);
                Assert(renderer.forceRenderingOff==alreadyHidden,"Room cleanup restores previous backdrop visibility");
            }
            Debug.Log("[StudyRoomChecks] PASS: hidden defaults, 10x10x6 room at existing floor/yaw, no colliders/shadows, all eight top/bottom rays, readiness geometry and independent debug toggle.");
        }
        finally
        {
            var shelves=(List<GameObject>)typeof(FindObjectGameManager).GetField("m_ShelfObjects",Private).GetValue(game);
            foreach(var shelf in shelves)Object.DestroyImmediate(shelf);
            var material=(Material)typeof(FindObjectGameManager).GetField("m_PlaneOutlineMaterial",Private).GetValue(game);
            typeof(FindObjectGameManager).GetField("m_PlaneOutlineMaterial",Private).SetValue(game,null);
            Object.DestroyImmediate(material); Object.DestroyImmediate(target); Object.DestroyImmediate(host);
        }
    }
}
