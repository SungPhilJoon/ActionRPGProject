// 대상 하이라이트 오버레이 (마우스 오버 · 선택). 대상 메시를 한 번 더 그리는 겹 렌더러(Framework.RendererHighlight)에 쓴다.
//  - Outline: 법선 방향으로 부풀린 뒷면(인버티드 헐) → 실루엣 바깥 테두리. 대상 앞면이 깊이를 써서 안쪽은 가려진다.
//  - Rim: 가장자리가 빛나는 프레넬 + 옅은 채움, 가산 합성 → 그늘에서도 대상이 눈에 띈다.
//  - 색 · 세기는 MaterialPropertyBlock 의 _HighlightColor (rgb, a = 세기) — 호버 / 선택마다 다른 색.
// 문서: Documentation/VRising/Targeting.md
Shader "Game/Highlight"
{
    Properties
    {
        _HighlightColor("Highlight Color (a = strength)", Color) = (1, 0.85, 0.4, 1)
        _OutlineWidth("Outline Width (m)", Float) = 0.035
        _RimPower("Rim Power", Range(0.5, 8.0)) = 2.5
        _RimIntensity("Rim Intensity", Float) = 1.6
        _Fill("Fill", Range(0.0, 1.0)) = 0.08
        _PulseSpeed("Pulse Speed", Float) = 3.0
        _PulseAmount("Pulse Amount", Range(0.0, 1.0)) = 0.25
        // 원본이 알파 컷아웃(나뭇잎)이면 RendererHighlight 가 원본 텍스처 · 문턱을 넘긴다 (잎 사각형이 보이지 않게).
        _BaseMap("Cutout Source", 2D) = "white" {}
        _Cutoff("Cutoff", Range(0.0, 1.0)) = 0.0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+10" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _HighlightColor;
            float _OutlineWidth;
            half _RimPower;
            half _RimIntensity;
            half _Fill;
            half _PulseSpeed;
            half _PulseAmount;
            float4 _BaseMap_ST;
            half _Cutoff;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        void HighlightClip(float2 InUV)
        {
            clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, InUV).a - _Cutoff);
        }

        half HighlightPulse()
        {
            return 1.0 - _PulseAmount + _PulseAmount * (0.5 + 0.5 * sin(_Time.y * _PulseSpeed));
        }
        ENDHLSL

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(positionWS + normalWS * _OutlineWidth);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                HighlightClip(input.uv);
                return half4(_HighlightColor.rgb, saturate(_HighlightColor.a) * HighlightPulse());
            }
            ENDHLSL
        }

        Pass
        {
            Name "Rim"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend One One

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                HighlightClip(input.uv);
                half3 normalWS = normalize(input.normalWS);
                half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half fresnel = pow(1.0 - saturate(dot(normalWS, viewWS)), _RimPower);
                half strength = (fresnel * _RimIntensity + _Fill) * _HighlightColor.a * HighlightPulse();
                return half4(_HighlightColor.rgb * strength, 0.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
