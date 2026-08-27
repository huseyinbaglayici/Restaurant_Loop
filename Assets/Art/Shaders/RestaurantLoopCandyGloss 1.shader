// =====================================================================================
//  RestaurantLoop / CandyGloss
// -------------------------------------------------------------------------------------
//  Royal Match / Royal Kingdom style candy shading. A hard reset from the hand-painted
//  brush-stroke set: no strokes, no canvas grain, no palette quantisation, nothing that
//  pulls saturation down.
//
//  HOW THIS LOOK ACTUALLY WORKS
//  ----------------------------
//  1. IT IS UNLIT, AND THAT IS THE POINT. The shading is driven by the VIEW-SPACE
//     normal, so the light always arrives from the top of the screen no matter where
//     the camera or the scene lights are. That is what makes every piece in a match-3
//     board read identically. _MainLightInfluence can blend the scene's directional
//     light back in, but 0 (fully unlit) is the default because it is what keeps a
//     conveyor full of props visually consistent.
//
//  2. COLOURS ARE DERIVED IN HSV, NEVER MULTIPLIED BY GREY. The artist sets ONE vivid
//     base colour. The shadow tone is that colour with its hue nudged toward magenta,
//     its saturation RAISED and its value lowered; the lit tone is the same hue nudged
//     toward yellow with saturation slightly dropped and value raised. Because all
//     three stops come from the base hue, nothing can go grey - which is exactly what
//     was wrong with multiplying a painted ramp into albedo.
//
//  3. THE GLOSS IS A PAINTED SHAPE, NOT A BRDF. Two elliptical blobs are placed at
//     fixed positions in view-space-normal space - the big one high on the upper left,
//     a small companion further out. Their radius is modulated by a slow sine of the
//     polar angle, so the edge wobbles slightly and reads as painted rather than
//     vector-perfect. This is the "white reflection stuck on the top corner" look.
//     A broad soft sheen sits underneath them for the glassy body.
//
//  4. PUFFINESS COMES FROM THE NORMAL, NOT THE MESH. _Roundness blends the mesh normal
//     toward the direction from a pivot out to the vertex, so a blocky low-poly prop
//     shades like an inflated sphere without being remodelled. _Inflate additionally
//     pushes vertices out along their normals for real silhouette puff.
//
//  5. INNER EDGE DARKENING is what makes candy read as thick and rounded: a soft band
//     of deeper, MORE saturated colour just inside the silhouette, plus a bright rim on
//     the top edge and a coloured bounce along the bottom.
//
//  Passes: DropShadow (SRPDefaultUnlit) / Unlit (UniversalForward) /
//          Outline (UniversalForwardOnly) / ShadowCaster / DepthOnly / DepthNormals
//
//  No Meta pass: the shader is unlit, so it has no meaningful albedo to contribute to a
//  lightmap bake. Add one only if these props ever need to bounce light onto lightmapped
//  geometry.
//
//  FORWARD ONLY - the outline uses the UniversalForwardOnly tag, so under a Deferred
//  renderer URP would draw the outline INSTEAD of the lit pass. Keep URP on Forward.
//
//  ONE SHADER FOR EVERYTHING. Heroes and background props share it; the difference is
//  which toggles are on and the material's Render Queue:
//      background props : Outline off, Drop Shadow off, Render Queue 2000
//      heroes / food    : Outline optional, Drop Shadow on, Render Queue 2010 (default)
//  The queue split is what lets a hero's drop shadow blend over props that are already
//  in the colour buffer.
//
//  Unity 6.3 / URP 17 / Forward / Android + iOS.
// =====================================================================================

