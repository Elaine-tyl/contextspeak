// ContextSpeak tropical ocean (URP). Layered Gerstner swells + chop (shared with C# OceanWaves for
// buoyancy), analytic + procedural detail normals, depth-based colour and transparency from the
// scene depth, refraction from the opaque texture, Fresnel sky reflection (reflection probe) and
// sun glint, shoreline and crest foam, wave attenuation in the shallows near the island.
Shader "ContextSpeak/Ocean"
{
    Properties
    {
        _Shallow ("Shallow colour", Color) = (0.25, 0.85, 0.8, 1)
        _Deep ("Deep colour", Color) = (0.02, 0.18, 0.35, 1)
        _Scatter ("Sub-surface scatter colour", Color) = (0.2, 0.9, 0.75, 1)
        _FoamColor ("Foam colour", Color) = (0.95, 0.97, 0.98, 1)
        _DepthFalloff ("Colour depth falloff", Float) = 0.18
        _Clarity ("Clarity (transparency depth)", Float) = 0.45
        _Refraction ("Refraction strength", Float) = 0.04
        _ShoreFoam ("Shore foam width (m)", Float) = 1.6
        _CrestFoam ("Crest foam", Range(0, 2)) = 0.6
        _DetailScale ("Detail normal scale", Float) = 0.35
        _DetailStrength ("Detail normal strength", Range(0, 2)) = 0.6
        _Smoothness ("Smoothness", Range(0, 1)) = 0.94
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Ocean"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite On
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Assets/ContextSpeak/Runtime/Water/Shaders/WaterRipple.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Shallow, _Deep, _Scatter, _FoamColor;
                float _DepthFalloff, _Clarity, _Refraction, _ShoreFoam, _CrestFoam, _DetailScale, _DetailStrength, _Smoothness;
            CBUFFER_END
            // Set every frame by OceanWaves.cs (same maths on the CPU).
            float4 _OceanWave[8];     // x,y direction · z steepness · w wavelength
            float _OceanWaveCount, _OceanTime, _OceanAmplitude;
            float4 _OceanShore;       // xyz island centre, w shoreline radius

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 screen : TEXCOORD2;
                float crest : TEXCOORD3;
                float fog : TEXCOORD4;
            };

            float ShoreFactor(float2 xz)
            {
                float distance = length(xz - _OceanShore.xz);
                return lerp(0.12, 1.0, smoothstep(_OceanShore.w - 8.0, _OceanShore.w + 90.0, distance));
            }

            void Gerstner(float2 xz, out float3 offset, out float3 tangent, out float3 binormal)
            {
                offset = 0; tangent = float3(1, 0, 0); binormal = float3(0, 0, 1);
                float shore = ShoreFactor(xz) * _OceanAmplitude;
                [loop] for (int i = 0; i < (int)_OceanWaveCount; i++)
                {
                    float4 wave = _OceanWave[i];
                    float k = 6.2831853 / wave.w;
                    float c = sqrt(9.81 / k);
                    float2 d = normalize(wave.xy);
                    float f = k * (dot(d, xz) - c * _OceanTime);
                    float a = wave.z / k * shore;
                    float s = sin(f), co = cos(f);
                    offset += float3(d.x * a * co, a * s, d.y * a * co);
                    tangent += float3(-d.x * d.x * wave.z * shore * s, d.x * wave.z * shore * co, -d.x * d.y * wave.z * shore * s);
                    binormal += float3(-d.x * d.y * wave.z * shore * s, d.y * wave.z * shore * co, -d.y * d.y * wave.z * shore * s);
                }
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                float3 offset, tangent, binormal;
                Gerstner(world.xz, offset, tangent, binormal);
                world += offset;
                output.positionWS = world;
                output.normalWS = normalize(cross(binormal, tangent));
                output.crest = offset.y;
                output.positionCS = TransformWorldToHClip(world);
                output.screen = ComputeScreenPos(output.positionCS);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            float Hash(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float Noise(float2 p)
            {
                float2 i = floor(p); float2 f = frac(p); float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }
            float Ripples(float2 p)
            {
                return Noise(p) * 0.5 + Noise(p * 2.1 + 3.7) * 0.3 + Noise(p * 4.3 - 1.3) * 0.2;
            }
            float3 DetailNormal(float2 xz)
            {
                float2 p1 = xz * _DetailScale + float2(_OceanTime * 0.35, _OceanTime * 0.21);
                float2 p2 = xz * _DetailScale * 1.9 - float2(_OceanTime * 0.27, -_OceanTime * 0.33);
                const float e = 0.12;
                float h = Ripples(p1) + Ripples(p2) * 0.6;
                float hx = Ripples(p1 + float2(e, 0)) + Ripples(p2 + float2(e, 0)) * 0.6;
                float hz = Ripples(p1 + float2(0, e)) + Ripples(p2 + float2(0, e)) * 0.6;
                return normalize(float3((h - hx) / e, 1.0 / max(_DetailStrength, 0.01), (h - hz) / e));
            }

            half4 frag(Varyings input, bool front : SV_IsFrontFace) : SV_Target
            {
                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                float3 detail = DetailNormal(input.positionWS.xz);
                float3 normal = normalize(float3(input.normalWS.x + detail.x, input.normalWS.y, input.normalWS.z + detail.z));
                // Interactive ripples layered on the Gerstner waves (the base waves are untouched).
                float2 rippleSlope; float rippleFoam;
                CSRipple(input.positionWS, rippleSlope, rippleFoam);
                normal = normalize(normal + float3(-rippleSlope.x, 0, -rippleSlope.y));
                if (!front) normal = -normal;

                // Water depth below this pixel from the scene depth.
                float2 uv = input.screen.xy / input.screen.w;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float surfaceDepth = input.screen.w;
                float depth = max(0, sceneDepth - surfaceDepth);
                float2 refractUv = uv + normal.xz * _Refraction * saturate(depth * 0.5);
                float refractDepth = LinearEyeDepth(SampleSceneDepth(refractUv), _ZBufferParams) - surfaceDepth;
                if (refractDepth < 0) { refractUv = uv; refractDepth = depth; }
                half3 below = SampleSceneColor(refractUv);

                // Absorption: shallow water is clear turquoise, deep water dark blue.
                float colourMix = 1 - exp(-refractDepth * _DepthFalloff);
                half3 water = lerp(_Shallow.rgb, _Deep.rgb, colourMix);
                float transmit = exp(-refractDepth * _Clarity);
                half3 body = lerp(water, below * _Shallow.rgb * 1.4, transmit);

                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float sunAmount = saturate(dot(normal, sun.direction)) * sun.shadowAttenuation;
                body *= 0.55 + 0.45 * sunAmount;
                // Light scattering through wave crests.
                float scatter = saturate(input.crest * 0.6) * pow(saturate(dot(viewDir, -sun.direction) * 0.5 + 0.5), 3);
                body += _Scatter.rgb * scatter * sun.color * 0.6;

                // Fresnel reflection of the sky (reflection probe) and a sharp sun glint.
                float fresnel = 0.02 + 0.98 * pow(1 - saturate(dot(normal, viewDir)), 5);
                float3 reflected = reflect(-viewDir, normal);
                half3 sky = GlossyEnvironmentReflection(reflected, 1 - _Smoothness, 1);
                float3 halfway = normalize(sun.direction + viewDir);
                half3 glint = sun.color * pow(saturate(dot(normal, halfway)), 600) * 6 * sun.shadowAttenuation;
                half3 colour = lerp(body, sky, saturate(fresnel)) + glint;

                // Foam: a breaking band along the shore and broken water on steep crests.
                float foamNoise = Ripples(input.positionWS.xz * 0.9 + _OceanTime * 0.3);
                float swash = 0.5 + 0.5 * sin(_OceanTime * 1.3 + length(input.positionWS.xz - _OceanShore.xz) * 0.35);
                float shore = saturate(1 - depth / max(_ShoreFoam * (0.6 + 0.8 * swash), 0.01));
                float crest = saturate((input.crest - 0.35) * 2) * _CrestFoam;
                float foam = saturate((shore + crest) * smoothstep(0.35, 0.75, foamNoise + shore * 0.4));
                foam = saturate(foam + rippleFoam);
                colour = lerp(colour, _FoamColor.rgb * (0.6 + 0.4 * sun.color), foam);

                float alpha = saturate(max(1 - transmit, fresnel) + foam + 0.05);
                colour = MixFog(colour, input.fog);
                // Edge fade where the water meets the sand.
                alpha *= saturate(depth * 4);
                return half4(colour * alpha, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
