Shader "ContextSpeak/Forest Water"
{
    Properties
    {
        // Atmosphere.Apply changes this property throughout the day.
        _BaseColor ("Water tint (day and weather)", Color) = (0.28, 0.45, 0.42, 1)
        _ShallowTint ("Shallow tint multiplier", Color) = (0.82, 1.0, 0.76, 1)
        _DeepTint ("Deep tint multiplier", Color) = (0.30, 0.62, 0.66, 1)
        _Opacity ("Overall opacity", Range(0, 1)) = 0.92
        _ShallowOpacity ("Shallow opacity", Range(0, 1)) = 0.42
        _DeepOpacity ("Deep opacity", Range(0, 1)) = 0.90

        _RippleScale ("Ripples per metre", Range(0.15, 3)) = 0.72
        _RippleStrength ("Ripple normal strength", Range(0, 0.25)) = 0.055
        _RippleSpeed ("Ripple speed", Range(0, 2)) = 0.24
        _Smoothness ("Reflection smoothness", Range(0, 1)) = 0.86
        _ReflectionStrength ("Environment reflection", Range(0, 1)) = 0.82
        _SunGlint ("Sun glint", Range(0, 2)) = 0.55

        [Toggle(_WATER_DEPTH_FADE)] _UseDepthFade ("Use camera depth texture", Float) = 1
        _DepthDistance ("Deep colour distance (metres)", Range(0.1, 8)) = 2.0
        _ShoreFadeDistance ("Shore fade distance (metres)", Range(0.01, 1)) = 0.22
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForestWaterForward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local_fragment _WATER_DEPTH_FADE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            // Transparent receivers use the main light's shadow map, including
            // when URP enables screen-space shadows for opaque objects.
            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #if defined(_WATER_DEPTH_FADE)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #endif

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _ShallowTint;
                half4 _DeepTint;
                half _Opacity;
                half _ShallowOpacity;
                half _DeepOpacity;
                float _RippleScale;
                half _RippleStrength;
                float _RippleSpeed;
                half _Smoothness;
                half _ReflectionStrength;
                half _SunGlint;
                float _UseDepthFade;
                float _DepthDistance;
                float _ShoreFadeDistance;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // Only the shading normal moves. The waterline, fishing target,
                // colliders and float remain on the original pond plane.
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half3 RippleNormal(float3 positionWS, half3 geometricNormal)
            {
                // World-space waves keep their size when the pond is scaled.
                // These four gentle directions form normal detail without a
                // normal-map texture or geometry displacement. Intended for a
                // horizontal pond surface with upward-facing mesh normals.
                float2 p = positionWS.xz * _RippleScale;
                float time = _Time.y * _RippleSpeed;
                const float2 directionA = float2(0.8192, 0.5736);
                const float2 directionB = float2(-0.4226, 0.9063);
                const float2 directionC = float2(0.9659, -0.2588);
                const float2 directionD = float2(0.1736, 0.9848);

                float2 slope = directionA * cos(dot(p, directionA) * 6.283185 + time) * 0.50;
                slope += directionB * cos(dot(p, directionB) * 9.676105 - time * 0.73) * 0.28;
                slope += directionC * cos(dot(p, directionC) * 16.52478 + time * 1.21) * 0.14;
                slope += directionD * cos(dot(p, directionD) * 2.261947 - time * 0.39) * 0.22;

                // Remove sub-pixel ripple normals at distance to restrain
                // sparkling on low-resolution browser render targets.
                float footprint = max(length(ddx(p)), length(ddy(p)));
                half detailFade = 1.0h - smoothstep(0.18, 0.75, footprint);
                half2 perturbation = slope * (_RippleStrength * detailFade);
                return normalize(geometricNormal + half3(-perturbation.x, 0, -perturbation.y));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                half3 geometricNormal = normalize(input.normalWS);
                half3 normalWS = RippleNormal(input.positionWS, geometricNormal);
                half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half viewFacing = saturate(dot(normalWS, viewWS));
                // Keep the Fresnel power explicit: Pow5 is not provided by URP17 on GLES3.
                half grazing = 1.0h - viewFacing;
                half grazingSquared = grazing * grazing;
                half fresnel = 0.02h + 0.98h * grazingSquared * grazingSquared * grazing;

                // This fallback keeps a readable pond when depth is explicitly
                // disabled. Depth sampling only sees opaque scene geometry.
                half depthBlend = 0.78h;
                half shoreFade = 1.0h;
                #if defined(_WATER_DEPTH_FADE)
                    float rawDepth = SampleSceneDepth(screenUV);
                    float sceneEyeDepth = unity_OrthoParams.w > 0.5
                        ? LinearDepthToEyeDepth(rawDepth)
                        : LinearEyeDepth(rawDepth, _ZBufferParams);
                    float waterEyeDepth = -TransformWorldToView(input.positionWS).z;

                    // Convert eye-space separation to approximate thickness
                    // along the plane normal. Perspective eye depth is measured
                    // along camera forward, not along the normalized view ray.
                    float rayDepthScale = unity_OrthoParams.w > 0.5
                        ? 1.0
                        : rcp(max(abs(TransformWorldToViewDir(-viewWS).z), 0.05));
                    float thickness = max(sceneEyeDepth - waterEyeDepth, 0.0)
                        * rayDepthScale * max(abs(dot(viewWS, geometricNormal)), 0.04);
                    depthBlend = saturate(1.0 - exp2(-thickness / max(_DepthDistance, 0.01)));
                    shoreFade = saturate(thickness / max(_ShoreFadeDistance, 0.001));
                #endif

                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half sunFacing = saturate(dot(normalWS, sun.direction));
                half sunVisibility = sun.distanceAttenuation * sun.shadowAttenuation;
                half3 ambient = max(SampleSH(normalWS), half3(0.015h, 0.02h, 0.025h));
                half3 waterTint = _BaseColor.rgb * lerp(_ShallowTint.rgb, _DeepTint.rgb, depthBlend);
                half3 waterLight = ambient + sun.color * (sunFacing * sunVisibility * 0.5h);
                half3 scatter = waterTint * waterLight;

                // URP samples the assigned reflection probe, or its sky fallback.
                // One probe lookup; no planar reflection or screen-space pass.
                half3 reflectionWS = reflect(-viewWS, normalWS);
                half3 environment = GlossyEnvironmentReflection(
                    reflectionWS, input.positionWS, 1.0h - _Smoothness, 1.0h, screenUV);
                half reflectionWeight = saturate((0.10h + fresnel * 0.90h) * _ReflectionStrength);
                half3 color = lerp(scatter, environment, reflectionWeight);

                half3 halfDirection = SafeNormalize(sun.direction + viewWS);
                half specularPower = lerp(48.0h, 384.0h, _Smoothness);
                half sunHighlight = pow(saturate(dot(normalWS, halfDirection)), specularPower);
                color += sun.color * (sunHighlight * sunFacing * sunVisibility * _SunGlint);

                half alpha = saturate(lerp(_ShallowOpacity, _DeepOpacity, depthBlend)
                    * _Opacity + fresnel * 0.18h);
                alpha *= shoreFade * saturate(_BaseColor.a);
                return half4(MixFog(color, input.fogFactor), alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
