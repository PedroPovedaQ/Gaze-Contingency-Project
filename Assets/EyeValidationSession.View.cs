using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.UI;

public sealed partial class EyeValidationSession
{
    // Reserved only for this isolated presentation; TagManager layer 31 is otherwise unused.
    const int ValidationLayer=31;
    GameObject m_Root,m_Dot,m_Panel,m_Buttons,m_Background;
    TextMeshProUGUI m_Text,m_Cue;
    Camera m_Camera;
    int m_OldMask;
    Color m_OldBackground;
    CameraClearFlags m_OldClear;
    Vector3 m_Anchor;
    Quaternion m_Heading;
    readonly Dictionary<Canvas,bool> m_Canvases=new Dictionary<Canvas,bool>();
    readonly List<Material> m_Materials=new List<Material>();
    readonly List<GameObject> m_Search=new List<GameObject>();
    LineRenderer m_Pointer;
    EyeDot m_EyeDot;
    bool m_OldDot, m_StopHeld;
    [Serializable] sealed class SearchLayout {public int target;public Vector3[] positions;public string[] objects;}
    [Serializable] sealed class ControllerRow
    {
        public string kind="validation_controller",validationId;
        public int step,frame,triggerHeld,tracked;
        public double time;
        public bool available;
        public Vector3 origin,direction;
    }
    void CreateView()
    {
        m_Camera=Camera.main;m_OldMask=m_Camera.cullingMask;m_OldBackground=m_Camera.backgroundColor;m_OldClear=m_Camera.clearFlags;
        m_Anchor=m_Camera.transform.position;m_Heading=Quaternion.Euler(0,m_Camera.transform.eulerAngles.y,0);
        HideCanvases();
        m_EyeDot=FindFirstObjectByType<EyeDot>();m_OldDot=m_EyeDot!=null && m_EyeDot.Visible;m_EyeDot?.SetVisible(false);
        m_Root=new GameObject("Eye validation presentation");m_Root.layer=ValidationLayer;
        m_Root.AddComponent<EyeValidationControllerSampler>().Session=this;
        m_Root.transform.SetPositionAndRotation(m_Anchor,m_Heading);
        m_Background=Primitive("Controlled gray background",PrimitiveType.Quad,new Vector3(0,0,4),new Vector3(30,30,1),Color.gray,false);
        m_Dot=Primitive("Validation target",PrimitiveType.Sphere,new Vector3(0,0,2.4f),Vector3.one*.018f,Color.black,false);
        m_Panel=new GameObject("Validation instructions",typeof(RectTransform),typeof(Canvas),typeof(TrackedDeviceGraphicRaycaster));
        m_Panel.transform.SetParent(m_Root.transform,false);m_Panel.transform.localPosition=new Vector3(0,0,1.2f);m_Panel.transform.localScale=Vector3.one*.001f;
        var canvasUI=m_Panel.GetComponent<Canvas>();canvasUI.renderMode=RenderMode.WorldSpace;canvasUI.worldCamera=m_Camera;
        m_Panel.GetComponent<RectTransform>().sizeDelta=new Vector2(900,650);
        m_Text=Text(m_Panel.transform,new Vector2(0,130),new Vector2(860,280),30);
        m_Buttons=new GameObject("Response controls",typeof(RectTransform));m_Buttons.transform.SetParent(m_Panel.transform,false);
        var cueCanvas=new GameObject("Blink cue",typeof(RectTransform),typeof(Canvas));cueCanvas.transform.SetParent(m_Root.transform,false);
        cueCanvas.transform.localPosition=new Vector3(0,-.08f,1.2f);cueCanvas.transform.localScale=Vector3.one*.001f;
        cueCanvas.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
        m_Cue=Text(cueCanvas.transform,Vector2.zero,new Vector2(300,70),30);
        var pointer=new GameObject("Validation controller pointer",typeof(LineRenderer));pointer.transform.SetParent(m_Root.transform,false);
        m_Pointer=pointer.GetComponent<LineRenderer>();m_Pointer.positionCount=2;m_Pointer.startWidth=m_Pointer.endWidth=.002f;m_Pointer.material=Material(Color.cyan);
        SetLayer(m_Root.transform);
        m_Camera.cullingMask=1<<ValidationLayer;m_Camera.clearFlags=CameraClearFlags.SolidColor;m_Camera.backgroundColor=new Color(.3f,.3f,.3f,1);
    }
    Material Material(Color color)
    {
        var shader=Shader.Find("Universal Render Pipeline/Unlit")??Shader.Find("Unlit/Color");
        if(shader==null)throw new InvalidOperationException("Validation unlit shader unavailable");
        var material=new Material(shader);material.color=color;if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
        m_Materials.Add(material);return material;
    }
    GameObject Primitive(string name,PrimitiveType type,Vector3 pos,Vector3 size,Color color,bool collider)
    {
        var obj=GameObject.CreatePrimitive(type);obj.name=name;obj.transform.SetParent(m_Root.transform,false);obj.transform.localPosition=pos;obj.transform.localScale=size;
        obj.layer=ValidationLayer;obj.GetComponent<Renderer>().sharedMaterial=Material(color);if(!collider)Destroy(obj.GetComponent<Collider>());return obj;
    }
    static TextMeshProUGUI Text(Transform parent,Vector2 pos,Vector2 size,float font)
    {
        var obj=new GameObject("Text",typeof(RectTransform),typeof(TextMeshProUGUI));obj.transform.SetParent(parent,false);
        var text=obj.GetComponent<TextMeshProUGUI>();text.rectTransform.anchoredPosition=pos;text.rectTransform.sizeDelta=size;
        text.fontSize=font;text.color=Color.white;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;return text;
    }
    static void SetLayer(Transform root){root.gameObject.layer=ValidationLayer;foreach(Transform child in root)SetLayer(child);}
    void RenderStage()
    {
        if(!Active)return;
        foreach(Transform child in m_Buttons.transform){child.gameObject.SetActive(false);Destroy(child.gameObject);}
        m_Dot.SetActive(m_Progress.stage=="running" && Step.exercise!="search");
        m_Panel.SetActive(m_Progress.stage!="running");m_Cue.text="";
        Color gray=new Color(Step.gray,Step.gray,Step.gray,1);
        m_Background.GetComponent<Renderer>().sharedMaterial.color=gray;
        if(m_Background.GetComponent<Renderer>().sharedMaterial.HasProperty("_BaseColor"))m_Background.GetComponent<Renderer>().sharedMaterial.SetColor("_BaseColor",gray);
        m_Camera.backgroundColor=gray;
        PlaceDot(Step.yaw,Step.pitch,Step.distance);
        if(m_Progress.stage=="instruction")
        {
            m_Text.text=$"<b>{Step.exercise.Replace('_',' ')} · {m_Progress.index+1}/{m_Steps.Length}</b>\n\n{Step.prompt}\n\n"+
                (Step.exercise=="arithmetic"?"Remember the problem. Solve while looking at the central dot. Answer when buttons appear.":"Press Begin when ready. B on the right controller stops the session.");
            if(m_StudyAccuracy)m_Text.text="<b>Now checking accuracy</b>\n\nKeep your head still and follow the five dots with your eyes.\nStarting automatically in 3 seconds.\nB on the right controller stops the check.";
            if(m_StudyAccuracy)RayMenuFeedback.CreateButton(m_Buttons.transform,"Skip (no offset)",new Vector2(0,-160),new Vector2(360,65),()=>End("skipped","Study accuracy check skipped"));
            if(Step.exercise!="arithmetic" && !m_StudyAccuracy)RayMenuFeedback.CreateButton(m_Buttons.transform,"Begin",new Vector2(0,-75),new Vector2(300,65),()=>Next("headset_button"));
        }
        if(m_Progress.stage=="response")
        {
            m_Text.text=m_Rating?"How difficult was that block?\n1 = very easy · 7 = very difficult":"Choose your answer.\nThe researcher can also enter your spoken answer in Trial Viewer.";
            var values=new List<int>();
            if(m_Rating)for(int i=1;i<=7;i++)values.Add(i);
            else
            {
                values.Add(Step.answer);var rng=new System.Random(m_Request.seed^m_Progress.index);
                while(values.Count<4){int n=Math.Max(0,Step.answer+rng.Next(-8,9));if(!values.Contains(n))values.Add(n);}
                values.Sort();
            }
            int revision=m_Progress.revision;
            for(int i=0;i<values.Count;i++)
            {
                int answer=values[i];
                RayMenuFeedback.CreateButton(m_Buttons.transform,answer.ToString(),new Vector2((i%4-1.5f)*190,-70-(i/4)*85),new Vector2(170,65),()=>
                {if(Active && revision==m_Progress.revision)Respond(answer,"headset_button");});
            }
            RayMenuFeedback.CreateButton(m_Buttons.transform,"Skip / no answer",new Vector2(0,-245),new Vector2(320,60),()=>SkipResponse(revision));
        }
        if(m_Progress.stage=="running" && Step.exercise=="search")CreateSearch();
        SetLayer(m_Panel.transform);
    }
    void SkipResponse(int revision)
    {
        if(!Active || m_Progress.stage!="response" || revision!=m_Progress.revision)return;
        Event(m_Rating?"difficulty_missing":"answer_missing","headset_button");
        if(!m_Rating && Step.rateAfter){m_Rating=true;m_Progress.revision++;m_Progress.since=Now;RenderStage();UpdateStatus();Event("rating_prompt");}
        else {m_Progress.Answer(revision,Now);EnterStep();}
    }
    void PlaceDot(float yaw,float pitch,float distance)
    {m_Dot.transform.localPosition=new Vector3(Mathf.Tan(yaw*Mathf.Deg2Rad)*distance,Mathf.Tan(pitch*Mathf.Deg2Rad)*distance,distance);m_Dot.transform.localScale=Vector3.one*(distance*.0075f);}
    void MoveStimulus()
    {if(Step.exercise=="pursuit")PlaceDot(12*Mathf.Sin((float)(Now-m_Progress.since)*Mathf.PI*.4f),0,Step.distance);}
    void UpdateView()
    {
        if(!Active)return;
        m_Cue.text=m_Progress.stage=="running" && Step.exercise=="blinks" && Now-m_Progress.since>=4 && Now-m_Progress.since<4.8?"BLINK":"";
    }
    void CreateSearch()
    {
        var layout=new SearchLayout {target=Step.answer,positions=new Vector3[9],objects=new string[9]};
        for(int i=0;i<9;i++)
        {
            bool target=i==Step.answer;var type=target||i%2==0?PrimitiveType.Cube:PrimitiveType.Sphere;
            Color color=target||i%2==1?Color.red:Color.blue;
            var obj=Primitive("search_"+i,type,new Vector3((i%3-1)*.38f,(i/3-1)*.32f,2.4f),Vector3.one*.1f,color,true);
            m_Search.Add(obj);layout.positions[i]=obj.transform.position;layout.objects[i]=(color==Color.red?"red_":"blue_")+type;
        }
        m_Dot.transform.position=m_Search[Step.answer].transform.position;
        Event("search_layout","headset",JsonUtility.ToJson(layout));
        m_TriggerHeld=m_Controller==null || m_Controller.RightTelemetry.triggerHeld!=0;
    }
    void ClearSearch(){foreach(var obj in m_Search)if(obj!=null){obj.SetActive(false);Destroy(obj);}m_Search.Clear();}
    public void CaptureControllerFrame()
    {
        if(!Active)return;
        try
        {
            var device=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if(device.TryGetFeatureValue(CommonUsages.secondaryButton,out bool stop))
            {if(stop && !m_StopHeld){End("aborted","Right controller B pressed");return;}m_StopHeld=stop;}
            if(m_Controller==null)return;
            var input=m_Controller.RightTelemetry;
            if(!m_Samples.AppendJson(JsonUtility.ToJson(new ControllerRow {validationId=Status.id,step=m_Progress.index,frame=Time.frameCount,time=Now,
                origin=input.rayOrigin,direction=input.rayDirection,available=input.rayAvailable,tracked=input.tracked,triggerHeld=input.triggerHeld})))
                throw new System.IO.IOException(m_Samples.Error??"Controller recording stopped");
            m_Pointer.enabled=input.rayAvailable && (m_Progress.stage!="running" || Step.exercise=="search");
            if(m_Pointer.enabled){m_Pointer.SetPosition(0,input.rayOrigin);m_Pointer.SetPosition(1,input.rayOrigin+input.rayDirection*2.4f);}
            bool pressed=input.triggerHeld==1 && !m_TriggerHeld;m_TriggerHeld=input.triggerHeld!=0;
            if(pressed && input.rayAvailable && m_Progress.stage=="running" && Step.exercise=="search")
            {
                int hit=-1;float closest=float.PositiveInfinity;
                for(int i=0;i<m_Search.Count;i++)if(m_Search[i].GetComponent<Collider>().Raycast(new Ray(input.rayOrigin,input.rayDirection),out var h,10) && h.distance<closest){hit=i;closest=h.distance;}
                Event("search_selection","right_controller",hit==Step.answer?"correct":hit<0?"no_object":"incorrect",hit,true);
                if(hit==Step.answer)FinishStimulus();
            }
        }catch(Exception e){End("error",e.Message);}
    }
    void HideCanvases()
    {
        foreach(var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {m_Canvases[canvas]=canvas.enabled;canvas.enabled=false;}
    }
    void RestoreCanvases()
    {foreach(var pair in m_Canvases)if(pair.Key!=null)pair.Key.enabled=pair.Value;m_Canvases.Clear();}
    void DestroyView()
    {
        if(m_Camera!=null){m_Camera.cullingMask=m_OldMask;m_Camera.clearFlags=m_OldClear;m_Camera.backgroundColor=m_OldBackground;}
        RestoreCanvases();
        m_EyeDot?.SetVisible(m_OldDot);ClearSearch();if(m_Root!=null){m_Root.SetActive(false);Destroy(m_Root);}
        foreach(var material in m_Materials)if(material!=null)Destroy(material);m_Materials.Clear();m_Camera=null;
    }
}

// The session updates stimuli before gaze sampling (-100); controller input is
// sampled by ControllerRaySelector at 200, so consume it separately afterward.
[DefaultExecutionOrder(250)]
public sealed class EyeValidationControllerSampler : MonoBehaviour
{
    public EyeValidationSession Session;
    void LateUpdate(){if(Session!=null)Session.CaptureControllerFrame();}
}
