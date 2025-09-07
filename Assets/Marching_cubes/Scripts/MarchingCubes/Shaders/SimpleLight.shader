Shader "Custom/SimpleLight"
{
    Properties
    {
        _Color("Color", Color) = (1, 0.5, 0, 1)
        _DirLight("Directional light dir", Vector) = (0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
            };

            float4 _Color;
            float4 _DirLight;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 normal = normalize(i.normal);
                float3 lightDir = normalize(_DirLight.xyz);

                float angle = max(0.1f, dot(normal, lightDir));

                return _Color * angle;
            }
            ENDCG
        }
    }
}