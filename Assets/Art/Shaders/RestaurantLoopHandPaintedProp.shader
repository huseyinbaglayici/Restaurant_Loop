// =====================================================================================
//  RestaurantLoop / HandPaintedProp                                             v3
// -------------------------------------------------------------------------------------
//  Background, environment and secondary props. Counters, walls, floors, shelves,
//  crates, decoration - everything the player is not meant to focus on.
//
//  Same painting engine as HandPaintedCharacterFood, so a shelf behind a character
//  reads as the same illustration:
//    - procedural domain-warped anisotropic stroke field (no texture needed)
//    - stroke-shaped normal perturbation, so marks read as raised paint
//    - three-tone paint ramp
//    - final palette quantisation
//    - canvas grain
//
//  What is deliberately removed, because it is what says "look at me":
//    - no ink outline pass
//    - no 2D drop shadow pass
//    - ONE draw call instead of three
//    - lower stroke relief and softer ramp edges so props sit behind the heroes
//    - Queue = Geometry, BELOW the hero shader's Geometry+10, which is what lets the
//      heroes' drop shadows blend correctly over these surfaces
//
//  Extras the hero shader does not have:
//    _MacroDriftStrength   very low-frequency world-space colour drift, kills tiling
//                          repetition on large flat walls and floors
//    _DepthFadeStrength    painted distance haze so far scenery recedes and foreground
//                          food stays readable. Off by default.
//
//  Stroke space defaults to WorldTriplanar here (heroes default to UV): environment
//  meshes are usually tiled or share UV space, and world-pinned strokes hide that.
//
//  Unity 6.3 / URP 17 / Forward / Android + iOS.
// =====================================================================================

