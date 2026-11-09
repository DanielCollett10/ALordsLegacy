// Unlit, alpha-blended shader for things drawn on the terrain (roads, area outlines, fills, drafts, markers).
// Decision 0020 (see its Amendment). Output = vertex colour x _Color x optional _MainTex.
//
// Depth bias (vertical clearance, converted to a push along the view ray):
//   dist = |camera - vertex|,  dir = unit vector vertex->camera
//   h    = _BiasConst + _BiasPerDist * dist          (vertical metres of clearance wanted)
//   s    = min(h / max(dir.y, _MinSin), dist * 0.5)  (distance to push the vertex towards the camera)
// Pushing a vertex s along the view ray lifts it s * sin(grazing angle) above the ground, so dividing
// by dir.y (the sine of the grazing angle) gives the clearance h at any camera pitch. _MinSin caps
// the push at very low pitch; dist * 0.5 keeps the vertex in front of the camera.
// Moving along the view ray leaves the screen position unchanged, so only the depth test is affected.
Shader "LordsLegacy/GroundOverlay"
{
    Properties
    {
        _MainTex ("Texture (optional)", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BiasConst ("Depth bias: vertical clearance, constant (m)", Float) = 0.15
        _BiasPerDist ("Depth bias: vertical clearance per metre of view distance", Float) = 0.004
        _MinSin ("Depth bias: minimum sine of grazing angle", Float) = 0.1
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
            float _MinSin;

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
                float h = _BiasConst + _BiasPerDist * dist;
                worldPos += dir * min(h / max(dir.y, _MinSin), dist * 0.5);
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
