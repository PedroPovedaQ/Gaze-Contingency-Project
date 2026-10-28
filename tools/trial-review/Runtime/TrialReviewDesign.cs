using UnityEngine;

public sealed partial class TrialReviewApp
{
    Color m_Info,m_Ok,m_Warn,m_ErrorColor,m_Raised;
    GUIStyle m_Mono,m_Clock,m_Card,m_Unavailable;
    Font m_MonoFont;
    Texture2D m_CardTexture,m_ScrollTexture;
    Texture2D Rounded(Color color)
    {
        const int size=16;
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp};
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            float dx=Mathf.Max(6-(x+.5f),x+.5f-10,0),dy=Mathf.Max(6-(y+.5f),y+.5f-10,0);
            var pixel=color;pixel.a*=Mathf.Clamp01(6.5f-Mathf.Sqrt(dx*dx+dy*dy));texture.SetPixel(x,y,pixel);
        }
        texture.Apply();return texture;
    }
    void DesignTokens()
    {
        m_Info=m_LightTheme?new Color32(31,95,175,255):new Color32(90,169,255,255);
        m_Ok=m_LightTheme?new Color32(30,127,79,255):new Color32(76,195,138,255);
        m_Warn=m_LightTheme?new Color32(163,84,0,255):new Color32(255,159,67,255);
        m_ErrorColor=m_LightTheme?new Color32(180,35,24,255):new Color32(255,93,93,255);
        m_Raised=new Color(Mathf.Min(1,m_Surface.r+.03f),Mathf.Min(1,m_Surface.g+.03f),Mathf.Min(1,m_Surface.b+.03f));
        if(m_MonoFont==null)m_MonoFont=Font.CreateDynamicFontFromOSFont(new[]{"Menlo","Monaco"},14);
        m_Mono=new GUIStyle(m_Label){font=m_MonoFont,wordWrap=false};
        m_Clock=new GUIStyle(m_Mono){fontSize=20};
        m_Unavailable=new GUIStyle(m_MutedLabel){fontStyle=FontStyle.Italic};
        if(m_CardTexture!=null)Destroy(m_CardTexture);
        m_CardTexture=Rounded(m_Raised);
        if(m_ScrollTexture!=null)Destroy(m_ScrollTexture);
        m_ScrollTexture=Rounded(m_Border);
        GUI.skin.verticalScrollbar=new GUIStyle{fixedWidth=8,normal={background=m_Panel},margin=new RectOffset(4,0,0,0)};
        GUI.skin.verticalScrollbarThumb=new GUIStyle{fixedWidth=8,border=new RectOffset(6,6,6,6),normal={background=m_ScrollTexture},hover={background=m_ScrollTexture},active={background=m_ScrollTexture}};
        GUI.skin.verticalScrollbarUpButton=new GUIStyle{fixedHeight=0};GUI.skin.verticalScrollbarDownButton=new GUIStyle{fixedHeight=0};
        m_Card=new GUIStyle{normal={background=m_CardTexture},border=new RectOffset(6,6,6,6),padding=new RectOffset(12,12,12,12),margin=new RectOffset(0,0,4,8)};
    }
    void StatusChip(Rect rect,string text,Color color,bool outline=false)
    {
        GUI.DrawTexture(rect,Texture2D.whiteTexture,ScaleMode.StretchToFill,false,0,outline?color:new Color(color.r,color.g,color.b,.14f),0,6);
        if(outline)GUI.DrawTexture(new Rect(rect.x+1,rect.y+1,rect.width-2,rect.height-2),Texture2D.whiteTexture,ScaleMode.StretchToFill,false,0,m_Surface,0,5);
        GUI.Label(rect,text,new GUIStyle(m_Small){normal={textColor=color},alignment=TextAnchor.MiddleCenter});
    }
}
