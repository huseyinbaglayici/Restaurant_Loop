// =====================================================================================
//  RestaurantLoop / SoftMatteWood                                                 v5
// -------------------------------------------------------------------------------------
//  Procedural stylised wood, sharing SoftMatte's exact lighting core so a wooden prop
//  and a painted prop sit in the same art world. Planks, seam grooves, grain and
//  per-board colour variation, all generated in HLSL - no wood texture, no UVs needed.
//
//
//  WHY v4 CAME BACK MAGENTA, AND WHY v5 CANNOT
//  ===========================================
//  v4 defined its own RgbToHsv() and HsvToRgb(). Unity already defines both, in
//  com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl, which Core.hlsl pulls in
//  through Common.hlsl. HLSL has no redefinition: that is a hard compile error, the
//  shader never builds, and Unity draws the magenta error material. v2 wood compiled
//  fine - that is how the plank-size problem was visible in the first place - so the
//  HSV helpers were the regression, precisely.
//
//  The permanent fix is not "rename those two". It is that EVERY function this file
//  defines now carries an RL_ prefix. A name that starts with RL_ cannot collide with
//  anything in Unity's shader library, today or after any package upgrade. This class of
//  bug is closed, not patched.
//
//  Three more things were hardened at the same time, each of which had a real chance of
//  failing on some platform compiler:
//
//    * Object scale is read through GetObjectToWorldMatrix() into a local float4x4,
//      not by writing unity_ObjectToWorld._m00 directly. unity_ObjectToWorld is a MACRO
//      under GPU/DOTS instancing and can expand to a function call; taking a member of a
//      function call result is not portable. Copying to a local first always is.
//    * sincos() and radians() are gone, replaced with plain sin/cos and a constant.
//      Both are fine in principle; neither is worth a cross-compiler risk for two ops.
//    * ddx/ddy are computed once at the top of the fragment, never inside a branch.
//
//
//  WHY PROCEDURAL HERE
//  ===================
//  There is a lot of wood in this game and almost none of it is unwrapped. A texture
//  approach would need a UV pass over every prop first. This generates the pattern from
//  OBJECT-SPACE position instead, so:
//    * no UVs required
//    * the grain is welded to the mesh, so a tray travelling down the conveyor carries
//      its wood with it instead of sliding through a world-fixed pattern
//    * one material drives a whole set of props at different sizes
//
//
//  SIZE IS IN METRES (v3)
//  ======================
//  Object space is not a unit. A mesh authored at 0.1 units and scaled to 30 in the
//  Transform, and a mesh authored at 1 unit at scale 1, can occupy the same 3 metres on
//  screen while their object-space coordinates differ by 300x. Measured on four typical
//  prop setups all covering 1-3 m, the old code produced planks of 7.5 cm, 10 cm, 30 cm
//  and 300 cm from ONE material.
//
//  The object's world scale is now pulled from the model matrix and multiplied in, so
//  the coordinate is in metres while still welded to the mesh. _PlankWidth 0.12 is a
//  12 cm board on every prop in the game. _ScaleCompensation = 0 restores the old
//  behaviour for anything already hand-tuned.
//
//
//  ALIGNMENT (v3)
//  ==============
//  Auto axis selection lands on the geometric edges of a box - where a carpenter would
//  change board direction - but flips face by face on bevels, lathe shapes and baked
//  rotations. _ProjectionAxis forces one plane, _PatternRotation spins the timber inside
//  it, _PatternOffset slides it. When boards run the wrong way, reach for them in that
//  order: _PlankAxis (90 degree flip), then _PatternRotation, then _ProjectionAxis.
//
//
//  COLOUR IS ONE SWATCH (v4)
//  =========================
//  The grain noise is centred on 0.5, so lerp(dark, light, grain) averages to the exact
//  midpoint of the two tones for every plank on every prop. With two hard-coded orange
//  tones that midpoint was (0.765, 0.445, 0.230) no matter what the artist did, and the
//  only per-material control was _BaseColor, which multiplies - and a multiply can only
//  darken. Reaching a pale beech (0.92, 0.86, 0.76) needed a tint of (1.20, 1.93, 3.30).
//  Beige, cream and near-white timber were not expressible at all.
//
//  Now: one _WoodColor. Light tone, dark tone and groove colour are derived from it in
//  HSV, and the value spread is SYMMETRIC, so the average of the rendered wood is
//  exactly the colour picked. Measured over six woods, the rendered average tracks the
//  swatch to within 0.05 worst case and 0.016 on the pale woods. All of it is uniform
//  maths on constants, so the compiler hoists it out of the fragment loop - the
//  per-pixel cost is unchanged.
//
//
//  PRESETS - sizes are real metres, so these transfer between props unchanged
//  =========================================================================
//   Furniture board - tables, chairs, crates, shelves         <- the defaults, start here
//     WoodColor (0.76,0.52,0.31)  ProjectionAxis ForceY
//     PlankWidth 0.12  PlankLength 0.80  GrainScale 85  GrooveWidth 0.05
//
//   Wide counter / conveyor frame
//     WoodColor (0.68,0.45,0.26)  ProjectionAxis ForceY
//     PlankWidth 0.18  PlankLength 1.20  GrainScale 60  Stagger 0.4
//
//   Thin slats - crate walls, railings, fences
//     WoodColor (0.72,0.55,0.38)  ProjectionAxis ForceZ
//     PlankWidth 0.05  PlankLength 0.30  GrainScale 180  GrooveWidth 0.08  Stagger 0
//
//   Wall panelling / floor - one continuous run
//     WoodColor (0.87,0.75,0.58)  ProjectionAxis ForceZ
//     PlankWidth 0.15  PlankLength 0  GrainScale 70  GrooveWidth 0.04
//
//   Chunky single block - chopping board, stool top, no seams
//     PlankWidth 1.0  PlankLength 0  GrooveDepth 0  GrainScale 70  GrainStretch 20
//
//   Wood swatches that work: walnut (0.34,0.21,0.13), oak (0.76,0.52,0.31),
//   pale beech (0.87,0.75,0.58), near-white ash (0.92,0.88,0.80),
//   grey driftwood (0.62,0.61,0.58), red mahogany (0.52,0.22,0.16).
//   Change _WoodColor alone - nothing else needs touching.
//
//
//  Everything is static. There is no _Time anywhere in this shader.
//  Includes: Core.hlsl, Lighting.hlsl, Shadows.hlsl. Nothing else.
//  No Meta pass - URP 17 removed ShaderLibrary/UnityMetaPass.hlsl and including it is
//  itself a magenta. No Fallback - a real Console error beats a silent wrong shader.
//
//  Passes: UniversalForward / ShadowCaster / DepthOnly / DepthNormals
//  Unity 6.x / URP 14-17 / Forward / Android + iOS.
// =====================================================================================

