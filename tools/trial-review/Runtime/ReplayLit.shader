Shader "TrialReview/Model"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} _Color ("Tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; };
            struct v2f { float4 position:SV_POSITION; float3 normal:TEXCOORD0; float2 uv:TEXCOORD1; };
            sampler2D _MainTex; fixed4 _Color;
            v2f vert(appdata v) { v2f o; o.position=UnityObjectToClipPos(v.vertex); o.normal=UnityObjectToWorldNormal(v.normal); o.uv=v.uv; return o; }
            fixed4 frag(v2f i):SV_Target
            {
                float light=.42+.4*saturate(dot(normalize(i.normal),normalize(float3(-.4,.8,-.6))))+.18*saturate(dot(normalize(i.normal),normalize(float3(.6,.2,.6))));
                fixed4 c=tex2D(_MainTex,i.uv)*_Color; return fixed4(c.rgb*light,1);
            }
            ENDCG
        }
    }
}
