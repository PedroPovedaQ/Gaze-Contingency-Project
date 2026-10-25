using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class TrialReviewAvatarSetup
{
    public const string ModelPath = "Assets/ThirdParty/KenneyProtagonists/characterMedium.fbx";
    static readonly Vector3 RightGripPosition=new Vector3(-.025f,-.055f,.095f);
    static readonly Quaternion RightGripRotation=Quaternion.LookRotation(Vector3.back,Vector3.up);
    static Transform Bone(GameObject model,string name) => model.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
    static void Point(Transform bone,Transform child,Vector3 direction)
    { bone.rotation=Quaternion.FromToRotation(child.position-bone.position,direction)*bone.rotation; }
    static void CloseHand(GameObject human,string side)
    {
        // This stylized rig uses one finger chain for the four-finger mitten.
        // Curl around the thumb-side axis; preserve the palm anchor used by controller IK.
        var palm=Bone(human,side+"PalmAnchor");
        float sign=side=="Left"?1:-1;
        float[] curl=side=="Right"?new[]{25f,110f,200f}:new[]{50f,130f,210f};
        for(int i=0;i<3;i++)
            Point(Bone(human,side+"HandIndex"+(i+1)),
                Bone(human,side+(i==2?"HandIndex3_end":"HandIndex"+(i+2))),
                Quaternion.AngleAxis(sign*curl[i],palm.up)*palm.forward);
        Vector3 inward=palm.right*sign;
        Point(Bone(human,side+"HandThumb1"),Bone(human,side+"HandThumb2"),
            palm.forward*.6f-palm.up*.4f+inward*.4f);
        Point(Bone(human,side+"HandThumb2"),Bone(human,side+"HandThumb2_end"),
            -palm.up+inward*.3f);
        if(side=="Right")
        {
            // Reconstruct the controller frame at this palm. Rest the thumb on
            // its button deck, while the mitten fingers curl beneath the handle.
            // Bake this pose once; runtime IK moves the whole hand with the grip.
            var rotation=palm.rotation*Quaternion.Inverse(RightGripRotation);
            var origin=palm.position-rotation*RightGripPosition;
            var thumb=Bone(human,"RightHandThumb1");
            var tip=Bone(human,"RightHandThumb2");
            Point(thumb,tip,origin+rotation*new Vector3(.005f,-.012f,.055f)-thumb.position);
            Point(tip,Bone(human,"RightHandThumb2_end"),origin+rotation*new Vector3(-.001f,-.014f,.034f)-tip.position);
        }
    }
    static void RestLeftHand(GameObject root,GameObject human)
    {
        // Bake a resting pose above the left thigh. Runtime restores these bone
        // rotations, so the fist remains on the lap when the body turns.
        var lap=new GameObject("LeftLapRestAnchor").transform;
        lap.SetParent(root.transform,false);
        lap.position=Vector3.Lerp(Bone(human,"LeftUpLeg").position,Bone(human,"LeftLeg").position,.55f)
            +new Vector3(.02f,.09f,0);
        lap.rotation=Quaternion.LookRotation(Vector3.forward,Vector3.right);
        new TrialReviewArm(root,"Left").Prepare(lap,root.transform);
    }
    static Material Material(string name,Color color,Texture texture=null)
    {
        string path="Assets/Resources/"+name+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(Shader.Find("TrialReview/Model"));AssetDatabase.CreateAsset(material,path);}
        material.color=color;material.mainTexture=texture;EditorUtility.SetDirty(material);return material;
    }
    static GameObject Part(string name,Transform parent,Vector3 position,Vector3 scale,Material material)
    {
        var part=GameObject.CreatePrimitive(PrimitiveType.Cube);part.name=name;part.transform.SetParent(parent,false);
        part.transform.localPosition=position;part.transform.localScale=new Vector3(scale.x/parent.lossyScale.x,scale.y/parent.lossyScale.y,scale.z/parent.lossyScale.z);
        UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());part.GetComponent<Renderer>().sharedMaterial=material;return part;
    }
    public static void Prepare()
    {
        Directory.CreateDirectory("Assets/Resources");AssetDatabase.Refresh();
        var model=AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if(model==null)throw new Exception("Missing Kenney avatar");
        var root=new GameObject("Illustrative seated participant");
        try
        {
            var human=UnityEngine.Object.Instantiate(model,root.transform);
            foreach(var animator in human.GetComponentsInChildren<Animator>())UnityEngine.Object.DestroyImmediate(animator);
            // A plain mannequin represents pose without suggesting the participant's face,
            // skin tone or clothing. Keep the existing rig and tracked-equipment anchors.
            var bodyMaterial=Material("AvatarNeutral",new Color(.62f,.64f,.66f));
            foreach(var renderer in human.GetComponentsInChildren<Renderer>()){renderer.sharedMaterials=renderer.sharedMaterials.Select(_=>bodyMaterial).ToArray();if(renderer is SkinnedMeshRenderer skinned)skinned.updateWhenOffscreen=true;}
            // Static seated illustration: bone directions, not recorded body kinematics.
            foreach(string side in new[]{"Left","Right"})
            {
                Point(Bone(human,side+"UpLeg"),Bone(human,side+"Leg"),Vector3.forward);
                Point(Bone(human,side+"Leg"),Bone(human,side+"Foot"),Vector3.down);
                Point(Bone(human,side+"Arm"),Bone(human,side+"ForeArm"),Vector3.down);
                Point(Bone(human,side+"ForeArm"),Bone(human,side+"Hand"),new Vector3(side=="Left"?.2f:-.2f,-.1f,1));
            }
            human.transform.localScale=Vector3.one*.46f;
            foreach(string side in new[]{"Left","Right"})
            {
                var hand=Bone(human,side+"Hand");var index=Bone(human,side+"HandIndex1");
                // Reduce the cartoon hand size so it grips rather than engulfs
                // the life-size controller. Palm anchors are measured after scaling.
                hand.localScale*=.65f;
                var thumb=Bone(human,side+"HandThumb1");
                var palm=new GameObject(side+"PalmAnchor").transform;palm.SetParent(hand,false);
                palm.position=Vector3.Lerp(hand.position,index.position,.7f);
                palm.rotation=Quaternion.LookRotation(index.position-hand.position,thumb.position-index.position);
            }
            RestLeftHand(root,human);
            CloseHand(human,"Left");
            CloseHand(human,"Right");
            var head=Bone(human,"Head");
            var anchor=new GameObject("RecordedHeadAnchor").transform;
            anchor.SetParent(head,false);
            // Stylized eye location, chosen in model space; no participant anthropometry inferred.
            anchor.position=head.position+new Vector3(0,.22f,.20f);anchor.rotation=Quaternion.identity;
            var headset=Material("IllustrativeHeadset",new Color(.08f,.10f,.13f));
            Part("Illustrative headset",anchor,Vector3.zero,new Vector3(.32f,.17f,.13f),headset);
            var seat=Material("IllustrativeChair",new Color(.19f,.28f,.32f));
            // Shift the complete chair behind the seated body so the seat does not cut through the knees.
            var hips=Bone(human,"Hips").position+Vector3.back*.14f;
            Part("Swivel seat",root.transform,hips+new Vector3(0,-.12f,.08f),new Vector3(.48f,.10f,.43f),seat);
            Part("Chair backrest",root.transform,hips+new Vector3(0,.18f,-.17f),new Vector3(.48f,.53f,.08f),seat);
            var frame=Material("ChairFrame",new Color(.38f,.42f,.46f));
            for(int side=-1;side<=1;side+=2)
            {
                Part("Armrest",root.transform,hips+new Vector3(side*.28f,.06f,.08f),new Vector3(.065f,.055f,.34f),seat);
                Part("Armrest support",root.transform,hips+new Vector3(side*.28f,-.04f,-.02f),new Vector3(.04f,.18f,.04f),frame);
            }
            var baseRoot=new GameObject("ChairBase").transform;baseRoot.SetParent(root.transform,false);
            baseRoot.localPosition=hips+new Vector3(0,-.59f,.07f);
            var column=GameObject.CreatePrimitive(PrimitiveType.Cylinder);column.name="Swivel pedestal";
            column.transform.SetParent(baseRoot,false);column.transform.localPosition=new Vector3(0,.23f,0);column.transform.localScale=new Vector3(.065f,.23f,.065f);
            UnityEngine.Object.DestroyImmediate(column.GetComponent<Collider>());column.GetComponent<Renderer>().sharedMaterial=frame;
            for(int i=0;i<5;i++)
            {
                float angle=i*72;var direction=Quaternion.Euler(0,angle,0)*Vector3.forward;
                var spoke=Part("Chair spoke",baseRoot,direction*.16f,new Vector3(.055f,.04f,.34f),frame);spoke.transform.localRotation=Quaternion.Euler(0,angle,0);
                var wheel=GameObject.CreatePrimitive(PrimitiveType.Cylinder);wheel.name="Chair caster";wheel.transform.SetParent(baseRoot,false);
                wheel.transform.localPosition=direction*.31f+Vector3.down*.035f;wheel.transform.localRotation=Quaternion.Euler(0,angle,90);wheel.transform.localScale=new Vector3(.085f,.025f,.085f);
                UnityEngine.Object.DestroyImmediate(wheel.GetComponent<Collider>());wheel.GetComponent<Renderer>().sharedMaterial=headset;
            }
            // Give the recorded hand/controller clearance in front of the
            // illustrative torso. The chair base remains at the layout center.
            foreach(Transform part in root.transform)
                if(part!=baseRoot)part.localPosition+=Vector3.back*.12f;
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/ReplayParticipant.prefab");
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
        foreach(string hand in new[]{"L","R"})
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/ViveControllers/Focus3Model"+hand+".fbx");
            if(source==null)throw new Exception("Missing official HTC controller model");
            var controller=new GameObject("Recorded controller "+hand);
            var visual=UnityEngine.Object.Instantiate(source,controller.transform);
            // The vendor mesh points along -Z; the recorded aim pose uses +Z.
            // Correct the mesh, not the recorded root transform or hit endpoint.
            visual.transform.localRotation=Quaternion.Euler(0,180,0);
            try
            {
                foreach(var animator in controller.GetComponentsInChildren<Animator>())UnityEngine.Object.DestroyImmediate(animator);
                var material=Material("ViveController",new Color(.12f,.14f,.16f));
                foreach(var renderer in controller.GetComponentsInChildren<Renderer>())renderer.sharedMaterials=renderer.sharedMaterials.Select(_=>material).ToArray();
                var bounds=controller.GetComponentsInChildren<Renderer>().Select(r=>r.bounds).Aggregate((a,b)=>{a.Encapsulate(b);return a;});
                if(bounds.size.magnitude<.05f||bounds.size.magnitude>.6f)throw new Exception("Controller imported at unexpected scale: "+bounds);
                var grip=new GameObject("GripAnchor").transform;grip.SetParent(controller.transform,false);
                // Place the palm beside the handle with fingers wrapping underneath
                // and the thumb resting on the controls. Mirror the unused left model.
                // This attachment is illustrative, not a measured grip pose.
                grip.localPosition=visual.transform.localRotation*Vector3.Scale(RightGripPosition,new Vector3(hand=="L"?-1:1,1,1));
                grip.localRotation=visual.transform.localRotation*RightGripRotation;
                var tip=new GameObject("RayTip").transform;tip.SetParent(controller.transform,false);
                tip.localPosition=visual.transform.localRotation*new Vector3(hand=="L"?-.01f:.01f,-.027f,.006f);
                Debug.Log("VIVE_CONTROLLER_BOUNDS "+hand+" "+bounds);
                PrefabUtility.SaveAsPrefabAsset(controller,"Assets/Resources/ReplayController"+hand+".prefab");
            }
            finally{UnityEngine.Object.DestroyImmediate(controller);}
        }
        AssetDatabase.SaveAssets();
    }
}
