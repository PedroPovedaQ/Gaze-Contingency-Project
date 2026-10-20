Shader "TrialReview/ObjectShine"
{
    Properties { _Color ("Color", Color)=(1,1,1,1) _RimColor ("Rim", Color)=(0,0,0,0) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; };
            struct v2f { float4 vertex:SV_POSITION; float3 normal:TEXCOORD0; float3 view:TEXCOORD1; };
            float4 _Color, _RimColor;
            v2f vert(appdata v) {
                v2f o; o.vertex=UnityObjectToClipPos(v.vertex);
                o.normal=UnityObjectToWorldNormal(v.normal);
                o.view=_WorldSpaceCameraPos-mul(unity_ObjectToWorld,v.vertex).xyz; return o;
            }
            float4 frag(v2f i):SV_Target {
                float edge=pow(1-saturate(dot(normalize(i.normal),normalize(i.view))),1.5);
                return float4(_Color.rgb+_RimColor.rgb*edge*_RimColor.a,_Color.a);
            }
            ENDCG
        }
    }
}
