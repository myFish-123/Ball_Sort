Shader "BallSort/WaterStream"
{
    Properties
    {
        _MainTex ("Water Texture", 2D) = "white" {}
        _FlowSpeed ("Texture Flow Speed", Float) = 3
        _StripeStrength ("Temporary Stripe Strength", Range(0, 1)) = 0.25
        [HideInInspector] _PathOffset ("Path Offset", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _FlowSpeed;
                float _StripeStrength;
                float _PathOffset;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                uv.x += _PathOffset - _Time.y * _FlowSpeed;
                uv = uv * _MainTex_ST.xy + _MainTex_ST.zw;
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * input.color;
                half stripe = pow(saturate(0.5 + 0.5 * sin(uv.x * 18.849556)), 6);
                color.rgb = lerp(color.rgb, half3(1, 1, 1), stripe * _StripeStrength);
                half edge = smoothstep(0, 0.12, input.uv.y) * smoothstep(0, 0.12, 1 - input.uv.y);
                color.a *= edge;
                return color;
            }
            ENDHLSL
        }
    }
}
