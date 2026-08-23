Shader "Custom/CozyToon"
{
    Properties
    {
        [Header(Base Properties)]
        _BaseMap ("Base Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)

        [Header(Cozy Toon Shading)]
        _ShadowThreshold ("Shadow Threshold", Range(0, 1)) = 0.5
        _ShadowSmoothness ("Shadow Smoothness (Softness)", Range(0.01, 0.5)) = 0.15
        _ShadowColor ("Shadow Tint Color", Color) = (0.7, 0.65, 0.75, 1) // Hafif sıcak/mor pastel gölge

        [Header(Rim Lighting (Edge Glow))]
        [Toggle(_RIM_LIGHT)] _EnableRim ("Enable Rim Light", Float) = 1
        _RimColor ("Rim Light Color", Color) = (1, 0.95, 0.8, 1) // Sıcak gün ışığı ışıltısı
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 3.0
        _RimSmoothness ("Rim Smoothness", Range(0.01, 0.5)) = 0.2

        [Header(Specular Highlight)]
        [Toggle(_SPECULAR_ON)] _EnableSpecular ("Enable Specular", Float) = 1
        _SpecColor ("Specular Color", Color) = (1, 1, 1, 0.5)
        _Glossiness ("Glossiness / Shininess", Range(8, 128)) = 32
        _SpecSmoothness ("Specular Sharpness", Range(0.01, 0.2)) = 0.05
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline" 
            "Queue" = "Geometry" 
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float3 viewDirWS    : TEXCOORD2;
                float3 positionWS   : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _ShadowColor;
                float4 _RimColor;
                float4 _SpecColor;
                float _ShadowThreshold;
                float _ShadowSmoothness;
                float _EnableRim;
                float _RimPower;
                float _RimSmoothness;
                float _EnableSpecular;
                float _Glossiness;
                float _SpecSmoothness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = normalize(normalInput.normalWS);
                output.viewDirWS = normalize(GetCameraPositionWS() - vertexInput.positionWS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float4 mainTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                float4 albedo = mainTex * _BaseColor;

                // Ana Işık Bilgileri
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float3 lightDir = normalize(mainLight.direction);
                float3 lightColor = mainLight.color;
                float shadowAtten = mainLight.shadowAttenuation;

                // N dot L (Işık açısı)
                float3 N = normalize(input.normalWS);
                float3 V = normalize(input.viewDirWS);
                float NdotL = dot(N, lightDir);
                float halfLambert = NdotL * 0.5 + 0.5; // Yumuşak ışık dağılımı (Half-Lambert)
                
                // Soft Cel Shading Ramp (Yumuşak Çizgi Film Gölgesi)
                float lightIntensity = halfLambert * shadowAtten;
                float toonRamp = smoothstep(_ShadowThreshold - _ShadowSmoothness, _ShadowThreshold + _ShadowSmoothness, lightIntensity);
                
                // Gölge Rengi ve Işık Karışımı
                float3 diffuseLighting = lerp(_ShadowColor.rgb, lightColor, toonRamp);

                // Ortam Işığı (Ambient Lighting - Cozy sıcaklık için)
                float3 ambient = SampleSH(N) * 0.5;
                float3 finalColor = albedo.rgb * (diffuseLighting + ambient);

                // 1. Cozy Rim Lighting (Kenar Işıltısı)
                if (_EnableRim > 0.5)
                {
                    float NdotV = 1.0 - saturate(dot(N, V));
                    float rim = pow(NdotV, _RimPower);
                    float rimIntensity = smoothstep(0.5 - _RimSmoothness, 0.5 + _RimSmoothness, rim) * toonRamp;
                    finalColor += _RimColor.rgb * rimIntensity * _RimColor.a;
                }

                // 2. Cartoony Specular Highlight (Çizgi Film Parlaması)
                if (_EnableSpecular > 0.5)
                {
                    float3 H = normalize(lightDir + V);
                    float NdotH = saturate(dot(N, H));
                    float spec = pow(NdotH, _Glossiness);
                    float specIntensity = smoothstep(0.5 - _SpecSmoothness, 0.5 + _SpecSmoothness, spec) * shadowAtten;
                    finalColor += _SpecColor.rgb * specIntensity * _SpecColor.a;
                }

                return half4(finalColor, albedo.a);
            }
            ENDHLSL
        }

        // Gölge Dökme (ShadowCaster) Pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
