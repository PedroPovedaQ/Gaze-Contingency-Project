Shader "TrialReview/IdlePyramid"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;};
            struct v2f {float4 vertex:SV_POSITION;float3 normal:TEXCOORD0;float3 view:TEXCOORD1;};
            v2f vert(appdata v) {
                v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.normal=UnityObjectToWorldNormal(v.normal);
                o.view=_WorldSpaceCameraPos-mul(unity_ObjectToWorld,v.vertex).xyz;return o;
            }
            float4 frag(v2f i):SV_Target {
                float3 n=normalize(i.normal),v=normalize(i.view),light=normalize(float3(-.6,.9,-.7));
                float diffuse=saturate(dot(n,light));
                float glint=pow(saturate(dot(n,normalize(light+v))),28);
                float rim=pow(1-saturate(dot(n,v)),3);
                float3 gold=float3(1,.788,.016)*(.38+.62*diffuse);
                return float4(gold+float3(1,.92,.65)*glint*.3+float3(.22,.14,.01)*rim,1);
            }
            ENDCG
        }
    }
}
