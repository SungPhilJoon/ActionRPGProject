// 대기 원근 풀스크린 패스 (URP Full Screen Pass Renderer Feature, URP.md 10절).
//  - 높이 안개: 낮은 곳(호수 분지 · 강 · 계곡)에 고이는 지수 높이 안개 — 광선 따라 해석적으로 적분
//  - 태양 산란: 시선이 해 쪽을 향할수록 안개 색이 햇빛 색으로 물든다 (역광 아지랑이)
//  - 하늘(깊이 = 먼 평면) 픽셀은 건드리지 않는다
// 파라미터는 ScreenEffects 가 매 프레임 전역 값으로 넣는다 (_AtmoSunDir, _AtmoSunColor, _AtmoFogColor, _AtmoParams).
Shader "Game/PostFx/Atmosphere"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "Atmosphere"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // xyz = 지면 → 태양 방향, w = 산란 세기
            float4 _AtmoSunDir;
            float4 _AtmoSunColor;
            float4 _AtmoFogColor;
            // x = 높이 안개 밀도, y = 높이 감쇠(1/m), z = 안개 기준 높이(m), w = 최대 안개 양(0~1)
            float4 _AtmoParams;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float rawDepth = SampleSceneDepth(uv);
            #if UNITY_REVERSED_Z
                if (rawDepth <= 0.000001)
            #else
                if (rawDepth >= 0.999999)
            #endif
                {
                    return color;
                }

                float3 positionWS = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                float3 toPixel = positionWS - _WorldSpaceCameraPos.xyz;
                float distance = length(toPixel);
                float3 direction = toPixel / max(distance, 0.0001);

                // 밀도(h) = d · exp(-k (h - base)) 를 카메라 → 픽셀 직선을 따라 적분.
                float density = _AtmoParams.x;
                float falloff = max(_AtmoParams.y, 0.0001);
                float h0 = _WorldSpaceCameraPos.y - _AtmoParams.z;
                float h1 = positionWS.y - _AtmoParams.z;
                float dh = h1 - h0;
                float integral = abs(dh) > 0.01
                    ? (exp(-falloff * h0) - exp(-falloff * h1)) / (falloff * dh)
                    : exp(-falloff * h0);
                float fog = 1.0 - exp(-density * distance * max(integral, 0.0));
                fog = min(fog, _AtmoParams.w);

                float sun = pow(saturate(dot(direction, normalize(_AtmoSunDir.xyz))), 6.0) * _AtmoSunDir.w;
                half3 fogColor = lerp(_AtmoFogColor.rgb, _AtmoSunColor.rgb, saturate(sun));
                color.rgb = lerp(color.rgb, fogColor, fog);
                return color;
            }
            ENDHLSL
        }
    }
}
