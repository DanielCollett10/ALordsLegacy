// Unlit, alpha-blended shader for things drawn on the terrain (roads, area outlines, fills, drafts, markers).
// Decision 0020. Output = vertex colour x _Color x optional _MainTex.
// Depth bias: each vertex is pulled towards the camera along its view ray by
// _BiasConst + _BiasPerDist * distance (world units). Moving along the view ray leaves the screen
// position unchanged, so only the depth test is affected; this keeps overlays above terrain that
// Unity's terrain LOD lifts or drops when zoomed out.
Shader "LordsLegacy/GroundOverlay"
{
    Properties
    {
        _MainTex ("Texture (optional)", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BiasConst ("Depth bias (constant, world units)", Float) = 0.05
        _BiasPerDist ("Depth bias per unit of view distance", Float) = 0.01
    }
    SubShader
    {
        Tags { "Queue"="Transparent-100" "RenderType"="Transparent" "IgnoreProjector"="True" "ForceNoShadowCasting"="True" }
        LOD 100
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _BiasConst;
            float _BiasPerDist;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 toCam = _WorldSpaceCameraPos - worldPos;
                float dist = length(toCam);
                float3 dir = toCam / max(dist, 1e-4);
                worldPos += dir * min(_BiasConst + _BiasPerDist * dist, dist * 0.5);
                o.pos = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.uv) * i.color;
            }
            ENDCG
        }
    }
    Fallback Off
}
