// ContextSpeak inland water: pools, rivers, streams, lakes, ponds and waterfall sheets (URP 17).
// Absorption by real depth (camera depth texture), refraction (opaque texture), Fresnel reflection
// from reflection probes, sun glint, procedural ripple normals that flow along _Flow, contact foam
// around rocks and banks, raindrop rings while it rains, and a caustic approximation drawn onto
// the refracted floor of shallow water. Parameters per water type come from WaterSettings.cs.
// Needs the camera depth + opaque textures (the Environment setup enables them).
Shader "ContextSpeak/Water Surface"
{
    Properties
    {
        _Shallow ("Shallow tint", Color) = (0.55, 0.8, 0.75, 1)
        _Deep ("Deep colour", Color) = (0.05, 0.2, 0.22, 1)
        _FoamColor ("Foam colour", Color) = (0.92, 0.95, 0.95, 1)
        _Absorption ("Colour depth falloff", Float) = 0.6
        _Clarity ("Transparency depth falloff", Float) = 0.9
        _Refraction ("Refraction", Range(0, 0.1)) = 0.025
        _RippleScale ("Ripple scale", Float) = 1.2
        _RippleStrength ("Ripple strength", Range(0, 2)) = 0.4
        _Flow ("Flow (xy direction m/s)", Vector) = (0, 0, 0, 0)
        _Turbulence ("Current turbulence", Range(0, 2)) = 0
        _ContactFoam ("Contact foam width (m)", Float) = 0.25
        _Caustics ("Caustics", Range(0, 2)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.95
        _Vertical ("Falling sheet (waterfall)", Range(0, 1)) = 0
        _RippleDisplace ("Interactive ripple displacement", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-20" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite On
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Assets/ContextSpeak/Runtime/Water/Shaders/WaterRipple.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Shallow, _Deep, _FoamColor;
                float4 _Flow;
                float _Absorption, _Clarity, _Refraction, _RippleScale, _RippleStrength, _Turbulence, _ContactFoam, _Caustics, _Smoothness, _Vertical, _RippleDisplace;
            CBUFFER_END
            float _EnvRain;            // 0…1 from EnvironmentManager
            float _WindStrength;
            float4 _WindDirection;
            float _WindTime;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1;
                float4 screen : TEXCOORD2; float2 uv : TEXCOORD3; float fog : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float2 Hash2(float2 p) { p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3))); return frac(sin(p) * 43758.5453) * 2 - 1; }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p), u = f * f * (3 - 2 * f);
                return lerp(lerp(dot(Hash2(i), f), dot(Hash2(i + float2(1, 0)), f - float2(1, 0)), u.x),
                            lerp(dot(Hash2(i + float2(0, 1)), f - float2(0, 1)), dot(Hash2(i + 1), f - 1), u.x), u.y);
            }
            float Height(float2 p, float t)
            {
                float2 flow = _Flow.xy;
                float wind = _WindStrength * 0.02;
                float2 w = _WindDirection.xy * wind * t;
                float h = Noise((p - flow * t) * _RippleScale + w) * 0.6
                        + Noise((p - flow * t * 1.3) * _RippleScale * 2.3 + float2(5.2, 1.3) - w * 1.7) * 0.3
                        + Noise((p - flow * t * 0.7) * _RippleScale * 5.1 + float2(1.7, 9.2)) * 0.15 * (1 + _Turbulence);
                return h;
            }
            // Rain: expanding rings in a grid of random cells.
            float RainRings(float2 p, float t)
            {
                float rings = 0;
                for (int layer = 0; layer < 2; layer++)
                {
                    float2 q = p * (2.2 + layer * 1.3) + layer * 7.1;
                    float2 cell = floor(q);
                    float2 local = frac(q) - 0.5 - Hash2(cell) * 0.3;
                    float phase = frac(t * 0.9 + Hash2(cell + 3.1).x * 0.5 + 0.5);
                    float radius = phase * 0.45;
                    float d = length(local);
                    rings += sin(saturate(1 - abs(d - radius) * 18) * 3.14159) * (1 - phase) * step(Hash2(cell + 9.7).y, _EnvRain * 1.6 - 0.6);
                }
                return rings;
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionWS.y += CSRippleHeightLod(output.positionWS) * _RippleDisplace * (1 - _Vertical);   // interactive ripples (dense meshes)
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.screen = ComputeScreenPos(output.positionCS);
                output.uv = input.uv;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input, bool front : SV_IsFrontFace) : SV_Target
            {
                float t = _WindTime;
                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                // Ripple normal in the surface plane (waterfalls use the mesh UV as the flow axis).
                float2 p = lerp(input.positionWS.xz, float2(input.uv.x * 4, input.uv.y * 4 + t * 2.5), _Vertical);
                float e = 0.05;
                float h = Height(p, t) + RainRings(p, t) * 0.25;
                float hx = Height(p + float2(e, 0), t) + RainRings(p + float2(e, 0), t) * 0.25;
                float hz = Height(p + float2(0, e), t) + RainRings(p + float2(0, e), t) * 0.25;
                float3 baseN = normalize(input.normalWS) * (front ? 1 : -1);
                float3 tangent = normalize(cross(baseN, abs(baseN.y) < 0.9 ? float3(0, 1, 0) : float3(0, 0, 1)));
                float3 bitangent = cross(baseN, tangent);
                float strength = _RippleStrength * (1 + _Vertical * 2);
                float3 normal = normalize(baseN + (tangent * (h - hx) + bitangent * (h - hz)) / e * strength * 0.15);
                // Interactive ripples from characters, objects, rain and fish (WaterRippleSimulation).
                float2 rippleSlope; float rippleFoam;
                CSRipple(input.positionWS, rippleSlope, rippleFoam);
                rippleSlope *= 1 - _Vertical; rippleFoam *= 1 - _Vertical;
                normal = normalize(normal + float3(-rippleSlope.x, 0, -rippleSlope.y) * (front ? 1 : -1));

                float2 uv = input.screen.xy / input.screen.w;
                float surfaceDepth = input.screen.w;
                float depth = max(0, LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams) - surfaceDepth);
                float2 refractUv = uv + (normal.xz - baseN.xz) * _Refraction * saturate(depth);
                float refractDepth = LinearEyeDepth(SampleSceneDepth(refractUv), _ZBufferParams) - surfaceDepth;
                if (refractDepth < 0) { refractUv = uv; refractDepth = depth; }
                half3 below = SampleSceneColor(refractUv);

                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                // Caustics on the floor seen through shallow water (approximation; no light transport).
                float2 cp = input.positionWS.xz * 1.6;
                float caustic = pow(saturate(1 - abs(Noise(cp + t * 0.35) + Noise(cp * 1.7 - t * 0.27))) , 6);
                below *= 1 + caustic * _Caustics * sun.color * sun.shadowAttenuation * saturate(1.5 - refractDepth * 0.6) * saturate(refractDepth * 3);

                float colourMix = 1 - exp(-refractDepth * _Absorption);
                half3 body = lerp(_Shallow.rgb * below, _Deep.rgb * (SampleSH(float3(0, 1, 0)) + sun.color * 0.15), colourMix);
                float transmit = exp(-refractDepth * _Clarity);
                body = lerp(body, below, transmit * 0.6);

                float fresnel = 0.02 + 0.98 * pow(1 - saturate(dot(normal, viewDir)), 5);
                half3 sky = GlossyEnvironmentReflection(reflect(-viewDir, normal), 1 - _Smoothness, 1);
                float3 halfway = normalize(sun.direction + viewDir);
                half3 glint = sun.color * pow(saturate(dot(normal, halfway)), 900) * 5 * sun.shadowAttenuation * _Smoothness;
                half3 colour = lerp(body, sky, saturate(fresnel)) + glint;

                // Foam where the surface meets banks and rocks, broken up by the current.
                float churn = Noise(p * 3 - _Flow.xy * t * 1.5) * 0.5 + 0.5;
                float contact = saturate(1 - depth / max(_ContactFoam * (0.6 + churn + length(_Flow.xy) * 0.4), 0.01));
                float foam = saturate(contact * smoothstep(0.35, 0.7, churn + contact * 0.3) + _Vertical * smoothstep(0.55, 0.9, churn));
                foam = saturate(foam + rippleFoam);
                colour = lerp(colour, _FoamColor.rgb * (SampleSH(float3(0, 1, 0)) + sun.color * sun.shadowAttenuation * 0.8), foam);

                float alpha = saturate(max(1 - transmit, fresnel) + foam + 0.1);
                alpha = lerp(alpha * saturate(depth * 8), alpha, _Vertical);
                colour = MixFog(colour, input.fog);
                return half4(colour * alpha, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
