using System;
using UnityEngine;

/// <summary>Decorative idle animation, isolated from the replay camera and recorded clock.</summary>
public sealed class TrialReviewWaitingPyramid : IDisposable
{
    readonly GameObject m_Root;
    readonly Camera m_Camera;
    readonly Mesh m_Mesh;
    readonly Material m_Material;
    readonly RenderTexture m_Texture;
    public TrialReviewWaitingPyramid()
    {
        m_Root=new GameObject("Waiting pyramid"){layer=31};
        var a=new Vector3(-.7f,-.55f,-.7f);var b=new Vector3(.7f,-.55f,-.7f);
        var c=new Vector3(.7f,-.55f,.7f);var d=new Vector3(-.7f,-.55f,.7f);var top=new Vector3(0,.85f,0);
        m_Mesh=new Mesh {name="Idle pyramid",vertices=new[]{a,top,b,b,top,c,c,top,d,d,top,a,a,b,c,a,c,d},
            triangles=new[]{0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17}};
        m_Mesh.RecalculateNormals();m_Mesh.RecalculateBounds();
        m_Material=new Material(Resources.Load<Shader>("TrialReviewIdlePyramid"));
        m_Root.AddComponent<MeshFilter>().sharedMesh=m_Mesh;
        m_Root.AddComponent<MeshRenderer>().sharedMaterial=m_Material;
        m_Camera=new GameObject("Waiting pyramid camera").AddComponent<Camera>();
        m_Camera.enabled=false;m_Camera.cullingMask=1<<31;
        m_Camera.clearFlags=CameraClearFlags.SolidColor;m_Camera.backgroundColor=Color.clear;
        m_Camera.orthographic=true;m_Camera.orthographicSize=1.12f;
        m_Camera.nearClipPlane=.1f;m_Camera.farClipPlane=10;
        m_Camera.transform.position=new Vector3(0,.65f,-4);m_Camera.transform.LookAt(Vector3.zero);
        m_Texture=new RenderTexture(512,512,24,RenderTextureFormat.ARGB32){antiAliasing=4};
        m_Camera.targetTexture=m_Texture;m_Root.SetActive(false);
    }
    public Texture Render(double idleTime)
    {
        m_Root.transform.rotation=Quaternion.Euler(0,(float)(idleTime*12%360),0);
        m_Root.transform.position=Vector3.up*(.035f*Mathf.Sin((float)(idleTime%1000)*.8f));
        // Never leave the prop active for a subsequent reconstruction render.
        m_Root.SetActive(true);
        try {m_Camera.Render();return m_Texture;}
        finally {m_Root.SetActive(false);}
    }
    public void Dispose()
    {
        m_Texture.Release();UnityEngine.Object.DestroyImmediate(m_Texture);
        UnityEngine.Object.DestroyImmediate(m_Camera.gameObject);UnityEngine.Object.DestroyImmediate(m_Root);
        UnityEngine.Object.DestroyImmediate(m_Mesh);UnityEngine.Object.DestroyImmediate(m_Material);
    }
}
