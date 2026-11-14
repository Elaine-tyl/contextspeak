// ContextSpeak ground surfaces (roads, pavements, lawns, paths, sand) for meshes without good UVs.
// World-space projection (top-down, triplanar on sides) so textures never stretch across a 50 m
// road; anti-tiling macro variation; PBR with mask map (G = AO, B = height, A = smoothness);
// rain wetness and puddles that collect in low areas of the height map and reflect the sky.
Shader "ContextSpeak/Ground Lit"
{
    Properties
    {
        _BaseMap ("Base colour", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        [Normal] _BumpMap ("Normal map", 2D) = "bump" {}
        _BumpScale ("Normal strength", Range(0, 2)) = 1
        _MaskMap ("Mask (G AO, B height, A smoothness)", 2D) = "white" {}
        _Tile ("Metres per texture tile", Float) = 2
        _MacroScale ("Macro variation size (m)", Float) = 23
        _MacroStrength ("Macro variation", Range(0, 0.5)) = 0.18
        _Puddles ("Puddles when wet", Range(0, 1)) = 0.5
        _Porosity ("Darkening when wet", Range(0, 1)) = 0.6
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            float _BumpScale, _Tile, _MacroScale, _MacroStrength, _Puddles, _Porosity;
        CBUFFER_END
        float _EnvWetness, _EnvRain, _WindTime;

        float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
        float ValueNoise(float2 p)
        {
            float2 i = floor(p), f = frac(p), u = f * f * (3 - 2 * f);
            return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + 1), u.x), u.y);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float fog : TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 n = normalize(input.normalWS);
                float3 w = pow(abs(n), 4); w /= (w.x + w.y + w.z);
                float3 p = input.positionWS / max(_Tile, 0.01);
                                float2 uvY = p.xz, uvX = p.zy, uvZ = p.xy;
                half4 cY = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvY), cX = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvX), cZ = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvZ);
                half4 mY = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, uvY), mX = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, uvX), mZ = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, uvZ);
                half3 nY = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvY), _BumpScale);
                half3 nX = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvX), _BumpScale);
                half3 nZ = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvZ), _BumpScale);
                half3 albedo = (cX.rgb * w.x + cY.rgb * w.y + cZ.rgb * w.z) * _BaseColor.rgb;
                half4 mask = mX * w.x + mY * w.y + mZ * w.z;
                // Whiteout-blended triplanar normal.
                float3 normal = normalize(n + w.x * float3(0, nX.y, nX.x) + w.y * float3(nY.x, 0, nY.y) + w.z * float3(nZ.x, nZ.y, 0));

                // Macro variation (anti-tiling): broad patches of slightly lighter/darker, warmer/cooler ground.
                float macro = ValueNoise(input.positionWS.xz / _MacroScale) * 0.65 + ValueNoise(input.positionWS.xz / (_MacroScale * 0.37) + 17) * 0.35;
                albedo *= 1 + (macro - 0.5) * 2 * _MacroStrength * half3(1, 0.96, 0.9);

                half smoothness = mask.a;
                half occlusion = mask.g;
                // Rain: porous ground darkens; puddles collect in the low parts of the height map on flat ground.
                float wet = saturate(_EnvWetness);
                float flat = saturate((n.y - 0.85) * 8);
                float low = 1 - mask.b;
                float puddleField = ValueNoise(input.positionWS.xz * 0.35) * 0.7 + low * 0.5;
                float puddle = saturate((puddleField - (1.15 - wet * _Puddles * 0.6)) * 8) * flat * wet;
                albedo *= lerp(1, 1 - 0.4 * _Porosity, wet);
                albedo = lerp(albedo, albedo * 0.35, puddle);
                smoothness = lerp(lerp(smoothness, max(smoothness, 0.6), wet * flat), 0.96, puddle);
                normal = normalize(lerp(normal, n, puddle));
                // Raindrop rings in puddles.
                if (puddle > 0.01 && _EnvRain > 0.05)
                {
                    float2 q = input.positionWS.xz * 3;
                    float2 cell = floor(q);
                    float2 local = frac(q) - 0.5;
                    float phase = frac(_WindTime * 1.1 + Hash(cell));
                    float ring = saturate(1 - abs(length(local) - phase * 0.45) * 20) * (1 - phase) * _EnvRain;
                    normal = normalize(normal + float3(local.x, 0, local.y) * ring * 2 * puddle);
                }

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.smoothness = smoothness;
                surface.occlusion = occlusion;
                surface.alpha = 1;
                surface.normalTS = half3(0, 0, 1);
                InputData data = (InputData)0;
                data.positionWS = input.positionWS;
                data.positionCS = input.positionCS;
                data.normalWS = normal;
                data.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                data.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                data.fogCoord = input.fog;
                data.bakedGI = SampleSH(normal);
                data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                data.shadowMask = half4(1, 1, 1, 1);
                half4 colour = UniversalFragmentPBR(data, surface);
                colour.rgb = MixFog(colour.rgb, input.fog);
                return half4(colour.rgb, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                float3 normal = TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 toLight = normalize(_LightPosition - world);
                #else
                    float3 toLight = _LightDirection;
                #endif
                float4 clip = TransformWorldToHClip(ApplyShadowBias(world, normal, toLight));
                #if UNITY_REVERSED_Z
                    clip.z = min(clip.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    clip.z = max(clip.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                output.positionCS = clip;
                return output;
            }
            half4 frag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert(Attributes input) { Varyings output; UNITY_SETUP_INSTANCE_ID(input); output.positionCS = TransformObjectToHClip(input.positionOS.xyz); return output; }
            half4 frag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            Varyings vert(Attributes input)
            {
                Varyings output; UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 frag(Varyings input) : SV_Target { return half4(NormalizeNormalPerPixel(input.normalWS), 0); }
            ENDHLSL
        }
    }
    Fallback Off
}
