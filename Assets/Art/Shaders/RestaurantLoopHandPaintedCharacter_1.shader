// =====================================================================================
//  RestaurantLoop / HandPaintedCharacterFood                                    v3
// -------------------------------------------------------------------------------------
//  WHY v1 AND v2 SHOWED NO STROKES
//  -------------------------------
//  _BrushTex defaulted to Unity's built-in "gray" texture - a flat 0.5 everywhere.
//  The brush term was (sample - 0.5), so with no texture assigned that term was
//  identically ZERO and no _BrushStrength value could ever make a stroke appear.
//  This version has no brush texture at all. Every mark is generated procedurally in
//  HLSL, so the effect is visible the moment the material is created.
//
//  TECHNIQUES, AND WHERE THEY COME FROM
//  ------------------------------------
//  1. STROKE-SHAPED NORMAL PERTURBATION.  The single most important one. Gabriel de
//     Laubier's stylized paint shader stores brush strokes in the RG channels of a
//     normal map, and Substance's "ABN Painterly Normals" converts painted albedo
//     strokes into normal detail for the same reason: when strokes live in the NORMAL,
//     the lighting itself carves them out. They catch the highlight, they darken on
//     the shadow side, they survive on flat surfaces. Modulating albedo (what v1/v2
//     did) can only ever tint - it never reads as a raised mark of paint.
//     Here the gradient of the procedural stroke field is taken with three taps and
//     folded into the tangent-space normal (_StrokeBumpStrength).
//
//  2. DOMAIN-WARPED ANISOTROPIC VALUE NOISE.  Plain noise reads as grain. Squashing
//     the sample coordinate across one axis (_StrokeElongation) turns blobs into
//     elongated marks, and warping the coordinate with a second, lower-frequency noise
//     (_StrokeWarp) gives those marks the wandering, uneven edges of real brushwork.
//
//  3. THREE-TONE PAINT RAMP.  De Laubier's breakdown describes a stepped gradient of
//     discrete values rather than a smooth falloff. PaintRamp() blends shadow -> mid ->
//     light across two explicit thresholds, which is what reads as "someone chose these
//     three colours" instead of "a computer interpolated".
//
//  4. FINAL COLOUR QUANTISATION.  From Maxime Heckel's painterly-shader essay:
//     floor(c * (n-1) + 0.5) / (n-1). Snapping the finished pixel to a limited palette
//     is what makes an image read as painted rather than rendered. Applied at
//     _PaintQuantizeStrength so it can be dialled back.
//
//  5. UV WARP.  The stroke field also nudges the base-map UV (_StrokeUVWarp), so
//     texture boundaries wobble like brushed paint instead of following the mesh
//     exactly.
//
//  6. CANVAS GRAIN.  Heckel's paper-texture overlay, done as one extra noise tap.
//
//  NOT USED, AND WHY: the Kuwahara filter (and its anisotropic / Papari variants) is
//  the textbook painterly algorithm, but it is a multi-pass screen-space post-process
//  needing many taps per pixel. On a mid-range phone at 60fps that is not affordable,
//  and it would also repaint the UI. Everything above is per-material and cheap.
//
//  OUTLINE
//  -------
//  Inverted hull, near-black, with the width jittered by object-space noise so the
//  line thickness wanders the way a pen line does. The seed is object space, so the
//  wobble is welded to the model and never crawls when the camera moves.
//
//  Passes: DropShadow (SRPDefaultUnlit) / ForwardLit (UniversalForward) /
//          Outline (UniversalForwardOnly) / ShadowCaster / DepthOnly / DepthNormals / Meta
//
//  FORWARD ONLY - the outline uses the UniversalForwardOnly tag, so a Deferred
//  renderer would draw the outline INSTEAD of the lit pass. Keep URP on Forward.
//
//  Unity 6.3 / URP 17 / Android + iOS.
// =====================================================================================

