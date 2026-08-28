Shader "RestaurantLoop/CandyGloss_MobileOptimized"
{
    Properties
    {
        [Header(Surface)]
        [MainTexture] _BaseMap("Base Map (optional)", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color (make it vivid)", Color) = (0.95,0.28,0.35,1)
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5
        [Toggle(_ALPHATEST_ON)] _AlphaClipToggle("Enable Alpha Clipping", Float) = 0
        [Toggle(_VERTEXCOLOR_ON)] _VertexColorToggle("Mesh Has Vertex Colors", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Float) = 2

        [Header(Candy Form puffiness)]
        _Roundness("Roundness (normal to radial)", Range(0,1)) = 0.55
        _RoundPivot("Round Pivot (Object Space)", Vector) = (0,0,0,0)
        _Inflate("Inflate (World Units)", Range(-0.2,0.2)) = 0.0

        [Header(Candy Shading unlit)]
        _LightAngle("Light Angle (Screen Degrees)", Range(0,360)) = 125
        _LightDepth("Light Toward Camera", Range(0,1)) = 0.35
        _LightWrap("Light Wrap", Range(0,1)) = 0.22
        _ShadeLow("Shadow to Base Edge", Range(0,1)) = 0.30
        _ShadeHigh("Base to Light Edge", Range(0,1)) = 0.84
        _ShadeSoft("Ramp Softness", Range(0.01,0.6)) = 0.17

        [Header(Shadow Tone derived from base hue)]
        _ShadowHueShift("Shadow Hue Shift", Range(-0.2,0.2)) = 0.045
        _ShadowSat("Shadow Saturation x", Range(0,2)) = 1.42
        _ShadowValue("Shadow Value x", Range(0,1)) = 0.62

        [Header(Light Tone derived from base hue)]
        _LightHueShift("Light Hue Shift", Range(-0.2,0.2)) = -0.035
        _LightSat("Light Saturation x", Range(0,2)) = 0.95
        _LightValue("Light Value x", Range(1,3)) = 1.14

        [Header(Painted Gloss main blob)]
        _GlossColor("Gloss Color", Color) = (1,1,1,1)
        _Gloss1Strength("Main Blob Strength", Range(0,2)) = 0.95
        _Gloss1Pos("Main Blob Position (XY)", Vector) = (-0.42,0.52,0,0)
        _Gloss1Size("Main Blob Size (XY)", Vector) = (0.30,0.20,0,0)
        _Gloss1Angle("Main Blob Angle", Range(0,180)) = 35
        _Gloss1Soft("Main Blob Softness", Range(0.01,1)) = 0.35
        _Gloss1Wobble("Main Blob Hand Wobble", Range(0,0.5)) = 0.16

        [Header(Painted Gloss companion blob)]
        _Gloss2Strength("Small Blob Strength", Range(0,2)) = 0.7
        _Gloss2Pos("Small Blob Position (XY)", Vector) = (-0.66,0.16,0,0)
        _Gloss2Size("Small Blob Size (XY)", Vector) = (0.11,0.14,0,0)
        _Gloss2Soft("Small Blob Softness", Range(0.01,1)) = 0.45
        _Gloss2Wobble("Small Blob Hand Wobble", Range(0,0.5)) = 0.12

        [Header(Painted Gloss broad sheen)]
        _SheenStrength("Broad Sheen Strength", Range(0,1)) = 0.10
        _SheenEdge("Broad Sheen Edge", Range(0,1)) = 0.72
        _SheenSoft("Broad Sheen Softness", Range(0.01,1)) = 0.28

        [Header(Candy Edges)]
        _InnerEdgeStrength("Inner Edge Darkening", Range(0,1)) = 0.50
        _InnerEdgeWidth("Inner Edge Width", Range(0,1)) = 0.50
        _InnerEdgeHueShift("Inner Edge Hue Shift", Range(-0.2,0.2)) = 0.06
        _InnerEdgeSat("Inner Edge Saturation x", Range(0,2)) = 1.45
        _InnerEdgeValue("Inner Edge Value x", Range(0,1)) = 0.62

        _RimTopColor("Top Rim Color", Color) = (1,0.98,0.92,1)
        _RimTopStrength("Top Rim Strength", Range(0,2)) = 0.5
        _RimPower("Rim Falloff", Range(0.5,8)) = 3.0

        _BounceColor("Bottom Bounce Color", Color) = (0.42,0.62,1.0,1)
        _BounceStrength("Bottom Bounce Strength", Range(0,2)) = 0.35

        [Header(Optional Scene Light)]
        _MainLightInfluence("Follow Scene Light", Range(0,1)) = 0.0
        _ShadowReceive("Receive Realtime Shadows", Range(0,1)) = 0.0
        _ShadowReceiveTint("Received Shadow Tint", Color) = (0.55,0.45,0.68,1)

        [Header(Finish)]
        _Vibrance("Vibrance", Range(0,2)) = 1.32
        _Brightness("Brightness", Range(0,2)) = 1.0

        [Header(Mobile Performance)]
        [Toggle(_MOBILE_SIMPLE)] _MobileSimpleToggle("Mobile Simple Mode", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "IgnoreProjector" = "True"
        }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half _Cutoff;

            half _Cull;
            half _AlphaClipToggle;
            half _VertexColorToggle;
            half _MobileSimpleToggle;

            half _Roundness;
            float4 _RoundPivot;
            half _Inflate;

            half _LightAngle;
            half _LightDepth;
            half _LightWrap;
            half _ShadeLow;
            half _ShadeHigh;
            half _ShadeSoft;

            half _ShadowHueShift;
            half _ShadowSat;
            half _ShadowValue;

            half _LightHueShift;
            half _LightSat;
            half _LightValue;

            half4 _GlossColor;
            half _Gloss1Strength;
            float4 _Gloss1Pos;
            float4 _Gloss1Size;
            half _Gloss1Angle;
            half _Gloss1Soft;
            half _Gloss1Wobble;

            half _Gloss2Strength;
            float4 _Gloss2Pos;
            float4 _Gloss2Size;
            half _Gloss2Soft;
            half _Gloss2Wobble;

            half _SheenStrength;
            half _SheenEdge;
            half _SheenSoft;

            half _InnerEdgeStrength;
            half _InnerEdgeWidth;
            half _InnerEdgeHueShift;
            half _InnerEdgeSat;
            half _InnerEdgeValue;

            half4 _RimTopColor;
            half _RimTopStrength;
            half _RimPower;

            half4 _BounceColor;
            half _BounceStrength;

            half _MainLightInfluence;
            half _ShadowReceive;
            half4 _ShadowReceiveTint;

            half _Vibrance;
            half _Brightness;

            half4 _BaseColor;
            float4 _BaseMap_ST;
        CBUFFER_END

        float2 CandyTransformTex(float2 uv)
        {
            return uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
        }

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        void ClipAlpha(half alpha)
        {
            #if defined(_ALPHATEST_ON)
                clip(alpha - _Cutoff);
            #endif
        }

        float2 Rotate2D(float2 v, float degreesValue)
        {
            float rad = degreesValue * 0.0174532924;
            float s, c;
            sincos(rad, s, c);
            return float2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        half3 CandyRgbToHsv(half3 c)
        {
            half4 K = half4(0.0h, -1.0h / 3.0h, 2.0h / 3.0h, -1.0h);
            half4 p = lerp(half4(c.bg, K.wz), half4(c.gb, K.xy), step(c.b, c.g));
            half4 q = lerp(half4(p.xyw, c.r), half4(c.r, p.yzx), step(p.x, c.r));
            half d = q.x - min(q.w, q.y);
            half e = 1.0e-4h;
            return half3(abs(q.z + (q.w - q.y) / (6.0h * d + e)), d / (q.x + e), q.x);
        }

        half3 CandyHsvToRgb(half3 c)
        {
            half4 K = half4(1.0h, 2.0h / 3.0h, 1.0h / 3.0h, 3.0h);
            half3 p = abs(frac(c.xxx + K.xyz) * 6.0h - K.www);
            return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
        }

        half3 ShiftTone(half3 baseRGB, half hueShift, half satMul, half valMul)
        {
            half3 hsv = CandyRgbToHsv(saturate(baseRGB));
            hsv.x = frac(hsv.x + hueShift);
            hsv.y = saturate(hsv.y * satMul);
            hsv.z = hsv.z * valMul;
            return CandyHsvToRgb(hsv);
        }

        half3 ApplySaturation(half3 c, half amount)
        {
            return lerp(Luminance(c).xxx, c, amount);
        }

        half PaintedBlob(float2 nxy, float2 centre, float2 size, half angleDeg,
              half softness, half wobble, bool allowWobble)
        {
            float2 d = Rotate2D(nxy - centre, angleDeg);
            d /= max(size, float2(1e-3, 1e-3));

            float r = length(d);

            if (allowWobble && wobble > 1e-4)
            {
                float a = atan2(d.y, d.x);
                r *= 1.0 + sin(a * 3.0 + 1.7) * wobble;
            }

            return 1.0h - smoothstep(1.0h - softness, 1.0h, (half)r);
        }
        ENDHLSL

        // =============================================================================
        //  PASS 1 - Unlit candy shading
        // =============================================================================
        Pass
        {
            Name "CandyUnlit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vertCandy
            #pragma fragment fragCandy

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _VERTEXCOLOR_ON
            #pragma shader_feature_local _MOBILE_SIMPLE

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 color : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
                float fogCoord : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 CandyNormalOS(float3 positionOS, float3 normalOS)
            {
                float3 radial = positionOS - _RoundPivot.xyz;
                float len = length(radial);
                radial = (len > 1e-4) ? radial / len : normalize(normalOS);
                return normalize(lerp(normalize(normalOS), radial, saturate(_Roundness)));
            }

            Varyings vertCandy(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 candyNormalOS = CandyNormalOS(input.positionOS.xyz, input.normalOS);
                float3 positionOS = input.positionOS.xyz + normalize(input.normalOS) * _Inflate;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(positionOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = TransformObjectToWorldNormal(candyNormalOS);
                output.uv = CandyTransformTex(input.uv);
                output.color = input.color;
                output.shadowCoord = GetShadowCoord(vertexInput);
                output.fogCoord = ComputeFogFactor(vertexInput.positionCS.z);
                return output;
            }

            half4 fragCandy(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 baseColor = _BaseColor;

                #if defined(_VERTEXCOLOR_ON)
                    half4 vertexColor = input.color;
                #else
                half4 vertexColor = half4(1.0h, 1.0h, 1.0h, 1.0h);
                #endif

                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 albedo = baseSample.rgb * baseColor.rgb * vertexColor.rgb;
                half alpha = baseSample.a * baseColor.a;
                ClipAlpha(alpha);

                half3 normalWS = normalize(input.normalWS);
                half3 nVS = normalize(mul((float3x3)UNITY_MATRIX_V, normalWS));

                half3 deepTone = ShiftTone(albedo, _ShadowHueShift, _ShadowSat, _ShadowValue);
                half3 lightTone = ShiftTone(albedo, _LightHueShift, _LightSat, _LightValue);

                half rad = _LightAngle * 0.0174532924h;
                half sa, ca;
                sincos(rad, sa, ca);
                half3 lightDirVS = normalize(half3(ca, sa, _LightDepth));

                half shadowAtten = 1.0h;
                if (_MainLightInfluence > 1e-4h || _ShadowReceive > 1e-4h)
                {
                    Light mainLight = GetMainLight(input.shadowCoord);
                    half3 mainVS = normalize(mul((float3x3)UNITY_MATRIX_V, mainLight.direction));
                    lightDirVS = normalize(lerp(lightDirVS, mainVS, _MainLightInfluence));
                    shadowAtten = lerp(1.0h, mainLight.shadowAttenuation, _ShadowReceive);
                }

                half ndl = dot(nVS, lightDirVS);
                half t = saturate((ndl + _LightWrap) / (1.0h + _LightWrap));

                half toBase = smoothstep(_ShadeLow - _ShadeSoft, _ShadeLow + _ShadeSoft, t);
                half toLight = smoothstep(_ShadeHigh - _ShadeSoft, _ShadeHigh + _ShadeSoft, t);

                half3 col = lerp(deepTone, albedo, toBase);
                col = lerp(col, lightTone, toLight);
                col *= lerp(_ShadowReceiveTint.rgb, half3(1, 1, 1), shadowAtten);

                half facing = saturate(nVS.z);
                half edge = 1.0h - facing;

                half3 edgeTone = ShiftTone(albedo, _InnerEdgeHueShift, _InnerEdgeSat, _InnerEdgeValue);
                half inner = smoothstep(_InnerEdgeWidth, 1.0h, edge) * _InnerEdgeStrength;
                col = lerp(col, edgeTone, inner);

                half sheen = smoothstep(_SheenEdge - _SheenSoft, _SheenEdge + _SheenSoft, t);
                col += _GlossColor.rgb * sheen * _SheenStrength;

                #if defined(_MOBILE_SIMPLE)
                    const bool allowWobble = false;
                #else
                const bool allowWobble = true;
                #endif

                half blob1 = PaintedBlob(nVS.xy, _Gloss1Pos.xy, _Gloss1Size.xy,
                                     _Gloss1Angle, _Gloss1Soft, _Gloss1Wobble, allowWobble);
                col += _GlossColor.rgb * blob1 * _Gloss1Strength;

                #if !defined(_MOBILE_SIMPLE)
                half blob2 = PaintedBlob(nVS.xy, _Gloss2Pos.xy, _Gloss2Size.xy,
   0.0h, _Gloss2Soft, _Gloss2Wobble, true);
                col += _GlossColor.rgb * blob2 * _Gloss2Strength;
                #endif

                half rimFall = pow(edge, _RimPower);
                half upMask = saturate(nVS.y * 0.5h + 0.5h);
                col += _RimTopColor.rgb * rimFall * upMask * _RimTopStrength;
                col += _BounceColor.rgb * rimFall * (1.0h - upMask) * _BounceStrength;

                col = ApplySaturation(col, _Vibrance) * _Brightness;
                col = MixFog(col, input.fogCoord);

                return half4(col, alpha);
            }
            ENDHLSL
        }

        // =============================================================================
        //  PASS 2 - ShadowCaster
        // =============================================================================
        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            ZWrite On
            ZTest LEqual
            Cull [_Cull]
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            float3 _LightDirection;
            float3 _LightPosition;

            struct AttributesSC
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsSC
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            VaryingsSC vertShadow(AttributesSC input)
            {
                VaryingsSC output = (VaryingsSC)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(
                    input.positionOS.xyz + normalize(input.normalOS) * _Inflate);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                output.uv = CandyTransformTex(input.uv);
                return output;
            }

            half4 fragShadow(VaryingsSC input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a
                    * _BaseColor.a;
                ClipAlpha(a);
                return 0;
            }
            ENDHLSL
        }

        // =============================================================================
        //  PASS 3 - DepthOnly
        // =============================================================================
        Pass
        {
            Name "DepthOnly"
            Tags
            {
                "LightMode" = "DepthOnly"
            }

            ZWrite On
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vertDepthOnly
            #pragma fragment fragDepthOnly
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing

            struct AttributesDO
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsDO
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            VaryingsDO vertDepthOnly(AttributesDO input)
            {
                VaryingsDO output = (VaryingsDO)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(
                    input.positionOS.xyz + normalize(input.normalOS) * _Inflate);
                output.uv = CandyTransformTex(input.uv);
                return output;
            }

            half4 fragDepthOnly(VaryingsDO input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a
                    * _BaseColor.a;
                ClipAlpha(a);
                return 0;
            }
            ENDHLSL
        }

        // =============================================================================
        //  PASS 4 - DepthNormals
        // =============================================================================
        Pass
        {
            Name "DepthNormals"
            Tags
            {
                "LightMode" = "DepthNormals"
            }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vertDepthNormals
            #pragma fragment fragDepthNormals
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing

            struct AttributesDN
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsDN
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            VaryingsDN vertDepthNormals(AttributesDN input)
            {
                VaryingsDN output = (VaryingsDN)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 radial = input.positionOS.xyz - _RoundPivot.xyz;
                float len = length(radial);
                radial = (len > 1e-4) ? radial / len : normalize(input.normalOS);
                float3 candyOS = normalize(lerp(normalize(input.normalOS), radial, saturate(_Roundness)));

                output.positionCS = TransformObjectToHClip(
                    input.positionOS.xyz + normalize(input.normalOS) * _Inflate);
                output.normalWS = TransformObjectToWorldNormal(candyOS);
                output.uv = CandyTransformTex(input.uv);
                return output;
            }

            half4 fragDepthNormals(VaryingsDN input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a
                    * _BaseColor.a;
                ClipAlpha(a);
                return half4(normalize(input.normalWS), 0.0h);
            }
            ENDHLSL
        }
    }
}