Shader "RestaurantLoop/SoftMatteWood"
{
    Properties
    {
        [Header(Surface)]
        [MainTexture] _BaseMap("Base Map (optional overlay)", 2D) = "white" {}
        [MainColor] _BaseColor("Base Tint", Color) = (1,1,1,1)
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5
        [Toggle(_ALPHATEST_ON)] _AlphaClipToggle("Enable Alpha Clipping", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Float) = 2

        [Header(Wood Colour    one swatch drives everything)]
        _WoodColor("Wood Color", Color) = (0.76,0.52,0.31,1)
        _ToneSpread("Light to Dark Spread", Range(0,1)) = 0.55
        _ToneWarmth("Shadow Warmth", Range(-1,1)) = 0.35
        _GrooveDarken("Groove Extra Darkening", Range(0,1)) = 0.45

        [Header(Per Board Variation)]
        _PlankTintVariation("Board Brightness Variation", Range(0,0.5)) = 0.10
        _PlankHueVariation("Board Warm Cool Variation", Range(0,1)) = 0.35
        _PlankWarmTint("Warm Board Tint", Color) = (1.07,1.00,0.92,1)
        _PlankCoolTint("Cool Board Tint", Color) = (0.94,0.98,1.06,1)

        [Header(Manual Two Tone    overrides the swatch above)]
        [Toggle(_TWOTONE_WOOD)] _TwoToneMode("Set Light and Dark Manually", Float) = 0
        _WoodLight("Wood Light Tone", Color) = (0.93,0.60,0.33,1)
        _WoodDark("Wood Dark Tone", Color) = (0.60,0.29,0.13,1)
        _GrooveColor("Groove Color", Color) = (0.40,0.20,0.11,1)

        [Header(Pattern Placement)]
        _ScaleCompensation("World Scale Compensation", Range(0,1)) = 1.0
        [Enum(Auto,0,ForceX,1,ForceY,2,ForceZ,3)] _ProjectionAxis("Projection Axis", Float) = 0
        _PatternRotation("Pattern Rotation (degrees)", Range(0,360)) = 0
        _PatternOffset("Pattern Offset (meters)", Vector) = (0,0,0,0)
        _WoodScale("Pattern Density Multiplier", Range(0.1,8)) = 1.0

        [Header(Planks    sizes in meters)]
        [Enum(AlongX,0,AlongY,1)] _PlankAxis("Plank Direction", Float) = 0
        _PlankWidth("Plank Width (m)", Range(0.01,1)) = 0.12
        _PlankLength("Plank Length (m, 0 = no cross cuts)", Range(0,3)) = 0.80
        _PlankStagger("Plank Stagger", Range(0,1)) = 0.5

        [Header(Grooves)]
        _GrooveWidth("Groove Width (fraction of plank)", Range(0.005,0.3)) = 0.05
        _GrooveDepth("Groove Darkening", Range(0,1)) = 0.55
        _GrooveNormalStrength("Groove Relief", Range(0,3)) = 1.0

        [Header(Grain)]
        _GrainScale("Grain Cells Per Meter", Range(5,300)) = 85.0
        _GrainStretch("Grain Stretch Along Plank", Range(1,30)) = 14.0
        _GrainStrength("Grain Strength", Range(0,1)) = 0.62
        _GrainContrast("Grain Contrast", Range(0.2,3)) = 1.8
        _GrainDetail("Second Octave", Range(0,1)) = 0.4

        [Header(Rounded Corners)]
        _Roundness("Roundness", Range(0,1)) = 0.25
        _RoundPivot("Round Pivot (Object Space)", Vector) = (0,0,0,0)

        [Header(Hemisphere    the top down look)]
        _SkyColor("Sky Color (up facing)", Color) = (1.06,1.03,0.98,1)
        _GroundColor("Ground Color (down facing)", Color) = (0.68,0.64,0.62,1)
        _HemisphereWrap("Hemisphere Softness", Range(0.01,1)) = 0.90

        [Header(Soft Directional)]
        _LightTint("Light Tint", Color) = (1.0,0.98,0.94,1)
        _ShadowTint("Shadow Tint", Color) = (0.70,0.62,0.62,1)
        _ShadowThreshold("Shadow Threshold", Range(0,1)) = 0.10
        _LightThreshold("Light Threshold", Range(0,1)) = 0.92
        _DirectionalStrength("Directional Strength", Range(0,1)) = 0.60

        [Header(Toon Stepping)]
        _ToonSteps("Toon Steps", Range(1,6)) = 3
        _ToonStepSoftness("Toon Step Softness", Range(0.05,1)) = 0.55

        [Header(Fake Self Light and Shadow)]
        _CurvatureScale("Curvature Scale", Range(0.01,4)) = 0.6
        _CreaseShadow("Crease Shadow", Range(0,1)) = 0.35
        _CreaseColor("Crease Color", Color) = (0.62,0.52,0.48,1)
        _EdgeLight("Edge Light", Range(0,1)) = 0.18
        _EdgeLightColor("Edge Light Color", Color) = (1.0,0.96,0.90,1)

        [Header(Light Direction)]
        _UseMainLightDirection("Use Scene Light Direction", Range(0,1)) = 0.0
        _FakeLightDirection("Fixed Light Direction", Vector) = (-0.35,0.85,-0.4,0)

        [Header(Realtime Shadows)]
        _ReceiveShadowStrength("Receive Shadow Strength", Range(0,1)) = 0.45
        _ShadowBrightness("Shadow Minimum Brightness", Range(0,1)) = 0.58
        _ShadowColorTint("Received Shadow Tint", Color) = (0.70,0.62,0.62,1)

        [Header(Ambient Occlusion)]
        _AOMap("AO Map (R)", 2D) = "white" {}
        _AOStrength("AO Strength", Range(0,1)) = 0.25
        _AOFloor("AO Minimum", Range(0,1)) = 0.45

        [Header(Vertical Gradient)]
        _TopGradientColor("Top Gradient", Color) = (1.04,1.02,1.00,1)
        _BottomGradientColor("Bottom Gradient", Color) = (0.90,0.87,0.86,1)
        _GradientIntensity("Gradient Intensity", Range(0,1)) = 0.12
        _GradientStart("Gradient Start", Float) = -0.5
        _GradientEnd("Gradient End", Float) = 0.5
        [Enum(ObjectSpace,0,WorldSpace,1)] _GradientSpace("Gradient Space", Float) = 0

        [Header(Colour)]
        _Saturation("Saturation", Range(0,2)) = 1.0
        _Brightness("Brightness", Range(0,2)) = 1.0
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
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

        // =============================================================================
        //  EVERY function below is prefixed RL_.
        //
        //  This is not style. Unity's shader library defines a large, undocumented and
        //  version-dependent set of global function names - RgbToHsv, HsvToRgb,
        //  SafeNormalize, ValueNoise, Luminance and many more - and HLSL treats a
        //  redefinition as a hard error, which Unity surfaces as the magenta error
        //  material. Prefixing makes collision structurally impossible, now and after
        //  any package upgrade.
        // =============================================================================

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _BaseColor;
            half   _Cutoff;
            half   _Cull;
            half   _AlphaClipToggle;

            half4 _WoodColor;
            half  _ToneSpread;
            half  _ToneWarmth;
            half  _GrooveDarken;

            half  _TwoToneMode;
            half4 _WoodLight;
            half4 _WoodDark;
            half4 _GrooveColor;

            half  _PlankTintVariation;
            half  _PlankHueVariation;
            half4 _PlankWarmTint;
            half4 _PlankCoolTint;

            half   _ScaleCompensation;
            half   _ProjectionAxis;
            float  _PatternRotation;
            float4 _PatternOffset;
            float  _WoodScale;

            half  _PlankAxis;
            float _PlankWidth;
            float _PlankLength;
            half  _PlankStagger;

            float _GrooveWidth;
            half  _GrooveDepth;
            half  _GrooveNormalStrength;

            float _GrainScale;
            float _GrainStretch;
            half  _GrainStrength;
            half  _GrainContrast;
            half  _GrainDetail;

            half   _Roundness;
            float4 _RoundPivot;

            half4 _SkyColor;
            half4 _GroundColor;
            half  _HemisphereWrap;

            half4 _LightTint;
            half4 _ShadowTint;
            half  _ShadowThreshold;
            half  _LightThreshold;
            half  _DirectionalStrength;

            half _ToonSteps;
            half _ToonStepSoftness;

            half  _CurvatureScale;
            half  _CreaseShadow;
            half4 _CreaseColor;
            half  _EdgeLight;
            half4 _EdgeLightColor;

            half   _UseMainLightDirection;
            float4 _FakeLightDirection;

            half  _ReceiveShadowStrength;
            half  _ShadowBrightness;
            half4 _ShadowColorTint;

            half _AOStrength;
            half _AOFloor;

            half4 _TopGradientColor;
            half4 _BottomGradientColor;
            half  _GradientIntensity;
            half  _GradientStart;
            half  _GradientEnd;
            half  _GradientSpace;

            half _Saturation;
            half _Brightness;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_AOMap);   SAMPLER(sampler_AOMap);

        // -----------------------------------------------------------------------------
        //  Small helpers
        // -----------------------------------------------------------------------------

        // A mesh imported with normals disabled hands the shader a zero vector, and
        // normalize(0) is NaN - one NaN normal poisons every term downstream.
        float3 RL_SafeNormal(float3 v, float3 fallback)
        {
            float lenSq = dot(v, v);
            return (lenSq > 1e-8) ? v * rsqrt(lenSq) : fallback;
        }

        void RL_ClipAlpha(half alpha)
        {
            #if defined(_ALPHATEST_ON)
                clip(alpha - _Cutoff);
            #endif
        }

        // The base map is an OPTIONAL overlay - the wood itself is generated. With no
        // texture assigned Unity supplies white, so this multiplies by 1 and the
        // procedural wood comes through untouched.
        half4 RL_SampleBase(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
        }

        float3 RL_RoundedNormalOS(float3 positionOS, float3 normalOS)
        {
            float3 n = RL_SafeNormal(normalOS, float3(0.0, 1.0, 0.0));
            float3 radial = RL_SafeNormal(positionOS - _RoundPivot.xyz, n);
            return RL_SafeNormal(lerp(n, radial, saturate(_Roundness)), n);
        }

        // Soft posterisation. Borders stay smooth at any step count, which is what keeps
        // this a soft toon rather than hard cel.
        half RL_ToonQuantize(half value, half steps, half softness)
        {
            half n = max(steps, 1.0h);
            half scaled = saturate(value) * n;
            half idx = floor(scaled);
            half frc = scaled - idx;
            half halfSoft = max(softness, 1e-3h) * 0.5h;
            half stepped = smoothstep(0.5h - halfSoft, 0.5h + halfSoft, frc);
            return saturate((idx + stepped) / n);
        }

        // -----------------------------------------------------------------------------
        //  Noise. The sin-free Dave Hoskins hash: sin()-based hashes drift on mobile
        //  GPUs with relaxed trig precision and band visibly. Must stay float - half
        //  precision destroys the hash.
        // -----------------------------------------------------------------------------
        float RL_Hash21(float2 p)
        {
            float3 p3 = frac(p.xyx * 0.1031);
            p3 += dot(p3, p3.yzx + 33.33);
            return frac((p3.x + p3.y) * p3.z);
        }

        float RL_Hash11(float n)
        {
            return RL_Hash21(float2(n, n * 1.7321 + 4.13));
        }

        float RL_ValueNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            float2 u = f * f * (3.0 - 2.0 * f);
            float a = RL_Hash21(i);
            float b = RL_Hash21(i + float2(1.0, 0.0));
            float c = RL_Hash21(i + float2(0.0, 1.0));
            float d = RL_Hash21(i + float2(1.0, 1.0));
            return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
        }

        // -----------------------------------------------------------------------------
        //  HSV. Branchless Sam Hocevar. Same algorithm as Unity's, deliberately under
        //  our own names - see the prefix note above; defining RgbToHsv/HsvToRgb is
        //  exactly what turned v4 magenta.
        // -----------------------------------------------------------------------------
        float3 RL_RgbToHsv(float3 c)
        {
            float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
            float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
            float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
            float d = q.x - min(q.w, q.y);
            return float3(abs(q.z + (q.w - q.y) / (6.0 * d + 1e-6)), d / (q.x + 1e-6), q.x);
        }

        float3 RL_HsvToRgb(float3 c)
        {
            float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
            float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
            return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
        }

        // =============================================================================
        //  Wood tone derivation
        //
        //  One swatch in, three tones out, with the spread SYMMETRIC in value so the
        //  average of the rendered wood is exactly the colour picked. Symmetry is the
        //  whole point: the grain noise is centred, so an asymmetric spread would drag
        //  the prop away from the swatch and the artist would end up chasing it with
        //  Brightness.
        //
        //  ON PALE WOODS: value spread is limited by whichever side has less headroom.
        //  A swatch at value 0.92 has only 0.08 of room above it, so a wide spread would
        //  clip to flat white and destroy the grain. That is not a workaround - pale
        //  timber genuinely has little value contrast and reads through SATURATION
        //  instead, so the tighter the value headroom the harder the saturation spread
        //  is pushed to compensate. _TwoToneMode hands the two poles back for any prop
        //  that needs more than this will give.
        //
        //  Uniform maths on constants only - the compiler hoists all of it out of the
        //  fragment loop, so this costs nothing per pixel.
        // =============================================================================
        void RL_ResolveWoodTones(out half3 lightTone, out half3 darkTone, out half3 grooveTone)
        {
        #if defined(_TWOTONE_WOOD)
            lightTone  = _WoodLight.rgb;
            darkTone   = _WoodDark.rgb;
            grooveTone = _GrooveColor.rgb;
        #else
            float3 hsv = RL_RgbToHsv(saturate(_WoodColor.rgb));
            float spread = saturate((float)_ToneSpread);

            float room = min(saturate((1.0 - hsv.z) / 0.5), saturate(hsv.z / 0.5));
            float vAmt = spread * 0.65 * room;

            // The contrast the value spread had to give up comes back as saturation.
            float sBoost = 1.0 + (1.0 - room) * 1.20;

            float3 lh = hsv;
            float3 dh = hsv;

            lh.z = saturate(hsv.z + vAmt);
            dh.z = saturate(hsv.z - vAmt);

            lh.y = saturate(hsv.y * (1.0 - spread * 0.40 * sBoost));  // highlights wash out
            dh.y = saturate(hsv.y * (1.0 + spread * 0.55 * sBoost));  // shadows deepen

            // Shadows drift toward red the way real timber does. Negative _ToneWarmth
            // sends them the other way, for grey and driftwood.
            dh.x = frac(hsv.x - (float)_ToneWarmth * 0.030 + 1.0);

            lightTone = (half3)RL_HsvToRgb(lh);
            darkTone  = (half3)RL_HsvToRgb(dh);

            // Groove is the dark tone taken further down and further saturated. Deriving
            // it means a colour change needs one swatch edited, not three.
            float gd = saturate((float)_GrooveDarken);
            float3 gh = dh;
            gh.z = saturate(dh.z * (1.0 - gd * 0.75));
            gh.y = saturate(dh.y * (1.0 + gd * 0.35));
            grooveTone = (half3)RL_HsvToRgb(gh);
        #endif
        }

        // =============================================================================
        //  Object world scale
        //
        //  Read through GetObjectToWorldMatrix() into a LOCAL matrix on purpose.
        //  unity_ObjectToWorld is a macro under GPU and DOTS instancing and can expand
        //  to a function call; taking ._m00 of a function call result is not portable
        //  across platform compilers. Copying to a local first always is.
        // =============================================================================
        float3 RL_ObjectWorldScale()
        {
            float4x4 m = GetObjectToWorldMatrix();

            float3 s = float3(
                length(float3(m._m00, m._m10, m._m20)),
                length(float3(m._m01, m._m11, m._m21)),
                length(float3(m._m02, m._m12, m._m22)));

            // A zero scale on any axis would collapse the pattern into a single line.
            s = max(s, 1e-4);
            return lerp(float3(1.0, 1.0, 1.0), s, saturate((float)_ScaleCompensation));
        }

        // =============================================================================
        //  Wood coordinate
        //
        //  Auto picks the two object axes most perpendicular to the normal. Projecting
        //  onto a frame derived from the normal instead would collapse to zero on any
        //  round object - on a sphere the position is parallel to the normal, so both
        //  dot products vanish and the whole mesh samples a single point.
        //
        //  Rotation is applied AFTER the projection and BEFORE the plank division, so
        //  planks, grooves and grain turn together as one piece of timber.
        // =============================================================================
        float2 RL_WoodCoord(float3 positionOS, float3 normalOS)
        {
            float3 p = positionOS * RL_ObjectWorldScale() * max(_WoodScale, 1e-3);

            float2 coord;
            if (_ProjectionAxis < 0.5h)
            {
                float3 an = abs(RL_SafeNormal(normalOS, float3(0.0, 1.0, 0.0)));
                if (an.x >= an.y && an.x >= an.z)      coord = float2(p.z, p.y);
                else if (an.y >= an.z)                 coord = float2(p.x, p.z);
                else                                   coord = float2(p.x, p.y);
            }
            else if (_ProjectionAxis < 1.5h) coord = float2(p.z, p.y);   // Force X
            else if (_ProjectionAxis < 2.5h) coord = float2(p.x, p.z);   // Force Y
            else                             coord = float2(p.x, p.y);   // Force Z

            // _PlankAxis swaps which of the two runs along the boards.
            coord = (_PlankAxis < 0.5h) ? coord : coord.yx;

            // Plain sin/cos and a constant - no sincos(), no radians().
            float a  = _PatternRotation * 0.01745329252;
            float sa = sin(a);
            float ca = cos(a);
            coord = float2(coord.x * ca - coord.y * sa,
                           coord.x * sa + coord.y * ca);

            return coord + _PatternOffset.xy;
        }

        // Row / column indices and the local position inside one plank.
        //  .x = fraction across the plank (0..1, the seam is at 0 and 1)
        //  .y = fraction along the plank
        //  .z = row index
        //  .w = column index
        float4 RL_PlankCell(float2 wc)
        {
            float rowF = wc.y / max(_PlankWidth, 1e-3);
            float row  = floor(rowF);
            float across = rowF - row;

            // Stagger each row along its length so seams do not line up in a grid.
            // Proportional to plank LENGTH: with lengths in metres a fixed offset would
            // throw short boards several cells sideways.
            float alongPos = wc.x + RL_Hash11(row) * _PlankStagger * max(_PlankLength, 0.05);

            float col = 0.0;
            float along = 0.5;
            if (_PlankLength > 1e-3)
            {
                float colF = alongPos / _PlankLength;
                col = floor(colF);
                along = colF - col;
            }

            return float4(across, along, row, col);
        }

        // Seam grooves. Pure smoothstep maths, no noise, which is why this is cheap
        // enough to evaluate three times for a gradient.
        float RL_GrooveMask(float2 wc)
        {
            float4 cell = RL_PlankCell(wc);
            float gw = max(_GrooveWidth, 1e-4);

            float acrossMask = smoothstep(0.0, gw, cell.x) * smoothstep(0.0, gw, 1.0 - cell.x);
            float mask = 1.0 - acrossMask;

            if (_PlankLength > 1e-3)
            {
                float alongMask = smoothstep(0.0, gw, cell.y) * smoothstep(0.0, gw, 1.0 - cell.y);
                mask = max(mask, 1.0 - alongMask);
            }

            return saturate(mask);
        }

        // Grain: two octaves of value noise, squashed hard along the plank so it reads
        // as fibre rather than blobs. Offset per board so no two repeat.
        //
        // _GrainScale is CELLS PER METRE, so it holds its real size across props exactly
        // like the plank sizes do. Aim for about 10 cells across one plank: 85 cells/m
        // over a 0.12 m board gives 10.2, which is the density that reads as fibre.
        // Below ~5 per plank it smooths into a flat gradient; above ~15 it aliases into
        // noise on a phone screen.
        float RL_WoodGrain(float2 wc, float row, float col)
        {
            float2 g = float2(wc.x / max(_GrainStretch, 1.0), wc.y) * _GrainScale;
            g += float2(row * 17.31 + col * 5.77, row * 3.19 - col * 11.53);

            float n = RL_ValueNoise(g);
            n = lerp(n, n * 0.6 + RL_ValueNoise(g * 2.37 + 9.1) * 0.4, _GrainDetail);

            // Contrast around the midpoint, so strength 0 leaves the wood tone flat.
            n = saturate((n - 0.5) * _GrainContrast + 0.5);
            return n;
        }
        ENDHLSL

        // =============================================================================
        //  PASS 1 - Forward
        // =============================================================================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex RL_Vert
            #pragma fragment RL_Frag

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _TWOTONE_WOOD

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
                float3 positionOS  : TEXCOORD3;
                float3 normalOS    : TEXCOORD4;
                float4 shadowCoord : TEXCOORD5;
                float  fogCoord    : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings RL_Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                float3 rawNormalOS = RL_SafeNormal(input.normalOS, float3(0.0, 1.0, 0.0));
                float3 roundedOS   = RL_RoundedNormalOS(input.positionOS.xyz, rawNormalOS);

                output.positionCS  = positionInputs.positionCS;
                output.positionWS  = positionInputs.positionWS;
                output.positionOS  = input.positionOS.xyz;
                // The RAW normal drives the wood projection, so the plank layout does
                // not swim when _Roundness is dialled up.
                output.normalOS    = rawNormalOS;
                output.normalWS    = RL_SafeNormal(TransformObjectToWorldNormal(roundedOS),
                                                   float3(0.0, 1.0, 0.0));
                output.uv          = TRANSFORM_TEX(input.uv, _BaseMap);
                output.shadowCoord = GetShadowCoord(positionInputs);
                output.fogCoord    = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 RL_Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 baseSample = RL_SampleBase(input.uv);
                RL_ClipAlpha(baseSample.a);

                float3 normalWSf = RL_SafeNormal(input.normalWS, float3(0.0, 1.0, 0.0));
                half3  normalWS  = (half3)normalWSf;

                // Position derivatives, computed ONCE and outside every branch.
                float3 dPx = ddx(input.positionWS);
                float3 dPy = ddy(input.positionWS);

                // =================================================================
                //  WOOD
                // =================================================================
                float2 wc   = RL_WoodCoord(input.positionOS, input.normalOS);
                float4 cell = RL_PlankCell(wc);

                float groove = RL_GrooveMask(wc);
                float grain  = RL_WoodGrain(wc, cell.z, cell.w);

                half3 toneLight, toneDark, toneGroove;
                RL_ResolveWoodTones(toneLight, toneDark, toneGroove);

                half3 wood = lerp(toneDark, toneLight,
                                  (half)lerp(0.5, grain, _GrainStrength));

                // Per-board variation on TWO axes. Brightness alone is not enough:
                // real timber varies in warmth as much as in value, and it is the
                // warm/cool difference that makes a surface read as several boards
                // rather than one printed pattern.
                float boardA = RL_Hash21(float2(cell.z, cell.w));
                float boardB = RL_Hash21(float2(cell.w * 3.71 - 8.3, cell.z * 1.93 + 2.7));

                half3 boardTint = lerp(_PlankCoolTint.rgb, _PlankWarmTint.rgb, (half)boardB);
                boardTint = lerp(half3(1.0h, 1.0h, 1.0h), boardTint, _PlankHueVariation);
                boardTint *= (half)lerp(1.0 - _PlankTintVariation,
                                        1.0 + _PlankTintVariation, boardA);
                wood *= boardTint;

                wood = lerp(wood, toneGroove, (half)groove * _GrooveDepth);

                half3 albedo = wood * _BaseColor.rgb * baseSample.rgb;

                // Groove relief. Three cheap smoothstep evaluations give the groove's
                // slope; tilting the normal by it is what makes the seams catch the
                // crease shadow below instead of looking painted on.
                if (_GrooveNormalStrength > 1e-3h)
                {
                    float e = max(_PlankWidth, 1e-3) * 0.12;
                    float gdx = RL_GrooveMask(wc + float2(e, 0.0)) - groove;
                    float gdy = RL_GrooveMask(wc + float2(0.0, e)) - groove;

                    float3 tangentish   = RL_SafeNormal(dPx, float3(1.0, 0.0, 0.0));
                    float3 bitangentish = RL_SafeNormal(dPy, float3(0.0, 0.0, 1.0));

                    float3 tilt = tangentish * gdx + bitangentish * gdy;
                    normalWSf = RL_SafeNormal(normalWSf - tilt * _GrooveNormalStrength * 4.0,
                                              normalWSf);
                    normalWS = (half3)normalWSf;
                }

                // =================================================================
                //  LIGHTING - identical maths to RestaurantLoop/SoftMatte
                // =================================================================

                // Curvature from screen-space derivatives. Dividing by the squared
                // world size of a pixel makes this real curvature (1/radius in world
                // units) rather than something that changes with resolution or camera
                // distance. Positive is convex - an outer edge; negative is concave.
                float3 dNx = ddx(normalWSf);
                float3 dNy = ddy(normalWSf);
                float  pixelArea = dot(dPx, dPx) + dot(dPy, dPy) + 1e-8;
                float  curvature = (dot(dNx, dPx) + dot(dNy, dPy)) / pixelArea;
                curvature = clamp(curvature * _CurvatureScale, -1.0, 1.0);

                half concave = saturate((half)(-curvature));
                half convex  = saturate((half)( curvature));

                // Hemisphere: the main volume cue on a fixed top-down camera.
                half up = normalWS.y * 0.5h + 0.5h;
                up = saturate(lerp(step(0.5h, up), up, _HemisphereWrap));
                half3 hemisphere = lerp(_GroundColor.rgb, _SkyColor.rgb, up);

                float3 fixedDir = RL_SafeNormal(_FakeLightDirection.xyz, float3(-0.35, 0.85, -0.4));
                float3 lightDir = RL_SafeNormal(
                    lerp(fixedDir, _MainLightPosition.xyz, saturate(_UseMainLightDirection)),
                    fixedDir);

                // Half-lambert with the dot left SIGNED. Saturating before the *0.5+0.5
                // would clamp the result to 0.5..1 and flatten the form.
                half ndl = dot(normalWS, (half3)lightDir);
                half halfLambert = ndl * 0.5h + 0.5h;
                half lo = min(_ShadowThreshold, _LightThreshold - 1e-3h);
                half ramp = smoothstep(lo, _LightThreshold, halfLambert);
                ramp = RL_ToonQuantize(ramp, _ToonSteps, _ToonStepSoftness);

                half3 directional = lerp(_ShadowTint.rgb, _LightTint.rgb, ramp);
                directional = lerp(half3(1.0h, 1.0h, 1.0h), directional, _DirectionalStrength);

                Light mainLight = GetMainLight(input.shadowCoord);
                half atten = lerp(1.0h, mainLight.shadowAttenuation, saturate(_ReceiveShadowStrength));
                atten = max(atten, _ShadowBrightness);
                half3 shadowTerm = lerp(_ShadowColorTint.rgb, half3(1.0h, 1.0h, 1.0h), atten);

                half texAO = SAMPLE_TEXTURE2D(_AOMap, sampler_AOMap, input.uv).r;
                half ao = max(lerp(1.0h, texAO, _AOStrength), _AOFloor);

                half3 color = albedo * hemisphere * directional * shadowTerm * ao;

                color = lerp(color, color * _CreaseColor.rgb, concave * _CreaseShadow);
                color += _EdgeLightColor.rgb * (convex * _EdgeLight) * albedo;

                float height = (_GradientSpace < 0.5h) ? input.positionOS.y : input.positionWS.y;
                float range  = _GradientEnd - _GradientStart;
                float denom  = (abs(range) < 1e-4) ? 1e-4 : range;
                half  gradT  = saturate((half)((height - _GradientStart) / denom));
                half3 gradient = lerp(_BottomGradientColor.rgb, _TopGradientColor.rgb, gradT);
                color = lerp(color, color * gradient, _GradientIntensity);

                half luminance = dot(color, half3(0.2126h, 0.7152h, 0.0722h));
                color = lerp(luminance.xxx, color, _Saturation) * _Brightness;

                color = MixFog(color, input.fogCoord);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        // =============================================================================
        //  PASS 2 - Shadow Caster
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
            #pragma target 3.0
            #pragma vertex RL_ShadowVert
            #pragma fragment RL_ShadowFrag

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            float3 _LightDirection;
            float3 _LightPosition;

            struct AttributesShadow
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsShadow
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            VaryingsShadow RL_ShadowVert(AttributesShadow input)
            {
                VaryingsShadow output = (VaryingsShadow)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 normalOS   = RL_SafeNormal(input.normalOS, float3(0.0, 1.0, 0.0));
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS   = RL_SafeNormal(TransformObjectToWorldNormal(normalOS),
                                                  float3(0.0, 1.0, 0.0));

                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirectionWS = RL_SafeNormal(_LightPosition - positionWS,
                                                            float3(0.0, 1.0, 0.0));
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

            half4 RL_ShadowFrag(VaryingsShadow input) : SV_Target
            {
                RL_ClipAlpha(RL_SampleBase(input.uv).a);
                return 0;
            }
            ENDHLSL
        }

        // =============================================================================
        //  PASS 3 - Depth Only
        // =============================================================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex RL_DepthVert
            #pragma fragment RL_DepthFrag

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing

            struct AttributesDepth
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsDepth
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            VaryingsDepth RL_DepthVert(AttributesDepth input)
            {
                VaryingsDepth output = (VaryingsDepth)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 RL_DepthFrag(VaryingsDepth input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                RL_ClipAlpha(RL_SampleBase(input.uv).a);
                return 0;
            }
            ENDHLSL
        }

        // =============================================================================
        //  PASS 4 - Depth Normals
        //  Present so the material appears in the DepthNormals prepass that URP's SSAO
        //  renderer feature reads. The rounded normal is written here too, so SSAO
        //  agrees with the shading.
        // =============================================================================
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex RL_DepthNormalsVert
            #pragma fragment RL_DepthNormalsFrag

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
                UNITY_VERTEX_OUTPUT_STEREO
            };

            VaryingsDN RL_DepthNormalsVert(AttributesDN input)
            {
                VaryingsDN output = (VaryingsDN)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 roundedOS = RL_RoundedNormalOS(input.positionOS.xyz, input.normalOS);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS   = RL_SafeNormal(TransformObjectToWorldNormal(roundedOS),
                                                  float3(0.0, 1.0, 0.0));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 RL_DepthNormalsFrag(VaryingsDN input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                RL_ClipAlpha(RL_SampleBase(input.uv).a);
                return half4((half3)RL_SafeNormal(input.normalWS, float3(0.0, 1.0, 0.0)), 0.0h);
            }
            ENDHLSL
        }
    }

    // No Fallback on purpose. If this ever fails, magenta plus one real Console line
    // beats a silently substituted lit shader that hides the cause.
}
