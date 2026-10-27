Shader "BotsBolts/Enamel and Copper"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Strength", Range(0,1)) = 0.15
        _MetallicGlossMap("Metallic (R) Smoothness (A)", 2D) = "white" {}
        _Metallic("Metallic", Range(0,1)) = 0
        _Smoothness("Smoothness", Range(0,1)) = 0.4
        _EmissionMap("Emission Map", 2D) = "white" {}
        [HDR] _EmissionColor("Emission", Color) = (0,0,0,0)
        _ShadowTint("Cool Shadow Tint", Color) = (0.72,0.79,0.9,1)
        _AmbientStrength("Ambient Fill", Range(0,1)) = 0.4
        _SpecularStrength("Broad Highlight Strength", Range(0,2)) = 0.5
        _CoatStrength("Enamel Clear Coat", Range(0,1)) = 0.55
        _ReflectionStrength("Soft Reflections", Range(0,1)) = 0.25
        _RimStrength("Soft Edge Light", Range(0,0.5)) = 0.06
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clip", Float) = 0
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor, _EmissionColor, _ShadowTint;
            half _BumpScale, _Metallic, _Smoothness;
            half _AmbientStrength, _SpecularStrength, _CoatStrength, _ReflectionStrength, _RimStrength;

            half _AlphaClip, _Cutoff, _Cull;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);
        TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
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
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            half4 tangentWS : TEXCOORD2;
            float2 uv : TEXCOORD3;
            half fog : TEXCOORD4;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings EnamelVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
            VertexNormalInputs n = GetVertexNormalInputs(input.normalOS, input.tangentOS);
            output.positionCS = p.positionCS;
            output.positionWS = p.positionWS;
            output.normalWS = n.normalWS;
            output.tangentWS = half4(n.tangentWS, input.tangentOS.w * GetOddNegativeScale());
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            output.fog = ComputeFogFactor(p.positionCS.z);
            return output;
        }
        half4 BaseSample(Varyings input)
        {
            half4 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
            #if defined(_ALPHATEST_ON)
                clip(base.a - _Cutoff);
            #endif
            return base;
        }
        half3 EnamelNormal(Varyings input)
        {
            half3 normal = normalize(input.normalWS);
            #if defined(_NORMALMAP)
                half3 tangent = normalize(input.tangentWS.xyz);
                half3 bitangent = cross(normal, tangent) * input.tangentWS.w;
                half3 sampled = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                normal = normalize(TransformTangentToWorld(sampled, half3x3(tangent, bitangent, normal)));
            #endif
            return normal;
        }
        half3 EnamelLight(Light light, half3 normal, half3 glossNormal, half3 view,
            half3 base, half metallic, half smoothness, half normalVariance, bool mainLight)
        {
            half ndl = saturate(dot(normal, light.direction));
            half attenuation = saturate(light.distanceAttenuation) * light.shadowAttenuation;
            // Continuous shaped lighting, without hard bands or ink shadows.
            half diffuse = smoothstep(-0.08h, 0.95h, dot(normal, light.direction));
            half3 shading = mainLight
                ? lerp(_ShadowTint.rgb * 0.62h, half3(1,1,1), diffuse * attenuation)
                : (diffuse * attenuation).xxx;
            half3 halfDirection = SafeNormalize(light.direction + view);
            half ndh = saturate(dot(glossNormal, halfDirection));
            half exponent = lerp(5.0h, 20.0h, smoothness);
            exponent /= 1.0h + normalVariance * 120.0h;
            half broadSpec = pow(ndh, exponent);
            half3 specTint = lerp(half3(0.8h,0.86h,0.94h), base, metallic);
            half3 specular = specTint * broadSpec * _SpecularStrength;
            half coat = pow(ndh, 18.0h) * _CoatStrength * 0.5h * (1.0h - metallic);
            return (base * shading * lerp(1.0h, 0.72h, metallic)
                + (specular + coat) * attenuation * smoothstep(0.0h, 0.2h, ndl)) * light.color;
        }
        half4 EnamelFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half4 base = BaseSample(input);
            half3 normal = EnamelNormal(input);
            half3 highlightNormal = normalize(input.normalWS);
            // Evaluate derivatives before any divergent per-pixel light loops.
            half normalVariance = max(dot(ddx(highlightNormal), ddx(highlightNormal)), dot(ddy(highlightNormal), ddy(highlightNormal)));
            half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
            half metallic = _Metallic, smoothness = _Smoothness;
            #if defined(_METALLICSPECGLOSSMAP)
                half4 packed = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, input.uv);
                metallic *= packed.r; smoothness *= packed.a;
            #endif
            float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
            #endif
            Light main = GetMainLight(shadowCoord);
            half3 color = EnamelLight(main, normal, highlightNormal, view, base.rgb, metallic, smoothness, normalVariance, true);
            color += base.rgb * max(SampleSH(normal), 0) * _AmbientStrength;
            #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                #if USE_CLUSTER_LIGHT_LOOP
                    UNITY_LOOP for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); ++lightIndex)
                    {
                        Light light = GetAdditionalLight(lightIndex, input.positionWS, half4(1,1,1,1));
                        color += EnamelLight(light, normal, highlightNormal, view, base.rgb, metallic, smoothness, normalVariance, false);
                    }
                #endif
                uint count = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(count)
                    Light light = GetAdditionalLight(lightIndex, input.positionWS, half4(1,1,1,1));
                    color += EnamelLight(light, normal, highlightNormal, view, base.rgb, metallic, smoothness, normalVariance, false);
                LIGHT_LOOP_END
            #endif
            half3 reflected = GlossyEnvironmentReflection(reflect(-view, highlightNormal), input.positionWS,
                max(0.28h, 1.0h - smoothness), 1.0h, GetNormalizedScreenSpaceUV(input.positionCS));
            half facing = saturate(dot(highlightNormal, view));
            half3 fresnel = lerp(half3(0.04h,0.04h,0.04h), base.rgb, metallic);
            fresnel += (1.0h - fresnel) * pow(1.0h - facing, 5.0h);
            color += reflected * fresnel * _ReflectionStrength;
            color += base.rgb * pow(1.0h - facing, 3.0h) * _RimStrength;
            #if defined(_SCREEN_SPACE_OCCLUSION)
                AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(input.positionCS));
                color *= ao.indirectAmbientOcclusion;
            #endif
            color += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb;
            return half4(MixFog(color, input.fog), base.a);
        }
        half4 DepthFragment(Varyings input) : SV_Target { UNITY_SETUP_INSTANCE_ID(input); BaseSample(input); return 0; }
        half4 NormalFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            BaseSample(input);
            half3 normal = EnamelNormal(input);
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 octNormal = PackNormalOctQuadEncode(normal);
                return half4(PackFloat2To888(saturate(octNormal * 0.5 + 0.5)), 0);
            #else
                return half4(normal, 0);
            #endif
        }
        float3 _LightDirection, _LightPosition;
        Varyings ShadowVertex(Attributes input)
        {
            Varyings output = EnamelVertex(input);
            float3 direction = _LightDirection;
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                direction = normalize(_LightPosition - output.positionWS);
            #endif
            output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(output.positionWS, output.normalWS, direction)));
            return output;
        }
        ENDHLSL
        Pass
        {
            Name "EnamelForward" Tags { "LightMode"="UniversalForwardOnly" }
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex EnamelVertex
            #pragma fragment EnamelFragment
            #pragma multi_compile_instancing
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _METALLICSPECGLOSSMAP
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster" Tags { "LightMode"="ShadowCaster" }
            Cull [_Cull] ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags { "LightMode"="DepthOnly" }
            Cull [_Cull] ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex EnamelVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals" Tags { "LightMode"="DepthNormalsOnly" }
            Cull [_Cull] ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex EnamelVertex
            #pragma fragment NormalFragment
            #pragma multi_compile_instancing
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
    }
    Fallback Off
}