Shader "RestaurantLoop/HandPaintedProp"
{
    Properties
    {
        [Header(Surface)]
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5
        [Toggle(_ALPHATEST_ON)] _AlphaClipToggle("Enable Alpha Clipping", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Float) = 2

        [Header(Brush Strokes    procedural   no texture needed)]
        _StrokeScale("Stroke Scale", Float) = 22.0
        _StrokeAngle("Stroke Angle (Degrees)", Range(0,180)) = 16
        _StrokeElongation("Stroke Elongation", Range(1,10)) = 3.0
        _StrokeWarp("Stroke Edge Wander", Range(0,2)) = 0.75
        _StrokeStrength("Stroke Albedo Strength", Range(0,1)) = 0.22
        _StrokeBumpStrength("Stroke Relief (Normal)", Range(0,3)) = 0.75
        _StrokeContrast("Stroke Contrast", Range(0.2,4)) = 1.5
        _StrokeUVWarp("Stroke UV Warp", Range(0,0.05)) = 0.004
        _StrokeShadowBoost("Extra Relief In Shadow", Range(0,2)) = 0.6
        [Enum(ObjectSpaceUV,0,WorldTriplanar,1)] _StrokeSpace("Stroke Space", Float) = 1
        _StrokeWorldScale("World Stroke Scale", Float) = 3.0

        [Header(Pigment)]
        _PigmentWarm("Warm Pigment", Color) = (1.00,0.91,0.74,1)
        _PigmentCool("Cool Pigment", Color) = (0.68,0.65,0.89,1)
        _PigmentAmount("Pigment Temperature", Range(0,1)) = 0.65

        [Header(Canvas Grain)]
        _CanvasStrength("Canvas Grain", Range(0,0.5)) = 0.05
        _CanvasScale("Canvas Grain Scale", Float) = 160.0

        [Header(Large Surface Breakup)]
        _MacroDriftColor("Macro Drift Color", Color) = (0.93,0.96,1.06,1)
        _MacroDriftStrength("Macro Drift Strength", Range(0,1)) = 0.14
        _MacroDriftScale("Macro Drift Scale (World)", Float) = 0.30

        [Header(Three Tone Paint Ramp)]
        _ShadowColor("Shadow Color", Color) = (0.40,0.30,0.44,1)
        _MidColor("Mid Tone Color", Color) = (0.80,0.74,0.78,1)
        _LightColorTint("Light Color", Color) = (1.00,0.96,0.86,1)
        _RampShadowEdge("Shadow to Mid Edge", Range(0,1)) = 0.34
        _RampLightEdge("Mid to Light Edge", Range(0,1)) = 0.70
        _RampSoftness("Ramp Edge Softness", Range(0.005,0.5)) = 0.18
        _TerminatorBreak("Terminator Break By Strokes", Range(0,1)) = 0.7

        [Header(Light Response)]
        _ShadowStrength("Realtime Shadow Strength", Range(0,1)) = 0.5
        _LightWrap("Light Wrap", Range(0,1)) = 0.40
        _MinimumBrightness("Minimum Brightness", Range(0,1)) = 0.40
        _ShadowSaturation("Saturation In Shadow", Range(0,2)) = 1.12
        _LightSaturation("Saturation In Light", Range(0,2)) = 1.00

        [Header(Palette Quantisation)]
        _PaintLevels("Palette Levels", Range(2,32)) = 12
        _PaintQuantizeStrength("Quantise Strength", Range(0,1)) = 0.55

        [Header(Ambient Occlusion)]
        _AOMap("AO Map (R Channel)", 2D) = "white" {}
        _AOStrength("AO Strength", Range(0,1)) = 0.6
        _AOColor("AO Tint Color", Color) = (0.36,0.25,0.33,1)

        [Header(Ambient Sky Lighting)]
        _AmbientStrength("Ambient Strength", Range(0,2)) = 1.0
        _AmbientColor("Ambient Tint", Color) = (1,1,1,1)

        [Header(Vertical Painted Gradient)]
        _TopColor("Top Color", Color) = (1.07,1.02,0.92,1)
        _BottomColor("Bottom Color", Color) = (0.84,0.85,0.96,1)
        _GradientStrength("Gradient Strength", Range(0,1)) = 0.2
        _GradientCenter("Gradient Center", Float) = 0.0
        _GradientSize("Gradient Size", Float) = 2.0
        [Enum(ObjectSpace,0,WorldSpace,1)] _GradientSpace("Gradient Space", Float) = 1

        [Header(Broad Painted Highlight)]
        _HighlightColor("Highlight Color", Color) = (1,0.97,0.88,1)
        _HighlightStrength("Highlight Strength", Range(0,1)) = 0.12
        _HighlightSize("Highlight Size", Range(0.01,1)) = 0.3
        _HighlightSoftness("Highlight Softness", Range(0.01,1)) = 0.5

        [Header(Distance Haze for Far Scenery)]
        _DepthFadeColor("Haze Color", Color) = (0.86,0.87,0.96,1)
        _DepthFadeStrength("Haze Strength", Range(0,1)) = 0.0
        _DepthFadeStart("Haze Start Distance", Float) = 12.0
        _DepthFadeRange("Haze Fade Range", Float) = 20.0

        [Header(Optional MatCap)]
        [Toggle(_MATCAP_ON)] _MatCapToggle("Enable MatCap", Float) = 0
        _MatCapTex("MatCap Texture", 2D) = "black" {}
        _MatCapColor("MatCap Color", Color) = (1,1,1,1)
        _MatCapStrength("MatCap Strength", Range(0,1)) = 0.12

        [Header(Optional Rim Light)]
        [Toggle(_RIM_ON)] _RimToggle("Enable Rim Light", Float) = 0
        _RimColor("Rim Color", Color) = (1,0.95,0.9,1)
        _RimStrength("Rim Strength", Range(0,1)) = 0.2
        _RimPower("Rim Power", Range(0.1,8)) = 3.0

        [Header(Optional Normal Map)]
        [Toggle(_NORMALMAP)] _NormalMapToggle("Enable Normal Map", Float) = 0
        _NormalMap("Normal Map", 2D) = "bump" {}
        _NormalStrength("Normal Strength", Range(0,2)) = 0.5

        [Header(Mobile Performance)]
        [Toggle(_MOBILE_SIMPLE)] _MobileSimpleToggle("Mobile Simple Mode", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Opaque"
            "RenderPipeline"  = "UniversalPipeline"
            "Queue"           = "Geometry"
            "IgnoreProjector" = "True"
        }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _BaseColor;
            half   _Cutoff;

            half _Cull;
            half _AlphaClipToggle;
            half _MatCapToggle;
            half _RimToggle;
            half _NormalMapToggle;
            half _MobileSimpleToggle;

            float _StrokeScale;
            float _StrokeAngle;
            float _StrokeElongation;
            float _StrokeWarp;
            half  _StrokeStrength;
            half  _StrokeBumpStrength;
            half  _StrokeContrast;
            half  _StrokeUVWarp;
            half  _StrokeShadowBoost;
            half  _StrokeSpace;
            float _StrokeWorldScale;

            half4 _PigmentWarm;
            half4 _PigmentCool;
            half  _PigmentAmount;

            half  _CanvasStrength;
            float _CanvasScale;

            half4 _MacroDriftColor;
            half  _MacroDriftStrength;
            float _MacroDriftScale;

            half4 _ShadowColor;
            half4 _MidColor;
            half4 _LightColorTint;
            half  _RampShadowEdge;
            half  _RampLightEdge;
            half  _RampSoftness;
            half  _TerminatorBreak;

            half _ShadowStrength;
            half _LightWrap;
            half _MinimumBrightness;
            half _ShadowSaturation;
            half _LightSaturation;

            half _PaintLevels;
            half _PaintQuantizeStrength;

            half  _AOStrength;
            half4 _AOColor;

            half  _AmbientStrength;
            half4 _AmbientColor;

            half4 _TopColor;
            half4 _BottomColor;
            half  _GradientStrength;
            half  _GradientCenter;
            half  _GradientSize;
            half  _GradientSpace;

            half4 _HighlightColor;
            half  _HighlightStrength;
            half  _HighlightSize;
            half  _HighlightSoftness;

            half4 _DepthFadeColor;
            half  _DepthFadeStrength;
            half  _DepthFadeStart;
            half  _DepthFadeRange;

            half4 _MatCapColor;
            half  _MatCapStrength;

            half4 _RimColor;
            half  _RimStrength;
            half  _RimPower;

            half _NormalStrength;
        CBUFFER_END

        TEXTURE2D(_BaseMap);   SAMPLER(sampler_BaseMap);
        TEXTURE2D(_AOMap);     SAMPLER(sampler_AOMap);
        TEXTURE2D(_MatCapTex); SAMPLER(sampler_MatCapTex);
        TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);

        // =============================================================================
        //  PROCEDURAL STROKE FIELD - identical maths to the hero shader so the two
        //  stay visually matched. Duplicated rather than shared through an .hlsl
        //  include so each file stays a self-contained drop-in.
        //  These MUST stay float; half precision destroys the hash.
        // =============================================================================

        float Hash21(float2 p)
        {
            float3 p3 = frac(p.xyx * 0.1031);
            p3 += dot(p3, p3.yzx + 33.33);
            return frac((p3.x + p3.y) * p3.z);
        }

        float ValueNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            float2 u = f * f * (3.0 - 2.0 * f);
            float a = Hash21(i);
            float b = Hash21(i + float2(1.0, 0.0));
            float c = Hash21(i + float2(0.0, 1.0));
            float d = Hash21(i + float2(1.0, 1.0));
            return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
        }

        float2 Rotate2D(float2 v, float degreesValue)
        {
            float rad = degreesValue * 0.0174532924;
            float s, c;
            sincos(rad, s, c);
            return float2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        float StrokeField(float2 p)
        {
            float2 q = float2(p.x / max(_StrokeElongation, 1.0), p.y);

            float w = ValueNoise(q * 0.55 + 17.3);
            q += (w - 0.5) * _StrokeWarp;

            float n = ValueNoise(q);
            #if !defined(_MOBILE_SIMPLE)
                n = n * 0.68 + ValueNoise(q * 2.17 + 4.7) * 0.32;
            #endif
            return saturate(n);
        }

        float3 StrokeFieldAndGradient(float2 p)
        {
            const float e = 0.28;
            float n0 = StrokeField(p);
            float nx = StrokeField(p + float2(e, 0.0));
            float ny = StrokeField(p + float2(0.0, e));
            return float3(n0, (nx - n0) / e, (ny - n0) / e);
        }

        float2 GetStrokeCoord(float2 uv, float3 positionWS, float3 normalWS)
        {
            if (_StrokeSpace < 0.5h)
            {
                return Rotate2D(uv - 0.5, _StrokeAngle) * _StrokeScale;
            }
            else
            {
                float3 an = abs(normalWS);
                float3 p = positionWS * _StrokeWorldScale;
                float2 planar = (an.x > an.y && an.x > an.z) ? p.zy
                              : ((an.y > an.z) ? p.xz : p.xy);
                return Rotate2D(planar, _StrokeAngle) * (_StrokeScale * 0.06);
            }
        }

        // =============================================================================
        //  Colour helpers
        // =============================================================================

        void ClipAlpha(half alpha)
        {
            #if defined(_ALPHATEST_ON)
                clip(alpha - _Cutoff);
            #endif
        }

        half3 HueOnlyTint(half3 tintColor)
        {
            return tintColor / max(Luminance(tintColor), 1e-3h);
        }

        half3 ApplySaturation(half3 c, half amount)
        {
            return lerp(Luminance(c).xxx, c, amount);
        }

        half3 QuantizeColor(half3 c, half levels)
        {
            half n = max(levels, 2.0h) - 1.0h;
            return floor(saturate(c) * n + 0.5h) / n;
        }

        half3 PaintRamp(half t)
        {
            half toMid   = smoothstep(_RampShadowEdge - _RampSoftness,
                                      _RampShadowEdge + _RampSoftness, t);
            half toLight = smoothstep(_RampLightEdge  - _RampSoftness,
                                      _RampLightEdge  + _RampSoftness, t);
            half3 c = lerp(_ShadowColor.rgb, _MidColor.rgb, toMid);
            return lerp(c, _LightColorTint.rgb, toLight);
        }

        half3 SampleAOResult(float2 uv)
        {
            half rawAO = SAMPLE_TEXTURE2D(_AOMap, sampler_AOMap, uv).r;
            half ao = max(lerp(1.0h, rawAO, _AOStrength), 0.15h);
            half3 aoTint = lerp(half3(1,1,1), _AOColor.rgb, 1.0h - ao);
            return aoTint * ao;
        }

        // Three cheap sine waves instead of another noise octave. Completely static -
        // there is no _Time anywhere in this shader.
        half MacroDrift(float3 positionWS)
        {
            float3 p = positionWS * _MacroDriftScale;
            float wave = sin(p.x * 1.00 + 0.7) + sin(p.y * 0.73 + 2.1) + sin(p.z * 1.31 + 4.3);
            return saturate((half)(wave * 0.1667 + 0.5));
        }
        ENDHLSL

        // =============================================================================
        //  PASS 1 - Painted forward lighting (the only colour pass)
        // =============================================================================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vertForward
            #pragma fragment fragForward

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _MATCAP_ON
            #pragma shader_feature_local _RIM_ON
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _MOBILE_SIMPLE

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 positionOS  : TEXCOORD2;
                float3 normalWS    : TEXCOORD3;
                float4 tangentWS   : TEXCOORD4;
                float3 viewDirWS   : TEXCOORD5;
                float4 color       : TEXCOORD6;
                float4 shadowCoord : TEXCOORD7;
                float  fogCoord    : TEXCOORD8;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vertForward(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs   normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.positionOS = input.positionOS.xyz;
                output.normalWS   = normalInput.normalWS;

                real tangentSign = input.tangentOS.w * GetOddNegativeScale();
                output.tangentWS = half4(normalInput.tangentWS.xyz, tangentSign);

                output.viewDirWS   = GetWorldSpaceViewDir(vertexInput.positionWS);
                output.uv          = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color       = input.color;
                output.shadowCoord = GetShadowCoord(vertexInput);
                output.fogCoord    = ComputeFogFactor(vertexInput.positionCS.z);
                return output;
            }

            half4 fragForward(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 positionWS = input.positionWS;
                half3  viewDirWS  = normalize(input.viewDirWS);
                half3  geomNormal = normalize(input.normalWS);

                float3x3 tangentToWorld = CreateTangentToWorld(
                    geomNormal, normalize(input.tangentWS.xyz), input.tangentWS.w);

                // 1. Stroke field + gradient
                float2 strokeCoord = GetStrokeCoord(input.uv, positionWS, geomNormal);
                float3 strokeVG    = StrokeFieldAndGradient(strokeCoord);
                half   strokeValue = (half)strokeVG.x;
                half   strokeSigned = (strokeValue - 0.5h) * _StrokeContrast;
                float2 gradUV = Rotate2D(strokeVG.yz, -_StrokeAngle);

                // 2. Albedo with painted UV wobble
                float2 warpedUV = input.uv + gradUV * _StrokeUVWarp;
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, warpedUV);
                half3 albedo = baseSample.rgb * _BaseColor.rgb * input.color.rgb;
                half  alpha  = baseSample.a * _BaseColor.a;
                ClipAlpha(alpha);

                // 3. Normal: stroke relief, then optional normal map
                half3 normalWS = geomNormal;

                #if !defined(_MOBILE_SIMPLE)
                    half bump = _StrokeBumpStrength * input.color.a;
                    half3 strokeTS = normalize(half3(-gradUV.x * bump, -gradUV.y * bump, 1.0h));
                    normalWS = normalize(TransformTangentToWorld(strokeTS, tangentToWorld));
                #endif

                #if defined(_NORMALMAP)
                    half3 mapTS = UnpackNormalScale(
                        SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, warpedUV), _NormalStrength);
                    half3 mapWS = normalize(TransformTangentToWorld(mapTS, tangentToWorld));
                    normalWS = normalize(normalWS + mapWS - geomNormal);
                #endif

                // 4. Wrapped diffuse -> stroke-broken terminator -> three-tone ramp
                Light mainLight = GetMainLight(input.shadowCoord);

                half NdotL   = dot(normalWS, mainLight.direction);
                half wrapped = saturate((NdotL + _LightWrap) / (1.0h + _LightWrap));
                wrapped = saturate(wrapped + strokeSigned * _TerminatorBreak * 0.18h);

                half shadowAtten = lerp(1.0h - _ShadowStrength, 1.0h, mainLight.shadowAttenuation);
                half lightMask = max(wrapped * shadowAtten, _MinimumBrightness);

                half3 rampColor = PaintRamp(lightMask) * mainLight.color;

                // 5. Pigment
                half shadowSide = 1.0h - lightMask;
                half pigAmount = saturate(_StrokeStrength * input.color.a
                                          * (1.0h + _StrokeShadowBoost * shadowSide));

                half3 pigment = lerp(HueOnlyTint(_PigmentCool.rgb),
                                     HueOnlyTint(_PigmentWarm.rgb),
                                     saturate(strokeSigned * 0.5h + 0.5h));

                albedo *= clamp(1.0h + strokeSigned * pigAmount, 0.0h, 2.0h);
                albedo  = lerp(albedo, albedo * pigment, pigAmount * _PigmentAmount);

                // 6. Canvas grain
                #if !defined(_MOBILE_SIMPLE)
                    float canvas = ValueNoise(input.uv * _CanvasScale);
                    albedo *= 1.0h + ((half)canvas - 0.5h) * _CanvasStrength;
                #endif

                // 7. Macro drift - kills tiling repetition on large flat surfaces
                half drift = MacroDrift(positionWS);
                albedo = lerp(albedo,
                              albedo * lerp(half3(1,1,1), HueOnlyTint(_MacroDriftColor.rgb), drift),
                              _MacroDriftStrength);

                // 8. Vertical painted gradient
                half posY = (_GradientSpace < 0.5h) ? input.positionOS.y : positionWS.y;
                half gT = saturate((posY - _GradientCenter) / max(_GradientSize, 1e-4h) * 0.5h + 0.5h);
                albedo = lerp(albedo, albedo * lerp(_BottomColor.rgb, _TopColor.rgb, gT), _GradientStrength);

                // 9. Compose
                half3 aoResult = SampleAOResult(input.uv);
                half3 ambient  = SampleSH(normalWS) * _AmbientColor.rgb * _AmbientStrength;
                half3 total    = rampColor * lerp(1.0h, aoResult, 0.5h) + ambient * aoResult;

                half3 finalColor = albedo * total;
                finalColor = ApplySaturation(finalColor, lerp(_ShadowSaturation, _LightSaturation, lightMask));

                half3 halfDir = normalize(mainLight.direction + viewDirWS);
                half  NdotH = saturate(dot(normalWS, halfDir));
                half  e0 = saturate(1.0h - _HighlightSize - _HighlightSoftness);
                half  e1 = saturate(1.0h - _HighlightSize + _HighlightSoftness);
                half  hl = smoothstep(e0, max(e1, e0 + 1e-4h), NdotH);
                hl *= saturate(NdotL) * mainLight.shadowAttenuation * saturate(0.3h + strokeValue);
                finalColor += _HighlightColor.rgb * hl * _HighlightStrength;

                #if defined(_MATCAP_ON) && !defined(_MOBILE_SIMPLE)
                    half3 nVS = mul((float3x3)UNITY_MATRIX_V, normalWS);
                    half3 mc = SAMPLE_TEXTURE2D(_MatCapTex, sampler_MatCapTex, nVS.xy * 0.5h + 0.5h).rgb;
                    finalColor += mc * _MatCapColor.rgb * _MatCapStrength;
                #endif

                #if defined(_RIM_ON) && !defined(_MOBILE_SIMPLE)
                    half rim = pow(1.0h - saturate(dot(normalWS, viewDirWS)), _RimPower);
                    finalColor += _RimColor.rgb * rim * _RimStrength;
                #endif

                // 10. Painted distance haze (artistic depth cue, on top of URP fog)
                half viewDistance = length(GetCurrentViewPosition() - positionWS);
                half hazeT = saturate((viewDistance - _DepthFadeStart) / max(_DepthFadeRange, 1e-4h));
                finalColor = lerp(finalColor, _DepthFadeColor.rgb, hazeT * _DepthFadeStrength);

                // 11. Palette snap - last colour operation before fog
                finalColor = lerp(finalColor, QuantizeColor(finalColor, _PaintLevels), _PaintQuantizeStrength);

                finalColor = MixFog(finalColor, input.fogCoord);
                return half4(finalColor, alpha);
            }
            ENDHLSL
        }

        // =============================================================================
        //  PASS 2 - ShadowCaster
        // =============================================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

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
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsSC
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 GetShadowPositionHClip(AttributesSC input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);

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
                return positionCS;
            }

            VaryingsSC vertShadow(AttributesSC input)
            {
                VaryingsSC output = (VaryingsSC)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = GetShadowPositionHClip(input);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 fragShadow(VaryingsSC input) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
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
            Tags { "LightMode" = "DepthOnly" }

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
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsDO
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            VaryingsDO vertDepthOnly(AttributesDO input)
            {
                VaryingsDO output = (VaryingsDO)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 fragDepthOnly(VaryingsDO input) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
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
            Tags { "LightMode" = "DepthNormals" }

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
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsDN
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            VaryingsDN vertDepthNormals(AttributesDN input)
            {
                VaryingsDN output = (VaryingsDN)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                output.uv         = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 fragDepthNormals(VaryingsDN input) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
                ClipAlpha(a);
                return half4(normalize(input.normalWS), 0.0h);
            }
            ENDHLSL
        }

        // =============================================================================
        //  PASS 5 - Meta
        // =============================================================================
        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }

            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vertMeta
            #pragma fragment fragMeta
            #pragma shader_feature_local _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityMetaPass.hlsl"

            struct AttributesMeta
            {
                float4 positionOS : POSITION;
                float2 uv0        : TEXCOORD0;
                float2 uv1        : TEXCOORD1;
                float2 uv2        : TEXCOORD2;
            };

            struct VaryingsMeta
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            VaryingsMeta vertMeta(AttributesMeta input)
            {
                VaryingsMeta output = (VaryingsMeta)0;
                output.positionCS = MetaVertexPosition(input.positionOS, input.uv1, input.uv2,
                                                       unity_LightmapST, unity_DynamicLightmapST);
                output.uv = TRANSFORM_TEX(input.uv0, _BaseMap);
                return output;
            }

            half4 fragMeta(VaryingsMeta input) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half a = baseSample.a * _BaseColor.a;
                ClipAlpha(a);

                MetaInput metaInput = (MetaInput)0;
                metaInput.Albedo   = baseSample.rgb * _BaseColor.rgb;
                metaInput.Emission = half3(0,0,0);
                return MetaFragment(metaInput);
            }
            ENDHLSL
        }
    }
}
