// ContextSpeak vegetation (URP 17, forward, SRP-batcher + GPU-instancing compatible).
// Textured PBR (base colour, normal, glTF metallic-roughness), double-sided, thin-leaf translucency,
// screen-space AO and additional lights through URP's own PBR path, plus wetness while it rains.
// Wind (GlobalWindController globals): low-frequency bending that grows with height (trunks stay
// rooted), high-frequency flutter only where the texture is leaf-coloured, travelling grass waves
// and localized gusts. Per-type response comes from _WindResponse / _Flex.
Shader "ContextSpeak/Vegetation Lit"
{
    Properties
    {
        _BaseMap ("Base colour", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        [Normal] _BumpMap ("Normal map", 2D) = "bump" {}
        _BumpScale ("Normal strength", Range(0, 2)) = 1
        _RoughnessMap ("Metallic-roughness (glTF: G = roughness)", 2D) = "white" {}
        [Toggle] _UseRoughnessMap ("Use roughness map", Float) = 0
        _Smoothness ("Smoothness (no map)", Range(0, 1)) = 0.25
        _ObjectHeight ("Mesh height (object units)", Float) = 1
        _WindResponse ("Wind response", Range(0, 3)) = 1
        _Flex ("Leaf flutter", Range(0, 1)) = 0.8
        _LeafMask ("Flutter only leaf-coloured texels", Range(0, 1)) = 1
        _GrassWave ("Grass waves", Range(0, 1)) = 0
        _Translucency ("Translucency", Range(0, 2)) = 0.6
        _TranslucencyColor ("Translucency tint", Color) = (0.75, 0.9, 0.35, 1)
        _HueVariation ("Per-plant colour variation", Range(0, 0.3)) = 0.08
        _Porosity ("Darkening when wet", Range(0, 1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_RoughnessMap); SAMPLER(sampler_RoughnessMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor, _TranslucencyColor;
            float _BumpScale, _UseRoughnessMap, _Smoothness, _ObjectHeight, _WindResponse, _Flex, _LeafMask, _GrassWave;
            float _Translucency, _HueVariation, _Porosity, _Cull;
        CBUFFER_END
        // Globals from GlobalWindController / EnvironmentManager.
        float _WindStrength;        // m/s
        float _WindGust;            // 0…1 slow gust envelope
        float4 _WindDirection;      // xy = horizontal direction
        float4 _WindGustCentre;     // xy = world xz of a travelling gust, z = radius, w = extra strength
        float _WindTime;
        float _EnvWetness;          // 0…1

        float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

        // Leafiness of the texel: green/yellow-green foliage flutters, bark and stems don't.
        float Leafiness(float2 uv)
        {
            half3 c = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, uv, 4).rgb;
            float green = saturate((c.g - max(c.r * 0.85, c.b) + 0.02) * 8);
            return lerp(1, green, _LeafMask);
        }

        float3 Wind(float3 positionOS, float2 uv, out float leaf)
        {
            float3 world = TransformObjectToWorld(positionOS);
            float3 origin = TransformObjectToWorld(float3(0, 0, 0));
            float scaleY = length(float3(UNITY_MATRIX_M[0].y, UNITY_MATRIX_M[1].y, UNITY_MATRIX_M[2].y));
            float heightWorld = max(_ObjectHeight * scaleY, 0.05);
            float h = saturate(positionOS.y / max(_ObjectHeight, 1e-3));
            leaf = Leafiness(uv);
            float2 dir = normalize(_WindDirection.xy + float2(1e-4, 0));
            float t = _WindTime;
            float phase = Hash(origin.xz) * 6.2831;
            // Localized gust front.
            float gustLocal = _WindGustCentre.w * saturate(1 - distance(origin.xz, _WindGustCentre.xy) / max(_WindGustCentre.z, 1));
            float strength = _WindStrength * (0.75 + 0.5 * _WindGust) + gustLocal;
            // Low frequency: the whole plant leans and sways from its base (quadratic with height).
            float sway = 0.65 + 0.35 * sin(t * (0.6 + 0.25 * phase / 6.28) + phase) + 0.15 * sin(t * 1.7 + phase * 1.3);
            float bend = strength * _WindResponse * 0.0065 * h * h * heightWorld * sway;
            // Grass waves rolling across the field.
            float wave = _GrassWave * pow(saturate(sin(dot(world.xz, dir) * 0.45 - t * (0.6 + strength * 0.12)) * 0.5 + 0.5), 2) * strength * 0.012 * h;
            // High frequency: leaf flutter, only on leaf texels, stronger toward the tips.
            float flutter = leaf * _Flex * (0.004 + strength * 0.0022) * h * sin(t * (7 + strength * 0.5) + dot(world, float3(2.3, 1.9, 3.1)) * 3 + phase);
            world.xz += dir * (bend + wave);
            world.y -= (bend + wave) * (bend + wave) / max(heightWorld, 0.1) * 0.5;      // keep length when leaning
            world += float3(flutter, flutter * 0.6, -flutter * 0.8);
            return world;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
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

            struct Attributes
            {
                float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2; float4 tangentWS : TEXCOORD3; float4 extra : TEXCOORD4;   // x fog, y leaf, z hue
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                float leaf;
                float3 world = Wind(input.positionOS.xyz, output.uv, leaf);
                output.positionWS = world;
                output.positionCS = TransformWorldToHClip(world);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.normalWS = normals.normalWS;
                output.tangentWS = float4(normals.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.extra = float4(ComputeFogFactor(output.positionCS.z), leaf, Hash(TransformObjectToWorld(float3(0, 0, 0)).xz), 0);
                return output;
            }

            half4 frag(Varyings input, bool front : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                // Per-plant variation: slight brightness and yellow/green shift so copies don't repeat.
                float hue = input.extra.z * 2 - 1;
                albedo.rgb *= 1 + hue * _HueVariation * half3(0.6, 0.35, -0.2) + (input.extra.z - 0.5) * _HueVariation;

                float3 normalWS = normalize(input.normalWS) * (front ? 1 : -1);
                float3 tangentWS = input.tangentWS.xyz;
                if (dot(tangentWS, tangentWS) > 1e-4)
                {
                    float3 bitangent = input.tangentWS.w * cross(normalWS, normalize(tangentWS));
                    half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                    normalWS = normalize(mul(normalTS, float3x3(normalize(tangentWS), bitangent, normalWS)));
                }
                half roughness = _UseRoughnessMap > 0.5 ? SAMPLE_TEXTURE2D(_RoughnessMap, sampler_RoughnessMap, input.uv).g : 1 - _Smoothness;
                half smoothness = saturate(1 - roughness);
                // Rain: porous surfaces darken; up-facing ones gain a film of water.
                half wet = _EnvWetness;
                albedo.rgb *= lerp(1, 1 - 0.45 * _Porosity, wet);
                smoothness = lerp(smoothness, max(smoothness, 0.72), wet * saturate(normalWS.y * 0.8 + 0.2) * 0.8);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo.rgb;
                surface.metallic = 0;
                surface.specular = half3(0, 0, 0);
                surface.smoothness = smoothness;
                surface.normalTS = half3(0, 0, 1);
                surface.occlusion = 1;
                surface.alpha = 1;

                InputData data = (InputData)0;
                data.positionWS = input.positionWS;
                data.positionCS = input.positionCS;
                data.normalWS = normalWS;
                data.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                data.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                data.fogCoord = input.extra.x;
                data.bakedGI = SampleSH(normalWS);
                data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                data.shadowMask = half4(1, 1, 1, 1);
                half4 colour = UniversalFragmentPBR(data, surface);

                // Thin-leaf translucency: sunlight seen through leaves when looking toward the sun.
                Light sun = GetMainLight(data.shadowCoord);
                float3 through = -sun.direction + normalWS * 0.25;
                half back = pow(saturate(dot(data.viewDirectionWS, normalize(through))), 3.5) * 0.7 + saturate(dot(-normalWS, sun.direction)) * 0.3;
                colour.rgb += albedo.rgb * _TranslucencyColor.rgb * sun.color * (back * _Translucency * input.extra.y * sun.shadowAttenuation * sun.distanceAttenuation);
                colour.rgb = MixFog(colour.rgb, input.extra.x);
                return half4(colour.rgb, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            Cull Off
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
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float leaf;
                float3 world = Wind(input.positionOS.xyz, TRANSFORM_TEX(input.uv, _BaseMap), leaf);
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
            Cull Off
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float leaf;
                output.positionCS = TransformWorldToHClip(Wind(input.positionOS.xyz, TRANSFORM_TEX(input.uv, _BaseMap), leaf));
                return output;
            }
            half4 frag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            Cull Off
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float leaf;
                output.positionCS = TransformWorldToHClip(Wind(input.positionOS.xyz, TRANSFORM_TEX(input.uv, _BaseMap), leaf));
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 frag(Varyings input, bool front : SV_IsFrontFace) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(input.normalWS * (front ? 1 : -1)), 0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
