// 캐릭터 · 몬스터 공용 URP 셰이더 (URP.md 11절).
//  - PBR 모드: URP Lit 과 같은 맵 규칙(BaseMap, Normal, MetallicGloss[R=금속, A=매끄러움], Occlusion[G], Emission) + 모든 라이트(Forward+ 포함)
//  - 툰 모드(_Toon = 1, unity-chan): 메인 라이트를 2단계 밴드로 나누고 그늘 색을 따로 준다 + 추가 라이트(횃불)는 부드러운 밴드
//  - PBR 보조광(_FillLight): 반-램버트 필 — 해가 머리 위일 때 세로 면이 검게 뭉개지지 않게
//  - 공통: 림라이트(가장자리 빛, 쿼터뷰에서 캐릭터가 배경과 분리돼 보이게), 피격 플래시(_HitFlash, MaterialPropertyBlock),
//          외곽선(뒤집은 껍질, SRPDefaultUnlit 패스 — URP 포워드가 같은 오브젝트에 추가로 그린다)
//  - ShadowCaster / DepthOnly / DepthNormals (SSAO · 피사계 심도에 필요)
Shader "Game/Character"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
        _Cutoff("Alpha Cutoff (0 = 끔)", Range(0.0, 1.0)) = 0.0
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1.0
        _MetallicGlossMap("Metallic (R) Smoothness (A)", 2D) = "white" {}
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _OcclusionMap("Occlusion (G)", 2D) = "white" {}
        _OcclusionStrength("Occlusion Strength", Range(0.0, 1.0)) = 1.0
        _EmissionMap("Emission", 2D) = "black" {}
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 1)

        _FillLight("Character Fill Light (PBR)", Range(0.0, 1.0)) = 0.35

        [Header(Rim)]
        [HDR] _RimColor("Rim Color", Color) = (0.9, 0.75, 0.7, 1)
        _RimPower("Rim Power", Range(0.5, 8.0)) = 3.5
        _RimStrength("Rim Strength", Range(0.0, 2.0)) = 0.55

        [Header(Toon)]
        [Toggle] _Toon("Toon Shading", Float) = 0
        _ToonThreshold("Toon Threshold", Range(-1.0, 1.0)) = 0.1
        _ToonSoftness("Toon Softness", Range(0.001, 0.5)) = 0.06
        _ToonShadowColor("Toon Shadow Tint", Color) = (0.62, 0.52, 0.62, 1)

        [Header(Outline)]
        _OutlineWidth("Outline Width (m)", Range(0.0, 0.05)) = 0.006
        _OutlineColor("Outline Color", Color) = (0.06, 0.03, 0.04, 1)

        [Header(Hit)]
        [HDR] _HitFlash("Hit Flash (rgb, a = 세기)", Color) = (1, 1, 1, 0)

        [HideInInspector] _Cull("__cull", Float) = 2.0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Cutoff;
            half _BumpScale;
            half _Metallic;
            half _Smoothness;
            half _OcclusionStrength;
            half4 _EmissionColor;
            half _FillLight;
            half4 _RimColor;
            half _RimPower;
            half _RimStrength;
            half _Toon;
            half _ToonThreshold;
            half _ToonSoftness;
            half4 _ToonShadowColor;
            float _OutlineWidth;
            half4 _OutlineColor;
            half4 _HitFlash;
            half _Cull;
        CBUFFER_END

        TEXTURE2D(_BaseMap);            SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap);            SAMPLER(sampler_BumpMap);
        TEXTURE2D(_MetallicGlossMap);   SAMPLER(sampler_MetallicGlossMap);
        TEXTURE2D(_OcclusionMap);       SAMPLER(sampler_OcclusionMap);
        TEXTURE2D(_EmissionMap);        SAMPLER(sampler_EmissionMap);

        half CharacterAlpha(float2 InUV)
        {
            half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, InUV).a * _BaseColor.a;
            if (_Cutoff > 0.0)
            {
                clip(alpha - _Cutoff);
            }

            return alpha;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = normals.normalWS;
                output.tangentWS = half4(normals.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            // 툰 밴드: N·L 을 _ToonThreshold 기준으로 부드러운 계단으로 자른다.
            half toonBand(half InNdotL)
            {
                return smoothstep(_ToonThreshold - _ToonSoftness, _ToonThreshold + _ToonSoftness, InNdotL);
            }

            half3 toonLighting(InputData InData, half3 InAlbedo, half InOcclusion)
            {
                half4 shadowMask = half4(1.0, 1.0, 1.0, 1.0);
                Light mainLight = GetMainLight(InData.shadowCoord, InData.positionWS, shadowMask);
                half lit = toonBand(dot(InData.normalWS, mainLight.direction)) * mainLight.shadowAttenuation;
                half3 shade = lerp(_ToonShadowColor.rgb, half3(1.0, 1.0, 1.0), lit);
                half3 color = InAlbedo * shade * mainLight.color;
                color += InAlbedo * InData.bakedGI * InOcclusion;

            #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();
                InputData inputData = InData;
            #if USE_CLUSTER_LIGHT_LOOP
                [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                    Light light = GetAdditionalLight(lightIndex, InData.positionWS, shadowMask);
                    half band = toonBand(dot(InData.normalWS, light.direction));
                    color += InAlbedo * light.color * (band * light.distanceAttenuation * light.shadowAttenuation);
                }
            #endif
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, InData.positionWS, shadowMask);
                    half band = toonBand(dot(InData.normalWS, light.direction));
                    color += InAlbedo * light.color * (band * light.distanceAttenuation * light.shadowAttenuation);
                LIGHT_LOOP_END
            #endif
                return color;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                if (_Cutoff > 0.0)
                {
                    clip(albedo.a - _Cutoff);
                }

                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                half3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                half3 normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangent, input.normalWS)));
                half4 metallicGloss = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, input.uv);
                half occlusion = LerpWhiteTo(SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, input.uv).g, _OcclusionStrength);
                half3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1.0, 1.0, 1.0, 1.0);

                half4 color;
                if (_Toon > 0.5)
                {
                    color = half4(toonLighting(inputData, albedo.rgb, occlusion) + emission, 1.0);
                }
                else
                {
                    SurfaceData surface = (SurfaceData)0;
                    surface.albedo = albedo.rgb;
                    surface.alpha = 1.0;
                    surface.metallic = metallicGloss.r * _Metallic;
                    surface.smoothness = metallicGloss.a * _Smoothness;
                    surface.occlusion = occlusion;
                    surface.emission = emission;
                    surface.normalTS = normalTS;
                    color = UniversalFragmentPBR(inputData, surface);

                    // 캐릭터 보조광: 쿼터뷰에서 해가 위에 있으면 세로 면(다리 · 몸통)이 거의 빛을 못 받아 어두운 원본 텍스처가 검게 뭉개진다.
                    // 반-램버트(N·L*0.5+0.5) 필 + 앰비언트를 조금 더해 형태가 읽히게 한다 (액션 게임의 캐릭터 전용 라이트와 같은 역할).
                    Light fillLight = GetMainLight(inputData.shadowCoord, input.positionWS, inputData.shadowMask);
                    half halfLambert = dot(normalWS, fillLight.direction) * 0.5 + 0.5;
                    half3 fill = fillLight.color * (halfLambert * halfLambert * lerp(0.5, 1.0, fillLight.shadowAttenuation)) + inputData.bakedGI * 2.0;
                    color.rgb += surface.albedo * (1.0 - surface.metallic * 0.7) * fill * (_FillLight * occlusion);
                }

                // 림: 시선과 거의 수직인 가장자리를 밝게 (그늘 · 밤에도 실루엣이 보이게). 메인 라이트 색과 앰비언트로 물든다.
                half fresnel = pow(1.0 - saturate(dot(normalWS, inputData.viewDirectionWS)), _RimPower);
                half3 lightTint = GetMainLight().color * 0.6 + inputData.bakedGI * 1.4;
                color.rgb += _RimColor.rgb * lightTint * (fresnel * _RimStrength);

                // 피격 플래시 (게임이 MaterialPropertyBlock 으로 a 를 0→1→0).
                color.rgb = lerp(color.rgb, _HitFlash.rgb, saturate(_HitFlash.a));
                color.rgb = MixFog(color.rgb, input.fogFactor);
                color.a = 1.0;
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                // 멀리 있어도 선 두께가 화면에서 크게 줄지 않게 거리에 비례해 키운다 (상한 3배).
                float distanceScale = clamp(distance(positionWS, _WorldSpaceCameraPos.xyz) / 20.0, 1.0, 3.0);
                positionWS += normalWS * (_OutlineWidth * distanceScale);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                if (_OutlineWidth <= 0.0)
                {
                    discard;
                }

                CharacterAlpha(input.uv);
                half3 color = lerp(_OutlineColor.rgb, _HitFlash.rgb, saturate(_HitFlash.a));
                return half4(MixFog(color, input.fogFactor), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

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
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                CharacterAlpha(input.uv);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

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
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                CharacterAlpha(input.uv);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                CharacterAlpha(input.uv);
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
