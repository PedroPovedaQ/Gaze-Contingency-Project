using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Illustrative room and inferred octagonal wall bounds; never used for replay measurements.</summary>
public sealed class TrialReviewEnvironment : IDisposable
{
    readonly GameObject m_Root=new GameObject("Replay spatial context");
    readonly GameObject m_Walls,m_Room;
    readonly List<Material> m_Materials=new List<Material>();
    readonly Material m_Outline,m_Surface,m_Grid;
    TrialReplayRecord m_Trial;
    public bool WallsVisible => m_Walls.activeSelf;
    public bool RoomVisible => m_Room.activeSelf;
    public int WallCount { get; private set; }
    public Vector3[] DegreePositions { get; private set; } = Array.Empty<Vector3>();
    public Vector3 Center { get; private set; }
    public float Radius { get; private set; }
    public float Floor { get; private set; }
    float m_WallTop;
    public TrialReviewEnvironment()
    {
        m_Walls=new GameObject("Inferred study walls");m_Walls.transform.SetParent(m_Root.transform);
        m_Room=new GameObject("Illustrative room");m_Room.transform.SetParent(m_Root.transform);
        m_Outline=Material(new Color(.26f,.47f,.51f));m_Surface=Material(new Color(.20f,.25f,.29f),true);
        m_Grid=Material(new Color(.20f,.30f,.34f));
        m_Walls.SetActive(false);m_Room.SetActive(false);
    }
    Material Material(Color color,bool lit=false)
    {
        var material=new Material(Shader.Find(lit?"TrialReview/Model":"Unlit/Color")){color=color};
        m_Materials.Add(material);return material;
    }
    public void Prepare(TrialReplayRecord trial,bool showWalls,bool showRoom)
    {
        if(!ReferenceEquals(m_Trial,trial))
        {
            m_Trial=trial;Clear(m_Walls);Clear(m_Room);WallCount=0;DegreePositions=Array.Empty<Vector3>();
            if(trial?.objects!=null&&trial.objects.Length>0)Build(trial);
        }
        m_Walls.SetActive(showWalls&&WallCount>0);
        m_Room.SetActive(showRoom&&m_Room.transform.childCount>0);
    }
    void Build(TrialReplayRecord trial)
    {
        Center=trial.origin;
        // Work in the snapshot's layout frame, not the moving participant/head frame.
        var inverse=Quaternion.Inverse(trial.originRotation);
        var points=trial.objects.Select(o=>inverse*(o.position-Center)).ToArray();
        float bottom=points.Min(p=>p.y)-.2f,top=points.Max(p=>p.y)+.25f;
        Floor=bottom;float height=Mathf.Max(1,top-bottom);
        m_WallTop=bottom+height;
        var groups=trial.objects.Where(o=>o.planeId>=0&&o.planeId<8).GroupBy(o=>o.planeId).ToArray();
        bool octagon=trial.layout!=null&&trial.layout.StartsWith("rotational_",StringComparison.Ordinal)&&groups.Length==8;
        Radius=Mathf.Max(1,points.Max(p=>new Vector2(p.x,p.z).magnitude));
        if(octagon)
        {
            // Radius is inferred from object-plane distance. Panel height spans the saved objects;
            // neither these outlines nor the room imply recorded visibility or physical room geometry.
            var radii=new List<float>();
            foreach(var group in groups)
            {
                var normal=Quaternion.Euler(0,group.Key*45,0)*Vector3.forward;
                radii.Add(group.Average(o=>Vector3.Dot(inverse*(o.position-Center),normal)));
            }
            float radius=radii.Average();
            if(radius>.1f)
            {
                Radius=radius;
                DegreePositions=new Vector3[8];
                float half=radius*Mathf.Tan(Mathf.PI/8);
                foreach(var group in groups)
                {
                    var rotation=Quaternion.Euler(0,group.Key*45,0);
                    // Match the headset's layout-relative label height and forward convention.
                    DegreePositions[group.Key]=Center+trial.originRotation*(rotation*new Vector3(0,.43f,radius));
                    Vector3 a=rotation*new Vector3(-half,bottom,radius),b=rotation*new Vector3(half,bottom,radius);
                    Vector3 c=b+Vector3.up*height,d=a+Vector3.up*height;
                    Line(m_Walls,new[]{a,b,c,d,a},m_Outline,.012f);
                    WallCount++;
                }
            }
        }
        float roomRadius=Mathf.Max(Radius+1.5f,points.Max(p=>new Vector2(p.x,p.z).magnitude)+.8f);
        float roomHeight=Mathf.Max(3,height+.5f);
        Panel(m_Room,new Vector3(0,bottom,0),Quaternion.Euler(90,0,0),new Vector2(roomRadius*2,roomRadius*2));
        for(int i=0;i<4;i++)
        {
            var rotation=Quaternion.Euler(0,i*90,0);
            Panel(m_Room,rotation*new Vector3(0,bottom+roomHeight/2,roomRadius),rotation,new Vector2(roomRadius*2,roomHeight));
        }
        for(float x=-Mathf.Floor(roomRadius);x<=roomRadius;x+=1)
        {
            Line(m_Room,new[]{new Vector3(x,bottom+.005f,-roomRadius),new Vector3(x,bottom+.005f,roomRadius)},m_Grid,.006f);
            Line(m_Room,new[]{new Vector3(-roomRadius,bottom+.005f,x),new Vector3(roomRadius,bottom+.005f,x)},m_Grid,.006f);
        }
        // Single-sided room walls are a cutaway when the orbit camera is outside the room.
        foreach(var root in new[]{m_Walls,m_Room})root.transform.SetPositionAndRotation(Center,trial.originRotation);
    }
    // Use the same finite panels as the reconstruction, not object colliders or
    // an arbitrary distance along the gaze. This is a display projection only.
    public bool TryProjectGaze(Ray ray,out Vector3 point)
    {
        point=default;
        if(WallCount!=8||ray.direction.sqrMagnitude<.000001f)return false;
        Quaternion inverse=Quaternion.Inverse(m_Trial.originRotation);
        Vector3 origin=inverse*(ray.origin-Center),direction=inverse*ray.direction.normalized;
        float nearest=float.PositiveInfinity,half=Radius*Mathf.Tan(Mathf.PI/8);
        for(int i=0;i<8;i++)
        {
            var rotation=Quaternion.Euler(0,i*45,0);
            Vector3 normal=rotation*Vector3.forward;
            float denominator=Vector3.Dot(direction,normal);
            if(Mathf.Abs(denominator)<.000001f)continue;
            float distance=(Radius-Vector3.Dot(origin,normal))/denominator;
            if(distance<=0||distance>=nearest)continue;
            Vector3 hit=origin+direction*distance;
            float across=Vector3.Dot(hit,rotation*Vector3.right);
            if(Mathf.Abs(across)>half+.0001f||hit.y<Floor||hit.y>m_WallTop)continue;
            nearest=distance;point=Center+m_Trial.originRotation*hit;
        }
        return !float.IsInfinity(nearest);
    }
    void Panel(GameObject parent,Vector3 center,Quaternion rotation,Vector2 size)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Quad);go.name="Illustrative room surface";
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.transform.SetParent(parent.transform,false);
        go.transform.localPosition=center;go.transform.localRotation=rotation;go.transform.localScale=new Vector3(size.x,size.y,1);
        go.GetComponent<Renderer>().sharedMaterial=m_Surface;
    }
    static void Line(GameObject parent,Vector3[] points,Material material,float width)
    {
        var line=new GameObject("Spatial guide").AddComponent<LineRenderer>();line.transform.SetParent(parent.transform,false);
        line.useWorldSpace=false;line.positionCount=points.Length;line.SetPositions(points);
        line.startWidth=line.endWidth=width;line.sharedMaterial=material;
    }
    static void Clear(GameObject root)
    { while(root.transform.childCount>0)UnityEngine.Object.DestroyImmediate(root.transform.GetChild(0).gameObject);root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity); }
    public void Dispose()
    { UnityEngine.Object.DestroyImmediate(m_Root);foreach(var material in m_Materials)UnityEngine.Object.DestroyImmediate(material); }
}
