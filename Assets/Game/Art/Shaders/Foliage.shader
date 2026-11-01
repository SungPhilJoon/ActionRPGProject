// 나무/풀 공용 URP 셰이더 (Documentation/VRising/URP.md 8절).
//  - 알파 클립 + 양면, 잎 반투과(역광), 바람 흔들림(정점), 개체별 색 변화
//  - 정점 색 규약 (WorldBuilder 절차 나무): R = 흔들림 가중치(줄기 0 → 가지 끝 1), G = 잎 떨림, B = AO(1 = 밝음)
//  - 정점 색이 없는 외부 풀 메시는 _UseHeightWeight = 1 로 오브젝트 높이를 가중치로 쓴다
//  - ForwardLit / ShadowCaster / DepthOnly / DepthNormals 모두 같은 바람 변형을 적용해 그림자·깊이가 어긋나지 않는다
//  - 카메라 가림 페이드: _OcclusionFade (1 = 불투명, MaterialPropertyBlock 으로 렌더러별 — Framework.FadeOcclusionResponse).
//    ForwardLit 은 알파 블렌드(1 이면 불투명과 같음), 페이드 중에는 깊이 패스(DepthOnly/DepthNormals)를 그리지 않아 SSAO 가 남지 않는다. 그림자는 그대로.
Shader "Game/Foliage"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.45
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.15
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1.0

        _Translucency("Translucency", Range(0.0, 2.0)) = 0.6
        _TranslucencyColor("Translucency Color", Color) = (0.75, 0.9, 0.35, 1)
        _HueVariation("Hue Variation (rgb, a = amount)", Color) = (0.9, 0.75, 0.35, 0.25)

        _WindDirection("Wind Direction (xz)", Vector) = (1, 0, 0.35, 0)
        _WindStrength("Wind Sway (m)", Float) = 0.25
        _WindSpeed("Wind Speed", Float) = 1.2
        _WindFlutter("Leaf Flutter (m)", Float) = 0.03
        _SwayHeight("Height Weight Range (m)", Float) = 1.0
        [Toggle] _UseHeightWeight("Use Height As Sway Weight", Float) = 0
        [Toggle] _UseVertexAO("Use Vertex AO (Color.B)", Float) = 1
        [Toggle] _FlipBackNormals("Flip Back Face Normals", Float) = 1

        [HideInInspector] _Cull("__cull", Float) = 0.0
        [HideInInspector] _OcclusionFade("Occlusion Fade", Range(0.0, 1.0)) = 1.0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "IgnoreProjector" = "True" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Cutoff;
            half _Smoothness;
            half _BumpScale;
            half _Translucency;
            half4 _TranslucencyColor;
            half4 _HueVariation;
            float4 _WindDirection;
            float _WindStrength;
            float _WindSpeed;
            float _WindFlutter;
            float _SwayHeight;
            half _UseHeightWeight;
            half _UseVertexAO;
            half _FlipBackNormals;
            half _Cull;
            half _OcclusionFade;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap);
        SAMPLER(sampler_BumpMap);

        // 개체(오브젝트 원점)마다 다른 0~1 값. 바람 위상과 색 변화에 쓴다.
        float FoliageObjectHash()
        {
            float3 origin = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
            return frac(sin(dot(origin.xz, float2(12.9898, 78.233))) * 43758.5453);
        }

        float3 FoliageWind(float3 InPositionOS, half4 InColor)
        {
            float3 positionWS = TransformObjectToWorld(InPositionOS);
            float heightWeight = saturate(InPositionOS.y / max(_SwayHeight, 0.01));
            float sway = lerp(InColor.r, heightWeight, _UseHeightWeight);
            sway *= sway;
            float flutter = lerp(InColor.g, heightWeight, _UseHeightWeight);

            float phase = FoliageObjectHash() * 6.2831;
            float t = _Time.y * _WindSpeed + phase;
            float2 direction = normalize(_WindDirection.xz + float2(0.0001, 0.0));
            float gust = sin(t) * 0.55 + sin(t * 2.37 + 1.3) * 0.25 + 0.45;
            float bend = gust * _WindStrength * sway;
            positionWS.xz += direction * bend;
            positionWS.y -= bend * bend * 0.15;

            float wave = sin(t * 6.1 + dot(positionWS, float3(1.7, 1.3, 2.1)));
            positionWS += float3(wave, wave * 0.6, -wave) * (_WindFlutter * flutter);
            return positionWS;
        }

        half FoliageAlpha(float2 InUV)
        {
            half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, InUV).a * _BaseColor.a;
            clip(alpha - _Cutoff);
            return alpha;
        }

        // 깊이 패스: 카메라 가림 페이드 중이면 그리지 않는다.
        half FoliageDepthAlpha(float2 InUV)
        {
            clip(_OcclusionFade - 0.99);
            return FoliageAlpha(InUV);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite On
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _NORMALMAP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
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
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                half4 color : TEXCOORD4;
                half fogFactor : TEXCOORD5;
                half3 tint : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = FoliageWind(input.positionOS.xyz, input.color);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = normals.normalWS;
                output.tangentWS = half4(normals.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.tint = lerp(half3(1.0, 1.0, 1.0), _HueVariation.rgb, FoliageObjectHash() * _HueVariation.a);
                return output;
            }

            half4 frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                clip(albedo.a - _Cutoff);

                half faceSign = IS_FRONT_VFACE(face, 1.0, -1.0);
                half3 normalWS = input.normalWS * lerp(1.0, faceSign, _FlipBackNormals);
            #if defined(_NORMALMAP)
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                half3 bitangent = input.tangentWS.w * cross(normalWS, input.tangentWS.xyz);
                normalWS = TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangent, normalWS));
            #endif
                normalWS = NormalizeNormalPerPixel(normalWS);

                half occlusion = lerp(1.0, input.color.b, _UseVertexAO);

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

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo.rgb * input.tint;
                surface.alpha = 1.0;
                surface.metallic = 0.0;
                surface.specular = half3(0.0, 0.0, 0.0);
                surface.smoothness = _Smoothness;
                surface.occlusion = occlusion;
                surface.normalTS = half3(0.0, 0.0, 1.0);

                half4 color = UniversalFragmentPBR(inputData, surface);

                // 잎 반투과: 해를 등진 잎이 역광으로 빛난다 (그림자 안에서는 꺼짐).
                Light mainLight = GetMainLight(inputData.shadowCoord, input.positionWS, inputData.shadowMask);
                half backLit = pow(saturate(dot(inputData.viewDirectionWS, -mainLight.direction)), 4.0) + saturate(dot(-normalWS, mainLight.direction)) * 0.35;
                color.rgb += surface.albedo * _TranslucencyColor.rgb * mainLight.color * (mainLight.shadowAttenuation * mainLight.distanceAttenuation * backLit * _Translucency * occlusion);

                color.rgb = MixFog(color.rgb, input.fogFactor);
                color.a = _OcclusionFade;
                return color;
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
            Cull Off

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
                half4 color : COLOR;
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
                float3 positionWS = FoliageWind(input.positionOS.xyz, input.color);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                output.positionCS = ApplyShadowClamping(positionCS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                FoliageAlpha(input.uv);
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
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformWorldToHClip(FoliageWind(input.positionOS.xyz, input.color));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                FoliageDepthAlpha(input.uv);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Off

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
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformWorldToHClip(FoliageWind(input.positionOS.xyz, input.color));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                FoliageDepthAlpha(input.uv);
                half3 normalWS = input.normalWS * lerp(1.0, IS_FRONT_VFACE(face, 1.0, -1.0), _FlipBackNormals);
                return half4(NormalizeNormalPerPixel(normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