Shader "RestaurantLoop/CandyGloss"
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

        [Header(Candy Form    puffiness)]
        _Roundness("Roundness (normal to radial)", Range(0,1)) = 0.55
        _RoundPivot("Round Pivot (Object Space)", Vector) = (0,0,0,0)
        _Inflate("Inflate (World Units)", Range(-0.2,0.2)) = 0.0

        [Header(Candy Shading    unlit)]
        _LightAngle("Light Angle (Screen Degrees)", Range(0,360)) = 125
        _LightDepth("Light Toward Camera", Range(0,1)) = 0.35
        _LightWrap("Light Wrap", Range(0,1)) = 0.22
        _ShadeLow("Shadow to Base Edge", Range(0,1)) = 0.30
        _ShadeHigh("Base to Light Edge", Range(0,1)) = 0.84
        _ShadeSoft("Ramp Softness", Range(0.01,0.6)) = 0.17

        [Header(Shadow Tone    derived from base hue)]
        _ShadowHueShift("Shadow Hue Shift", Range(-0.2,0.2)) = 0.045
        _ShadowSat("Shadow Saturation x", Range(0,2)) = 1.42
        _ShadowValue("Shadow Value x", Range(0,1)) = 0.62

        [Header(Light Tone    derived from base hue)]
        _LightHueShift("Light Hue Shift", Range(-0.2,0.2)) = -0.035
        _LightSat("Light Saturation x", Range(0,2)) = 0.95
        _LightValue("Light Value x", Range(1,3)) = 1.14

        [Header(Painted Gloss    main blob)]
        _GlossColor("Gloss Color", Color) = (1,1,1,1)
        _Gloss1Strength("Main Blob Strength", Range(0,2)) = 0.95
        _Gloss1Pos("Main Blob Position (XY)", Vector) = (-0.42,0.52,0,0)
        _Gloss1Size("Main Blob Size (XY)", Vector) = (0.30,0.20,0,0)
        _Gloss1Angle("Main Blob Angle", Range(0,180)) = 35
        _Gloss1Soft("Main Blob Softness", Range(0.01,1)) = 0.35
        _Gloss1Wobble("Main Blob Hand Wobble", Range(0,0.5)) = 0.16

        [Header(Painted Gloss    companion blob)]
        _Gloss2Strength("Small Blob Strength", Range(0,2)) = 0.7
        _Gloss2Pos("Small Blob Position (XY)", Vector) = (-0.66,0.16,0,0)
        _Gloss2Size("Small Blob Size (XY)", Vector) = (0.11,0.14,0,0)
        _Gloss2Soft("Small Blob Softness", Range(0.01,1)) = 0.45
        _Gloss2Wobble("Small Blob Hand Wobble", Range(0,0.5)) = 0.12

        [Header(Painted Gloss    broad sheen)]
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

        [Header(Ink Outline    off by default)]
        [Toggle(_OUTLINE_ON)] _OutlineToggle("Enable Outline", Float) = 0
        _OutlineColor("Outline Color", Color) = (0.10,0.06,0.12,1)
        _OutlineWidth("Outline Width (Pixels)", Range(0,10)) = 2.0
        _OutlineReferenceHeight("Reference Screen Height", Float) = 1080

        [Header(2D Drop Shadow)]
        [Toggle(_DROPSHADOW_ON)] _DropShadowToggle("Enable Drop Shadow", Float) = 1
        _DropShadowColor("Drop Shadow Color", Color) = (0.16,0.10,0.22,1)
        _DropShadowStrength("Drop Shadow Strength", Range(0,1)) = 0.32
        _DropShadowOffset("Offset (NDC XY)", Vector) = (0.010,-0.013,0,0)
        _DropShadowSpread("Spread (World Units)", Range(0,0.2)) = 0.012
        _DropShadowSoftness("Edge Softness", Range(0.01,1)) = 0.55
        _DropShadowDepthBias("Depth Bias", Range(0,0.01)) = 0.0006

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
            half _VertexColorToggle;
            half _OutlineToggle;
            half _DropShadowToggle;
            half _MobileSimpleToggle;

            half   _Roundness;
            float4 _RoundPivot;
            half   _Inflate;

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
            half  _Gloss1Strength;
            float4 _Gloss1Pos;
            float4 _Gloss1Size;
            half  _Gloss1Angle;
            half  _Gloss1Soft;
            half  _Gloss1Wobble;

            half  _Gloss2Strength;
            float4 _Gloss2Pos;
            float4 _Gloss2Size;
            half  _Gloss2Soft;
            half  _Gloss2Wobble;

            half _SheenStrength;
            half _SheenEdge;
            half _SheenSoft;

            half _InnerEdgeStrength;
            half _InnerEdgeWidth;
            half _InnerEdgeHueShift;
            half _InnerEdgeSat;
            half _InnerEdgeValue;

            half4 _RimTopColor;
            half  _RimTopStrength;
            half  _RimPower;

            half4 _BounceColor;
            half  _BounceStrength;

            half  _MainLightInfluence;
            half  _ShadowReceive;
            half4 _ShadowReceiveTint;

            half _Vibrance;
            half _Brightness;

            half4 _OutlineColor;
            half  _OutlineWidth;
            half  _OutlineReferenceHeight;

            half4  _DropShadowColor;
            half   _DropShadowStrength;
            float4 _DropShadowOffset;
            half   _DropShadowSpread;
            half   _DropShadowSoftness;
            half   _DropShadowDepthBias;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

        // =============================================================================
        //  Helpers
        // =============================================================================

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

        // Sam Hocevar's branchless RGB<->HSV. Every tone in this shader is derived from
        // the base hue through these, which is why nothing can drift toward grey.
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

        // Derive a related tone: rotate the hue, scale saturation, scale value.
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

        // An elliptical, slightly irregular painted blob in view-space-normal space.
        // nVS.xy spans roughly -1..1 across the visible hemisphere, so this behaves like
        // a matcap coordinate: the blob stays pinned to the same screen corner of every
        // object regardless of how the object is turned.
        half PaintedBlob(float2 nxy, float2 centre, float2 size, half angleDeg,
                         half softness, half wobble, bool allowWobble)
        {
            float2 d = Rotate2D(nxy - centre, angleDeg);
            d /= max(size, float2(1e-3, 1e-3));

            float r = length(d);

            if (allowWobble && wobble > 1e-4)
            {
                // A slow sine of the polar angle dents the rim, so the highlight reads
                // as brushed on rather than as a perfect vector ellipse.
                float a = atan2(d.y, d.x);
                r *= 1.0 + sin(a * 3.0 + 1.7) * wobble;
            }

            return 1.0h - smoothstep(1.0h - softness, 1.0h, (half)r);
        }
        ENDHLSL

        // =============================================================================
        //  PASS 1 - 2D drop shadow
        //  SRPDefaultUnlit draws before UniversalForward, so this lands under the piece.
        //  ZWrite is on because opaques sort front-to-back and a depth-less blend would
        //  be overwritten by farther geometry drawn afterwards.
        // =============================================================================
        Pass
        {
            Name "CandyDropShadow"
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
                    output.positionCS = float4(0.0, 0.0, 0.0, 1.0);
                    return output;
                #else
                    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                    float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);
                    positionWS += normalWS * (_DropShadowSpread + _Inflate);

                    float4 positionCS = TransformWorldToHClip(positionWS);
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
                    half feather = smoothstep(0.0h, _DropShadowSoftness, saturate(dot(n, v)));

                    half3 rgb = MixFog(_DropShadowColor.rgb, input.fogCoord);
                    return half4(rgb, saturate(_DropShadowColor.a * _DropShadowStrength * feather));
                #endif
            }
            ENDHLSL
        }

        // =============================================================================
        //  PASS 2 - Unlit candy shading
        // =============================================================================
        Pass
        {
            Name "CandyUnlit"
            Tags { "LightMode" = "UniversalForward" }

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
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float4 color       : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
                float  fogCoord    : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Blend the mesh normal toward the direction from the pivot out to the
            // vertex. At 1.0 a cube shades exactly like a sphere - this is what turns a
            // blocky prop into a puffed candy without touching the mesh.
            float3 CandyNormalOS(float3 positionOS, float3 normalOS)
            {
                float3 radial = positionOS - _RoundPivot.xyz;
                float  len = length(radial);
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

                // Real silhouette puff, on top of the shading puff.
                float3 positionOS = input.positionOS.xyz + normalize(input.normalOS) * _Inflate;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(positionOS);

                output.positionCS  = vertexInput.positionCS;
                output.positionWS  = vertexInput.positionWS;
                output.normalWS    = TransformObjectToWorldNormal(candyNormalOS);
                output.uv          = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color       = input.color;
                output.shadowCoord = GetShadowCoord(vertexInput);
                output.fogCoord    = ComputeFogFactor(vertexInput.positionCS.z);
                return output;
            }

            half4 fragCandy(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                #if defined(_VERTEXCOLOR_ON)
                    half4 vertexColor = input.color;
                #else
                    half4 vertexColor = half4(1.0h, 1.0h, 1.0h, 1.0h);
                #endif

                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 albedo = baseSample.rgb * _BaseColor.rgb * vertexColor.rgb;
                half  alpha  = baseSample.a * _BaseColor.a;
                ClipAlpha(alpha);

                half3 normalWS = normalize(input.normalWS);

                // ---------------------------------------------------------------------
                //  View-space normal. Everything below is expressed in it, so the whole
                //  look is pinned to the screen: the highlight sits on the same corner
                //  of every piece no matter how the object is rotated.
                // ---------------------------------------------------------------------
                half3 nVS = normalize(mul((float3x3)UNITY_MATRIX_V, normalWS));

                // ---------------------------------------------------------------------
                //  1. The candy ramp. Three stops, all derived from the base hue in HSV
                //     so no stage can wash the colour out.
                // ---------------------------------------------------------------------
                half3 deepTone  = ShiftTone(albedo, _ShadowHueShift, _ShadowSat, _ShadowValue);
                half3 lightTone = ShiftTone(albedo, _LightHueShift,  _LightSat,  _LightValue);

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

                half toBase  = smoothstep(_ShadeLow  - _ShadeSoft, _ShadeLow  + _ShadeSoft, t);
                half toLight = smoothstep(_ShadeHigh - _ShadeSoft, _ShadeHigh + _ShadeSoft, t);

                half3 col = lerp(deepTone, albedo, toBase);
                col = lerp(col, lightTone, toLight);

                // Received shadows tint rather than darken, so pieces never go muddy.
                col *= lerp(_ShadowReceiveTint.rgb, half3(1,1,1), shadowAtten);

                // ---------------------------------------------------------------------
                //  2. Inner edge. A band of deeper, MORE saturated colour just inside
                //     the silhouette. This is the single biggest "thick and rounded"
                //     cue in the Royal Match style.
                // ---------------------------------------------------------------------
                half facing = saturate(nVS.z);
                half edge   = 1.0h - facing;

                half3 edgeTone = ShiftTone(albedo, _InnerEdgeHueShift, _InnerEdgeSat, _InnerEdgeValue);
                half inner = smoothstep(_InnerEdgeWidth, 1.0h, edge) * _InnerEdgeStrength;
                col = lerp(col, edgeTone, inner);

                // ---------------------------------------------------------------------
                //  3. Painted gloss. Blobs first, broad sheen underneath.
                // ---------------------------------------------------------------------
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

                // ---------------------------------------------------------------------
                //  4. Rim and bounce. Top edge catches the key light, bottom edge picks
                //     up a cool bounce - the pair is what sells the rounded volume.
                // ---------------------------------------------------------------------
                half rimFall = pow(edge, _RimPower);
                half upMask   = saturate(nVS.y * 0.5h + 0.5h);
                col += _RimTopColor.rgb * rimFall * upMask * _RimTopStrength;
                col += _BounceColor.rgb  * rimFall * (1.0h - upMask) * _BounceStrength;

                // ---------------------------------------------------------------------
                //  5. Finish
                // ---------------------------------------------------------------------
                col = ApplySaturation(col, _Vibrance) * _Brightness;
                col = MixFog(col, input.fogCoord);

                return half4(col, alpha);
            }
            ENDHLSL
        }

        // =============================================================================
        //  PASS 3 - Optional ink outline (inverted hull, screen-constant pixel width)
        // =============================================================================
        Pass
        {
            Name "CandyOutline"
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
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct AttributesOL
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsOL
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float  fogCoord   : TEXCOORD1;
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
                    // Expand along the ROUNDED normal so the outline follows the puffed
                    // silhouette the shading implies, and closes gaps on hard edges.
                    float3 radial = input.positionOS.xyz - _RoundPivot.xyz;
                    float  len = length(radial);
                    radial = (len > 1e-4) ? radial / len : normalize(input.normalOS);
                    float3 expandOS = normalize(lerp(normalize(input.normalOS), radial,
                                                     saturate(max(_Roundness, 0.35))));

                    float3 positionWS = TransformObjectToWorld(
                        input.positionOS.xyz + normalize(input.normalOS) * _Inflate);
                    float3 expandWS = TransformObjectToWorldNormal(expandOS);

                    float4 positionCS = TransformWorldToHClip(positionWS);
                    float3 expandCS   = mul((float3x3)UNITY_MATRIX_VP, expandWS);

                    float2 dir = expandCS.xy;
                    float  l = length(dir);
                    dir = (l > 1e-5) ? dir / l : float2(0.0, 0.0);

                    float widthPx = _OutlineWidth * (_ScreenParams.y / max(_OutlineReferenceHeight, 1.0));
                    positionCS.xy += dir * widthPx * 2.0 / _ScreenParams.xy * positionCS.w;

                    output.positionCS = positionCS;
                    output.uv         = TRANSFORM_TEX(input.uv, _BaseMap);
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
                    return half4(MixFog(_OutlineColor.rgb, input.fogCoord), 1.0h);
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
                float3 normalOS   : NORMAL;
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
                output.positionCS = TransformObjectToHClip(
                    input.positionOS.xyz + normalize(input.normalOS) * _Inflate);
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

                float3 radial = input.positionOS.xyz - _RoundPivot.xyz;
                float  len = length(radial);
                radial = (len > 1e-4) ? radial / len : normalize(input.normalOS);
                float3 candyOS = normalize(lerp(normalize(input.normalOS), radial, saturate(_Roundness)));

                output.positionCS = TransformObjectToHClip(
                    input.positionOS.xyz + normalize(input.normalOS) * _Inflate);
                output.normalWS = TransformObjectToWorldNormal(candyOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
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
    }
}