Shader "RestaurantLoop/HandPaintedCharacterFood"
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
        _StrokeScale("Stroke Scale", Float) = 26.0
        _StrokeAngle("Stroke Angle (Degrees)", Range(0,180)) = 28
        _StrokeElongation("Stroke Elongation", Range(1,10)) = 3.5
        _StrokeWarp("Stroke Edge Wander", Range(0,2)) = 0.85
        _StrokeStrength("Stroke Albedo Strength", Range(0,1)) = 0.30
        _StrokeBumpStrength("Stroke Relief (Normal)", Range(0,3)) = 1.10
        _StrokeContrast("Stroke Contrast", Range(0.2,4)) = 1.8
        _StrokeUVWarp("Stroke UV Warp", Range(0,0.05)) = 0.006
        _StrokeShadowBoost("Extra Relief In Shadow", Range(0,2)) = 0.8
        [Enum(ObjectSpaceUV,0,WorldTriplanar,1)] _StrokeSpace("Stroke Space", Float) = 0
        _StrokeWorldScale("World Stroke Scale", Float) = 3.0

        [Header(Pigment)]
        _PigmentWarm("Warm Pigment", Color) = (1.00,0.90,0.72,1)
        _PigmentCool("Cool Pigment", Color) = (0.66,0.63,0.88,1)
        _PigmentAmount("Pigment Temperature", Range(0,1)) = 0.7

        [Header(Canvas Grain)]
        _CanvasStrength("Canvas Grain", Range(0,0.5)) = 0.07
        _CanvasScale("Canvas Grain Scale", Float) = 190.0

        [Header(Three Tone Paint Ramp)]
        _ShadowColor("Shadow Color", Color) = (0.40,0.30,0.44,1)
        _MidColor("Mid Tone Color", Color) = (0.80,0.74,0.78,1)
        _LightColorTint("Light Color", Color) = (1.00,0.96,0.86,1)
        _RampShadowEdge("Shadow to Mid Edge", Range(0,1)) = 0.36
        _RampLightEdge("Mid to Light Edge", Range(0,1)) = 0.68
        _RampSoftness("Ramp Edge Softness", Range(0.005,0.5)) = 0.11
        _TerminatorBreak("Terminator Break By Strokes", Range(0,1)) = 0.85

        [Header(Light Response)]
        _ShadowStrength("Realtime Shadow Strength", Range(0,1)) = 0.55
        _LightWrap("Light Wrap", Range(0,1)) = 0.40
        _MinimumBrightness("Minimum Brightness", Range(0,1)) = 0.38
        _ShadowSaturation("Saturation In Shadow", Range(0,2)) = 1.20
        _LightSaturation("Saturation In Light", Range(0,2)) = 1.00

        [Header(Palette Quantisation)]
        _PaintLevels("Palette Levels", Range(2,32)) = 10
        _PaintQuantizeStrength("Quantise Strength", Range(0,1)) = 0.65

        [Header(Ambient Occlusion)]
        _AOMap("AO Map (R Channel)", 2D) = "white" {}
        _AOStrength("AO Strength", Range(0,1)) = 0.55
        _AOColor("AO Tint Color", Color) = (0.36,0.25,0.33,1)

        [Header(Ambient Sky Lighting)]
        _AmbientStrength("Ambient Strength", Range(0,2)) = 1.0
        _AmbientColor("Ambient Tint", Color) = (1,1,1,1)

        [Header(Vertical Painted Gradient)]
        _TopColor("Top Color", Color) = (1.07,1.02,0.92,1)
        _BottomColor("Bottom Color", Color) = (0.84,0.85,0.96,1)
        _GradientStrength("Gradient Strength", Range(0,1)) = 0.15
        _GradientCenter("Gradient Center", Float) = 0.0
        _GradientSize("Gradient Size", Float) = 1.0
        [Enum(ObjectSpace,0,WorldSpace,1)] _GradientSpace("Gradient Space", Float) = 0

        [Header(Broad Painted Highlight)]
        _HighlightColor("Highlight Color", Color) = (1,0.97,0.88,1)
        _HighlightStrength("Highlight Strength", Range(0,1)) = 0.28
        _HighlightSize("Highlight Size", Range(0.01,1)) = 0.35
        _HighlightSoftness("Highlight Softness", Range(0.01,1)) = 0.45

        [Header(Hand Drawn Ink Outline)]
        [Toggle(_OUTLINE_ON)] _OutlineToggle("Enable Outline", Float) = 1
        _OutlineColor("Outline Color", Color) = (0.05,0.04,0.06,1)
        _OutlineWidth("Outline Width (Pixels)", Range(0,10)) = 2.2
        _OutlineReferenceHeight("Reference Screen Height", Float) = 1080
        _OutlineJitter("Line Width Wobble", Range(0,1)) = 0.55
        _OutlineJitterScale("Wobble Frequency", Float) = 9.0
        _OutlineBreakup("Line Breakup (sketchy gaps)", Range(0,0.6)) = 0.0
        _OutlineInkVariation("Ink Density Variation", Range(0,1)) = 0.35
        _OutlineLightTaper("Thinner In Light", Range(0,1)) = 0.35
        _OutlineNormalBlend("Normal Blend (Mesh to Radial)", Range(0,1)) = 0.0

        [Header(Painted 2D Drop Shadow)]
        [Toggle(_DROPSHADOW_ON)] _DropShadowToggle("Enable 2D Drop Shadow", Float) = 1
        _DropShadowColor("Drop Shadow Color", Color) = (0.16,0.11,0.18,1)
        _DropShadowStrength("Drop Shadow Strength", Range(0,1)) = 0.30
        _DropShadowOffset("Offset (NDC XY)", Vector) = (0.009,-0.011,0,0)
        _DropShadowSpread("Spread (World Units)", Range(0,0.2)) = 0.010
        _DropShadowSoftness("Edge Softness", Range(0.01,1)) = 0.5
        _DropShadowDepthBias("Depth Bias", Range(0,0.01)) = 0.0006

        [Header(Optional MatCap)]
        [Toggle(_MATCAP_ON)] _MatCapToggle("Enable MatCap", Float) = 0
        _MatCapTex("MatCap Texture", 2D) = "black" {}
        _MatCapColor("MatCap Color", Color) = (1,1,1,1)
        _MatCapStrength("MatCap Strength", Range(0,1)) = 0.18

        [Header(Optional Rim Light)]
        [Toggle(_RIM_ON)] _RimToggle("Enable Rim Light", Float) = 0
        _RimColor("Rim Color", Color) = (1,0.95,0.9,1)
        _RimStrength("Rim Strength", Range(0,1)) = 0.35
        _RimPower("Rim Power", Range(0.1,8)) = 2.5

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
            "Queue"           = "Geometry+10"
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
            half _OutlineToggle;
            half _DropShadowToggle;
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

            half4 _OutlineColor;
            half  _OutlineWidth;
            half  _OutlineReferenceHeight;
            half  _OutlineJitter;
            float _OutlineJitterScale;
            half  _OutlineBreakup;
            half  _OutlineInkVariation;
            half  _OutlineLightTaper;
            half  _OutlineNormalBlend;

            half4  _DropShadowColor;
            half   _DropShadowStrength;
            float4 _DropShadowOffset;
            half   _DropShadowSpread;
            half   _DropShadowSoftness;
            half   _DropShadowDepthBias;

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
        //  PROCEDURAL STROKE FIELD
        // -----------------------------------------------------------------------------
        //  Hash is the sin-free Dave Hoskins construction. sin()-based hashes drift on
        //  mobile GPUs with relaxed trig precision and produce visible banding; this one
        //  is pure multiply/fract and behaves identically everywhere.
        //  These MUST stay float - half precision destroys the hash.
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
            float2 u = f * f * (3.0 - 2.0 * f);      // smoothstep interpolant
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

        // The stroke field itself.
        //   - the coordinate is squashed across one axis, so noise blobs become marks
        //   - a low-frequency noise warps the coordinate, giving the marks the
        //     wandering edges that separate brushwork from grain
        //   - a second octave adds bristle detail (skipped in mobile-simple)
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

        // Value plus its gradient, by forward differences. The gradient is what drives
        // the normal perturbation - the reason strokes read as raised paint.
        float3 StrokeFieldAndGradient(float2 p)
        {
            const float e = 0.28;
            float n0 = StrokeField(p);
            float nx = StrokeField(p + float2(e, 0.0));
            float ny = StrokeField(p + float2(0.0, e));
            return float3(n0, (nx - n0) / e, (ny - n0) / e);
        }

        // Where the strokes live. Object-space UV is the cheap default; the triplanar
        // option pins them to world space for meshes whose UVs are ugly or seamy.
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
                // pick the dominant axis rather than blending three samples - a blend
                // would triple the noise cost for a pattern nobody inspects that closely
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

        // Normalise a tint by its own luminance so multiplying shifts hue, not value.
        half3 HueOnlyTint(half3 tintColor)
        {
            return tintColor / max(Luminance(tintColor), 1e-3h);
        }

        half3 ApplySaturation(half3 c, half amount)
        {
            return lerp(Luminance(c).xxx, c, amount);
        }

        // Heckel's palette snap: floor(c * (n-1) + 0.5) / (n-1)
        half3 QuantizeColor(half3 c, half levels)
        {
            half n = max(levels, 2.0h) - 1.0h;
            return floor(saturate(c) * n + 0.5h) / n;
        }

        // Three-tone painted ramp: two explicit thresholds, not a smooth interpolation.
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
        ENDHLSL

        // =============================================================================
        //  PASS 1 - Painted 2D drop shadow
        //  SRPDefaultUnlit is drawn before UniversalForward, so this lands under the
        //  object. Queue Geometry+10 guarantees props (plain Geometry) are already in
        //  the colour buffer. ZWrite is on because opaques sort front-to-back and a
        //  depth-less blend would be overwritten by farther geometry drawn afterwards.
        // =============================================================================
        Pass
        {
            Name "PaintedDropShadow"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vertDropShadow
            #pragma fragment fragDropShadow

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _DROPSHADOW_ON
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct AttributesDS
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsDS
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float  fogCoord   : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            VaryingsDS vertDropShadow(AttributesDS input)
            {
                VaryingsDS output = (VaryingsDS)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                #if !defined(_DROPSHADOW_ON)
                    // Zero-area triangle: discarded before rasterisation.
                    output.positionCS = float4(0.0, 0.0, 0.0, 1.0);
                    return output;
                #else
                    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                    float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);
                    positionWS += normalWS * _DropShadowSpread;

                    float4 positionCS = TransformWorldToHClip(positionWS);
                    // times w -> constant on screen under both perspective and ortho
                    positionCS.xy += _DropShadowOffset.xy * positionCS.w;

                    #if UNITY_REVERSED_Z
                        positionCS.z -= _DropShadowDepthBias * positionCS.w;
                    #else
                        positionCS.z += _DropShadowDepthBias * positionCS.w;
                    #endif

                    output.positionCS = positionCS;
                    output.positionWS = positionWS;
                    output.normalWS   = normalWS;
                    output.uv         = TRANSFORM_TEX(input.uv, _BaseMap);
                    output.fogCoord   = ComputeFogFactor(positionCS.z);
                    return output;
                #endif
            }

            half4 fragDropShadow(VaryingsDS input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                #if !defined(_DROPSHADOW_ON)
                    return half4(0,0,0,0);
                #else
                    half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
                    ClipAlpha(a);

                    half3 n = normalize(input.normalWS);
                    half3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                    half facing = saturate(dot(n, v));
                    half feather = smoothstep(0.0h, _DropShadowSoftness, facing);

                    half3 rgb = MixFog(_DropShadowColor.rgb, input.fogCoord);
                    return half4(rgb, saturate(_DropShadowColor.a * _DropShadowStrength * feather));
                #endif
            }
            ENDHLSL
        }

        // =============================================================================
        //  PASS 2 - Painted forward lighting
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

                // Tangent frame is needed for BOTH the normal map and the stroke relief.
                float3x3 tangentToWorld = CreateTangentToWorld(
                    geomNormal, normalize(input.tangentWS.xyz), input.tangentWS.w);

                // ---------------------------------------------------------------------
                //  1. Stroke field + gradient
                // ---------------------------------------------------------------------
                float2 strokeCoord = GetStrokeCoord(input.uv, positionWS, geomNormal);
                float3 strokeVG    = StrokeFieldAndGradient(strokeCoord);
                half   strokeValue = (half)strokeVG.x;
                half   strokeSigned = (strokeValue - 0.5h) * _StrokeContrast;

                // Gradient is expressed in stroke space; rotate it back so it lines up
                // with the tangent frame before it becomes a normal.
                float2 gradUV = Rotate2D(strokeVG.yz, -_StrokeAngle);

                // ---------------------------------------------------------------------
                //  2. Albedo, with the stroke field warping the UV so painted edges
                //     wobble instead of tracking the mesh exactly.
                // ---------------------------------------------------------------------
                float2 warpedUV = input.uv + gradUV * _StrokeUVWarp;
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, warpedUV);
                half3 albedo = baseSample.rgb * _BaseColor.rgb * input.color.rgb;
                half  alpha  = baseSample.a * _BaseColor.a;
                ClipAlpha(alpha);

                // ---------------------------------------------------------------------
                //  3. Normal. Stroke relief first, optional normal map on top.
                //     THIS is what makes strokes read as paint rather than as tint.
                // ---------------------------------------------------------------------
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

                // ---------------------------------------------------------------------
                //  4. Wrapped diffuse, terminator broken by the strokes, three-tone ramp
                // ---------------------------------------------------------------------
                Light mainLight = GetMainLight(input.shadowCoord);

                half NdotL   = dot(normalWS, mainLight.direction);
                half wrapped = saturate((NdotL + _LightWrap) / (1.0h + _LightWrap));

                // Pushing the ramp thresholds around with the stroke field is what makes
                // every value border wobble like a painted edge.
                wrapped = saturate(wrapped + strokeSigned * _TerminatorBreak * 0.18h);

                half shadowAtten = lerp(1.0h - _ShadowStrength, 1.0h, mainLight.shadowAttenuation);
                half lightMask = max(wrapped * shadowAtten, _MinimumBrightness);

                half3 rampColor = PaintRamp(lightMask) * mainLight.color;

                // ---------------------------------------------------------------------
                //  5. Pigment. Value shift plus a hue-only temperature shift, stronger
                //     on the shadow side the way a painter leaves visible marks there.
                // ---------------------------------------------------------------------
                half shadowSide = 1.0h - lightMask;
                half pigAmount = saturate(_StrokeStrength * input.color.a
                                          * (1.0h + _StrokeShadowBoost * shadowSide));

                half3 pigment = lerp(HueOnlyTint(_PigmentCool.rgb),
                                     HueOnlyTint(_PigmentWarm.rgb),
                                     saturate(strokeSigned * 0.5h + 0.5h));

                albedo *= clamp(1.0h + strokeSigned * pigAmount, 0.0h, 2.0h);
                albedo  = lerp(albedo, albedo * pigment, pigAmount * _PigmentAmount);

                // ---------------------------------------------------------------------
                //  6. Canvas grain - one extra noise tap, Heckel's paper overlay.
                // ---------------------------------------------------------------------
                #if !defined(_MOBILE_SIMPLE)
                    float canvas = ValueNoise(input.uv * _CanvasScale);
                    albedo *= 1.0h + ((half)canvas - 0.5h) * _CanvasStrength;
                #endif

                // ---------------------------------------------------------------------
                //  7. Vertical painted gradient
                // ---------------------------------------------------------------------
                half posY = (_GradientSpace < 0.5h) ? input.positionOS.y : positionWS.y;
                half gT = saturate((posY - _GradientCenter) / max(_GradientSize, 1e-4h) * 0.5h + 0.5h);
                albedo = lerp(albedo, albedo * lerp(_BottomColor.rgb, _TopColor.rgb, gT), _GradientStrength);

                // ---------------------------------------------------------------------
                //  8. Compose
                // ---------------------------------------------------------------------
                half3 aoResult = SampleAOResult(input.uv);
                half3 ambient  = SampleSH(normalWS) * _AmbientColor.rgb * _AmbientStrength;
                half3 total    = rampColor * lerp(1.0h, aoResult, 0.5h) + ambient * aoResult;

                half3 finalColor = albedo * total;

                finalColor = ApplySaturation(finalColor, lerp(_ShadowSaturation, _LightSaturation, lightMask));

                // Broad painted highlight, broken by the stroke field.
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

                // ---------------------------------------------------------------------
                //  9. Palette snap. Last colour operation before fog - this is what
                //     tips the image from "rendered" to "painted".
                // ---------------------------------------------------------------------
                finalColor = lerp(finalColor, QuantizeColor(finalColor, _PaintLevels), _PaintQuantizeStrength);

                finalColor = MixFog(finalColor, input.fogCoord);
                return half4(finalColor, alpha);
            }
            ENDHLSL
        }

        // =============================================================================
        //  PASS 3 - Hand-drawn ink outline
        // -----------------------------------------------------------------------------
        //  Inverted hull expanded in CLIP space and measured in PIXELS, so the line
        //  keeps one weight regardless of distance - correct under the orthographic
        //  2.5D camera, where world-space extrusion would look wrong.
        //
        //  The wobble seed is OBJECT SPACE, so the uneven thickness is welded to the
        //  model and never crawls or shimmers when the camera moves.
        // =============================================================================
        Pass
        {
            Name "PaintedOutline"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vertOutline
            #pragma fragment fragOutline

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _OUTLINE_ON
            #pragma shader_feature_local _MOBILE_SIMPLE
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct AttributesOL
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsOL
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float2 inkCoord   : TEXCOORD1;
                float  fogCoord   : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            VaryingsOL vertOutline(AttributesOL input)
            {
                VaryingsOL output = (VaryingsOL)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                #if !defined(_OUTLINE_ON)
                    output.positionCS = float4(0.0, 0.0, 0.0, 1.0);
                    return output;
                #else
                    float2 uv = TRANSFORM_TEX(input.uv, _BaseMap);

                    // Expansion direction. Blending toward a radial object-space
                    // direction closes the gaps hard-edged meshes would otherwise show,
                    // with no baked smooth normals needed.
                    float3 meshN = normalize(input.normalOS);
                    float3 radial = input.positionOS.xyz;
                    float  rLen = length(radial);
                    radial = (rLen > 1e-4) ? radial / rLen : meshN;
                    float3 expandOS = normalize(lerp(meshN, radial, saturate(_OutlineNormalBlend)));

                    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                    float3 expandWS   = TransformObjectToWorldNormal(expandOS);

                    float4 positionCS = TransformWorldToHClip(positionWS);
                    float3 expandCS   = mul((float3x3)UNITY_MATRIX_VP, expandWS);

                    float2 dir = expandCS.xy;
                    float  len = length(dir);
                    dir = (len > 1e-5) ? dir / len : float2(0.0, 0.0);

                    // Pixel width, scaled to the device so the line does not thin out
                    // on a high-DPI screen.
                    float widthPx = _OutlineWidth * (_ScreenParams.y / max(_OutlineReferenceHeight, 1.0));

                    // Hand-drawn thickness wobble, seeded in object space.
                    float3 seedOS = input.positionOS.xyz * _OutlineJitterScale;
                    float jitter = ValueNoise(seedOS.xy + seedOS.z * 3.71);
                    widthPx *= lerp(1.0, 0.35 + jitter * 1.45, saturate(_OutlineJitter));

                    // Ink weight from light: heavier on the shadow side, like a pen
                    // pressed harder where the form turns away.
                    Light vl = GetMainLight();
                    float ndl = saturate(dot(normalize(TransformObjectToWorldNormal(meshN)), vl.direction));
                    widthPx *= lerp(1.0, lerp(1.30, 0.72, ndl), saturate(_OutlineLightTaper));

                    widthPx = max(widthPx, 0.0);
                    positionCS.xy += dir * widthPx * 2.0 / _ScreenParams.xy * positionCS.w;

                    output.positionCS = positionCS;
                    output.uv         = uv;
                    output.inkCoord   = seedOS.xy * 0.6 + seedOS.z * 1.9;
                    output.fogCoord   = ComputeFogFactor(positionCS.z);
                    return output;
                #endif
            }

            half4 fragOutline(VaryingsOL input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                #if !defined(_OUTLINE_ON)
                    return half4(0,0,0,0);
                #else
                    half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
                    ClipAlpha(a);

                    half3 ink = _OutlineColor.rgb;

                    #if !defined(_MOBILE_SIMPLE)
                        float inkNoise = ValueNoise(input.inkCoord);
                        // Optional sketchy gaps - a pen that skips on the paper.
                        clip(inkNoise - _OutlineBreakup);
                        // Ink density variation, so the line is not a uniform slab.
                        ink *= lerp(1.0h, 0.55h + (half)inkNoise * 0.9h, _OutlineInkVariation);
                    #endif

                    ink = MixFog(ink, input.fogCoord);
                    return half4(ink, 1.0h);
                #endif
            }
            ENDHLSL
        }

        // =============================================================================
        //  PASS 4 - ShadowCaster
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
        //  PASS 5 - DepthOnly
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
        //  PASS 6 - DepthNormals
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
        //  PASS 7 - Meta
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
