using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class TrialReviewAvatarSetup
{
    public const string ModelPath = "Assets/ThirdParty/KenneyProtagonists/characterMedium.fbx";
    static Transform Bone(GameObject model,string name) => model.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
    static void Point(Transform bone,Transform child,Vector3 direction)
    { bone.rotation=Quaternion.FromToRotation(child.position-bone.position,direction)*bone.rotation; }
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
                var thumb=Bone(human,side+"HandThumb1");
                var palm=new GameObject(side+"PalmAnchor").transform;palm.SetParent(hand,false);
                palm.position=Vector3.Lerp(hand.position,index.position,.7f);
                palm.rotation=Quaternion.LookRotation(index.position-hand.position,thumb.position-index.position);
            }
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
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/ReplayParticipant.prefab");
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
        foreach(string hand in new[]{"L","R"})
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/ViveControllers/Focus3Model"+hand+".fbx");
            if(source==null)throw new Exception("Missing official HTC controller model");
            var controller=UnityEngine.Object.Instantiate(source);
            try
            {
                foreach(var animator in controller.GetComponentsInChildren<Animator>())UnityEngine.Object.DestroyImmediate(animator);
                var material=Material("ViveController",new Color(.43f,.47f,.52f));
                foreach(var renderer in controller.GetComponentsInChildren<Renderer>())renderer.sharedMaterials=renderer.sharedMaterials.Select(_=>material).ToArray();
                var bounds=controller.GetComponentsInChildren<Renderer>().Select(r=>r.bounds).Aggregate((a,b)=>{a.Encapsulate(b);return a;});
                if(bounds.size.magnitude<.05f||bounds.size.magnitude>.6f)throw new Exception("Controller imported at unexpected scale: "+bounds);
                var grip=new GameObject("GripAnchor").transform;grip.SetParent(controller.transform,false);
                // Approximate handle center in the official mesh; visual attachment, not a measured grip pose.
                grip.localPosition=new Vector3(hand=="L"?-.012f:.012f,-.045f,.075f);
                grip.localRotation=Quaternion.LookRotation(Vector3.up,hand=="L"?Vector3.left:Vector3.right);
                Debug.Log("VIVE_CONTROLLER_BOUNDS "+hand+" "+bounds);
                PrefabUtility.SaveAsPrefabAsset(controller,"Assets/Resources/ReplayController"+hand+".prefab");
            }
            finally{UnityEngine.Object.DestroyImmediate(controller);}
        }
        AssetDatabase.SaveAssets();
    }
}
