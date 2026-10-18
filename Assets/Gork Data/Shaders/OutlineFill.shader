// Draws a screen-space expanded hull of the selected object, only outside its own
// silhouette (stencil from Gork/OutlineMask). Vertices are pushed away from _OutlineCenter
// in screen space, which keeps box-like buildings free of corner gaps.
Shader "Gork/OutlineFill"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 0.85, 0.3, 0.9)
        _OutlineWidth ("Outline Width (px)", Float) = 3
        _OutlineCenter ("Outline Center (world)", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+100" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "OutlineFill"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            Stencil
            {
                Ref 64
                ReadMask 64
                Comp NotEqual
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
                float4 _OutlineCenter;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float4 posCS = TransformObjectToHClip(v.positionOS.xyz);
                float4 centerCS = TransformWorldToHClip(_OutlineCenter.xyz);

                float2 aspect = float2(_ScreenParams.x / _ScreenParams.y, 1.0);
                float2 dir = (posCS.xy / posCS.w - centerCS.xy / centerCS.w) * aspect;
                dir = normalize(dir + 1e-5) / aspect;

                // Pixels -> NDC (NDC spans 2 units across the screen).
                float2 offset = dir * _OutlineWidth * 2.0 / _ScreenParams.y;
                posCS.xy += offset * posCS.w;
                o.positionCS = posCS;
                return o;
            }

            half4 frag(Varyings i) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }
    }
}
