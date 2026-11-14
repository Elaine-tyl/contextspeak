Shader "ContextSpeak/UI/FrostedGlass"
{
    Properties { _MainTex ("World colour", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        float4 _Direction;
        float4 _UvRect;
        fixed4 blur(v2f_img i) : SV_Target
        {
            float2 d = _Direction.xy;
            fixed4 colour = tex2D(_MainTex, i.uv) * 0.227027;
            colour += (tex2D(_MainTex, i.uv + d) + tex2D(_MainTex, i.uv - d)) * 0.1945946;
            colour += (tex2D(_MainTex, i.uv + 2*d) + tex2D(_MainTex, i.uv - 2*d)) * 0.1216216;
            colour += (tex2D(_MainTex, i.uv + 3*d) + tex2D(_MainTex, i.uv - 3*d)) * 0.054054;
            colour += (tex2D(_MainTex, i.uv + 4*d) + tex2D(_MainTex, i.uv - 4*d)) * 0.016216;
            return colour;
        }
        fixed4 cropAndFrost(v2f_img i) : SV_Target
        {
            fixed3 world = tex2D(_MainTex, _UvRect.xy + i.uv * _UvRect.zw).rgb;
            return fixed4(lerp(world, fixed3(0.968627, 0.952941, 0.917647), 0.72), 1);
        }
        ENDCG
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment blur
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment cropAndFrost
            ENDCG
        }
    }
}
