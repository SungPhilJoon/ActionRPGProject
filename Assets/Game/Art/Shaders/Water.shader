// 강/호수 URP 물 셰이더 (Documentation/VRising/URP.md 9절).
//  - 깊이 텍스처로 물 깊이 → 얕은 색/깊은 색 흡수, 물가 거품, 가장자리 페이드
//  - 불투명 텍스처(_CameraOpaqueTexture)를 노멀로 왜곡해 굴절
//  - 흐름: 정점 색 RG = 흐름 방향(xz, 0~1 인코딩), B = 흐름 속도(0~1), A = 급류 거품(0~1). 호수는 B = 0 → 바람 물결만
//  - 2위상 플로우맵(위상 0.5 차이 두 샘플 교차)으로 텍스처가 늘어나지 않게 흐른다
// 필요 설정: URP 에셋 Depth Texture / Opaque Texture 켜짐 (WorldBuilder.configurePipeline 이 켠다).
Shader "Game/Water"
{
    Properties
    {
        _ShallowColor("Shallow Tint", Color) = (0.55, 0.78, 0.72, 1)
        _DeepColor("Deep Color", Color) = (0.03, 0.12, 0.14, 1)
        _DepthDistance("Depth To Deep (m)", Float) = 2.5
        _EdgeFade("Edge Fade (m)", Float) = 0.25

        [Normal] _NormalMap("Normal Map", 2D) = "bump" {}
        _NormalTiling("Normal Tiling (1/m)", Float) = 0.18
        _NormalStrength("Normal Strength", Range(0.0, 2.0)) = 0.7
        _RippleSpeed("Wind Ripple Speed", Float) = 0.035
        _FlowSpeed("Flow Speed (m/s at B = 1)", Float) = 1.6
        _FlowCycle("Flow Cycle (s)", Float) = 1.6

        _Refraction("Refraction", Range(0.0, 0.1)) = 0.025
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.92
        _ReflectionColor("Sky Reflection Fallback", Color) = (0.55, 0.68, 0.78, 1)
        _EnvironmentReflection("Environment Reflection Weight", Range(0.0, 1.0)) = 0.5
        _FresnelPower("Fresnel Power", Float) = 4.0

        _FoamMap("Foam Noise", 2D) = "white" {}
        _FoamColor("Foam Color", Color) = (0.9, 0.95, 0.95, 1)
        _FoamDistance("Shore Foam (m)", Float) = 0.45
        _FoamTiling("Foam Tiling (1/m)", Float) = 0.35
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent-10" "IgnoreProjector" = "True" }

        Pass
        {
            Name "WaterForward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float _DepthDistance;
                float _EdgeFade;
                float4 _NormalMap_ST;
                float _NormalTiling;
                half _NormalStrength;
                float _RippleSpeed;
                float _FlowSpeed;
                float _FlowCycle;
                half _Refraction;
                half _Smoothness;
                half4 _ReflectionColor;
                half _EnvironmentReflection;
                half _FresnelPower;
                float4 _FoamMap_ST;
                half4 _FoamColor;
                float _FoamDistance;
                float _FoamTiling;
            CBUFFER_END

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            TEXTURE2D(_FoamMap);
            SAMPLER(sampler_FoamMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half4 flow : TEXCOORD1;
                float viewDepth : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.viewDepth = -TransformWorldToView(output.positionWS).z;
                output.flow = input.color;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            // 흐름 방향으로 미는 2위상 샘플. InOffset = 흐름 벡터(m/s). 반환: 탄젠트 공간 노멀(xy) 합.
            half3 sampleFlowNormal(float2 InUV, float2 InFlow, float InTime)
            {
                float cycle = max(_FlowCycle, 0.1);
                float phase0 = frac(InTime / cycle);
                float phase1 = frac(InTime / cycle + 0.5);
                float weight = abs(1.0 - 2.0 * phase0);
                half3 n0 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, InUV - InFlow * (phase0 * cycle)));
                half3 n1 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, InUV - InFlow * (phase1 * cycle) + 0.37));
                return lerp(n1, n0, weight);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float time = _Time.y;
                float2 worldUV = input.positionWS.xz * _NormalTiling;
                float2 flowDirection = input.flow.rg * 2.0 - 1.0;
                float flowSpeed = input.flow.b;
                float2 flow = flowDirection * (flowSpeed * _FlowSpeed * _NormalTiling);

                // 바람 물결 (두 방향) + 흐름
                half3 ripple0 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, worldUV + float2(time, time * 0.6) * _RippleSpeed));
                half3 ripple1 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, worldUV * 1.9 + float2(-time * 0.7, time) * _RippleSpeed));
                half3 flowNormal = sampleFlowNormal(worldUV * 1.3, flow, time);
                half2 slope = (ripple0.xy + ripple1.xy) * (0.5 * (1.0 - flowSpeed)) + flowNormal.xy * flowSpeed;
                half3 normalWS = normalize(half3(slope.x * _NormalStrength, 1.0, slope.y * _NormalStrength));

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float depth = max(0.0, sceneDepth - input.viewDepth);

                // 굴절: 물 앞쪽 물체가 번져 들어오지 않게 왜곡된 지점이 수면보다 가까우면 원래 UV 를 쓴다.
                float2 refractUV = screenUV + normalWS.xz * (_Refraction * saturate(depth));
                float refractDepth = LinearEyeDepth(SampleSceneDepth(refractUV), _ZBufferParams) - input.viewDepth;
                if (refractDepth < 0.0)
                {
                    refractUV = screenUV;
                    refractDepth = depth;
                }

                half3 sceneColor = SampleSceneColor(refractUV);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half3 ambient = SampleSH(half3(0.0, 1.0, 0.0));
                half3 lightColor = mainLight.color * (saturate(mainLight.direction.y) * 0.7 * mainLight.shadowAttenuation + 0.15) + ambient;

                half absorb = saturate(refractDepth / max(_DepthDistance, 0.01));
                half3 deep = _DeepColor.rgb * lightColor;
                half3 water = lerp(sceneColor * _ShallowColor.rgb, deep, absorb * absorb * (3.0 - 2.0 * absorb));

                // 반사 (프레넬) + 해 하이라이트
                half3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half fresnel = pow(1.0 - saturate(dot(normalWS, viewDir)), _FresnelPower);
                half perceptualRoughness = 1.0 - _Smoothness;
                half3 environment = GlossyEnvironmentReflection(reflect(-viewDir, normalWS), perceptualRoughness, 1.0);
                half3 reflection = lerp(_ReflectionColor.rgb * lightColor, environment, _EnvironmentReflection);
                water = lerp(water, reflection, saturate(fresnel * 0.85 + 0.04));

                half3 halfDir = normalize(mainLight.direction + viewDir);
                half specular = pow(saturate(dot(normalWS, halfDir)), 220.0 * _Smoothness + 8.0) * 3.0;
                water += mainLight.color * (specular * mainLight.shadowAttenuation);

                // 거품: 물가(얕은 깊이) + 급류(정점 A). 흐름 방향으로 같이 흘린다.
                float2 foamUV = input.positionWS.xz * _FoamTiling - flowDirection * (flowSpeed * _FlowSpeed * _FoamTiling * time);
                half foamNoise = SAMPLE_TEXTURE2D(_FoamMap, sampler_FoamMap, foamUV).r;
                half shore = 1.0 - saturate(depth / max(_FoamDistance, 0.01));
                half foam = saturate(shore * 1.4 + input.flow.a * 0.9) * foamNoise;
                foam = smoothstep(0.35, 0.75, foam);
                water = lerp(water, _FoamColor.rgb * lightColor, foam * 0.85);

                water = MixFog(water, input.fogFactor);
                half alpha = saturate(depth / max(_EdgeFade, 0.01));
                return half4(water, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
