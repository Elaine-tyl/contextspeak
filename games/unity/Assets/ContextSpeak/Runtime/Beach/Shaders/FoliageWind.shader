// ContextSpeak tropical foliage (URP, GPU instanced): Lambert + main light shadows + ambient,
// vertex wind sway that grows with height (trunks bend gently, leaves flutter individually).
Shader "ContextSpeak/Foliage Wind"
{
    Properties
    {
        _BaseColor ("Colour", Color) = (0.3, 0.55, 0.2, 1)
        _Flex ("Flexibility (0 trunk … 1 leaf)", Range(0, 1)) = 1
        _Smoothness ("Smoothness", Range(0, 1)) = 0.3
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float _Flex, _Smoothness;
        CBUFFER_END
        float _WindStrength, _WindGust;      // set by FoliageWind.cs from the sky's wind
        float4 _WindDirection;

        float3 Sway(float3 positionOS, out float3 world)
        {
            world = TransformObjectToWorld(positionOS);
            float3 origin = TransformObjectToWorld(float3(0, 0, 0));
            float height = max(0, world.y - origin.y);
            float phase = dot(origin.xz, float2(0.37, 0.71));
            float sway = (sin(_Time.y * 1.1 + phase) * 0.6 + sin(_Time.y * 2.3 + phase * 1.7) * 0.3) * (0.4 + _WindGust);
            float bend = _WindStrength * 0.012 * height * height * (0.25 + 0.75 * _Flex);
            float flutter = _Flex * _WindStrength * 0.015 * sin(_Time.y * 9 + dot(world, float3(3.1, 1.7, 2.3)));
            float2 dir = normalize(_WindDirection.xy + 1e-4);
            world.xz += dir * (bend * (0.6 + sway)) + flutter;
            world.y += flutter * 0.5 - bend * 0.08;
            return world;
        }
        ENDHLSL
        Pass
        {
            Name "Foliage"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float fog : TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID };
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 world;
                Sway(input.positionOS.xyz, world);
                output.positionWS = world;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(world);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 frag(Varyings input, bool front : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 normal = normalize(front ? input.normalWS : -input.normalWS);
                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float wrap = saturate((dot(normal, sun.direction) + 0.35) / 1.35);
                half3 ambient = SampleSH(normal);
                half3 colour = _BaseColor.rgb * (ambient + sun.color * wrap * sun.shadowAttenuation);
                // Light through thin leaves.
                float3 view = normalize(GetWorldSpaceViewDir(input.positionWS));
                colour += _BaseColor.rgb * sun.color * _Flex * 0.35 * pow(saturate(dot(view, -sun.direction)), 4) * sun.shadowAttenuation;
                return half4(MixFog(colour, input.fog), 1);
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
            float3 _LightDirection;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 world;
                Sway(input.positionOS.xyz, world);
                float3 normal = TransformObjectToWorldNormal(input.normalOS);
                float4 clip = TransformWorldToHClip(ApplyShadowBias(world, normal, _LightDirection));
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
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 world;
                Sway(input.positionOS.xyz, world);
                output.positionCS = TransformWorldToHClip(world);
                return output;
            }
            half4 frag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    Fallback Off
}
