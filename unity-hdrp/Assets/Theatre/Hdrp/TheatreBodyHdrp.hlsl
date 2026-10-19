#ifndef EVOSIM_THEATRE_BODY_HDRP_INCLUDED
#define EVOSIM_THEATRE_BODY_HDRP_INCLUDED

// The theatre's body on HDRP, for the spike (D202). The graph "Evosim/Theatre Body" calls the two
// functions below and does nothing else; tools/make_graph.py writes it from body.graph.json.
//
// What is the URP body's and what is HDRP's. The shape is the URP body's, word for word: the
// carve, the pinch, the taper and bend of a box and the leaf (TheatreBodyShape.hlsl, copied by
// tools/port_hlsl.py). So is the surface: the mottle, the veins, the leaf's tone and every cell's
// own shade. The light is HDRP's: the key, the fill, the ambient, the caustics, the fog and the
// fading with depth that the URP body computes by hand are left to HDRP's lights, its water and
// its volumetric fog, which is the question the spike asks. Two of the URP body's terms have no
// physical counterpart and are kept as emission, because the dark-field look rests on them: the
// guild's rim and the inner glow of a fed body.

#include "TheatreBodyShape.hlsl"
#include "TheatreDepthHdrp.hlsl"

// The key as the URP body sees it, and every directional light in the scene, from HdrpLook: the
// direction towards each (w 1 for the key), and its colour times its URP intensity, before the
// conversion to lux. The snapshot adds a portrait's back light and fills for each render; the URP
// body adds the light through the tissue for every one of them, and so does this.
#define EVO_HDRP_LIGHTS 8
float4 _EvoHdrpKeyDir;
float4 _EvoHdrpKeyColour;
float4 _EvoHdrpLightDir[EVO_HDRP_LIGHTS];
float4 _EvoHdrpLightColour[EVO_HDRP_LIGHTS];
float _EvoHdrpLightCount;
float _EvoHdrpBodyDebug;      // the test view, from HdrpLook (EVOSIM_HDRP_BODY_DEBUG)

// The URP body's light through a thin part: Barre-Brisebois and Bouchard's single pass
// approximation [BB11] in Austin's URP arrangement [JA], word for word.
float3 EvoTransmission(float3 n, float3 v, float3 l, float3 lightColour, float thickness)
{
    float3 bent = normalize(l + n * _TransDistortion);
    float back = pow(saturate(dot(v, -bent)), _TransPower) * _TransScale;
    return lightColour * (back * thickness);
}

// The vertex stage: the URP body's ShapeBody, handed back in object space for the graph's
// Position and Normal blocks, and what the fragment needs carried across.
void EvoBodyVertex_float(float3 PositionOS, float3 NormalOS, float4 Shape, float4 LeafIn,
    out float3 Position, out float3 Normal, out float3 CarveOS, out float CarveDepth,
    out float4 Leaf, out float Thickness)
{
    EvoBodyAttributes input = (EvoBodyAttributes)0;
    input.positionOS = float4(PositionOS, 1.0);
    input.normalOS = NormalOS;
    input.shapeOS = Shape.xy;
    input.leafOS = LeafIn;

    BodyVertex body = ShapeBody(input);

    // ShapeBody works in HDRP's camera-relative world space through the same transforms, so the
    // round trip back to object space cancels the camera's offset.
    Position = TransformWorldToObject(body.positionWS);
    Normal = normalize(TransformWorldToObjectNormal(body.normalWS));
    CarveOS = body.carveOS;
    CarveDepth = body.carveDepth;
    Leaf = body.leaf;

    // As the URP body: a thin part transmits and a thick one does not.
    float transMetres = _TransMetres + 0.08 * body.halfWidthMetres;
    Thickness = saturate(transMetres / max(1e-4, body.halfThickMetres));
}

// The fragment stage. PositionAWS is the absolute world position, so the mottle sits on the
// tissue wherever the camera is; ViewWS points from the surface to the eye.
void EvoBodySurface_float(float3 CarveOS, float CarveDepth, float4 Leaf, float Thickness,
    float3 NormalWS, float3 PositionAWS, float3 ViewWS,
    out float3 BaseColor, out float3 NormalOut, out float Smoothness, out float Metallic,
    out float3 Emission, out float Alpha, out float ThicknessOut)
{
    float3 n = CarvedNormal(normalize(NormalWS), CarveOS, CarveDepth);
    float3 v = normalize(ViewWS);

    float3 body = _BaseColor.rgb;

    float f1, f2, tone;
    EvoVoronoi(PositionAWS * _MottleCellsPerMetre, f1, f2, tone);
    float wall = saturate((f2 - f1) * 2.5);
    float mottle = lerp(1.0 - _MottleStrength, 1.0 + _MottleStrength, tone);
    mottle *= lerp(1.0 - _MottleStrength * 0.7, 1.0, wall);
    body *= mottle;

    float veinFace, veinThrough;
    LeafVeins(Leaf, veinFace, veinThrough);
    body *= veinFace;
    float thickness = Thickness * veinThrough;

    float3 leafTone = float3(1.0, 1.0, 1.0);
    float rimScale = 1.0;
    if (Leaf.w > 0.5)
    {
        float across = saturate(abs(Leaf.z));
        float edge = smoothstep(0.35, 1.0, across);
        float stalk = 1.0 - smoothstep(0.0, 0.4, Leaf.x);
        float leafShade = lerp(0.5, 1.12, edge) * (1.0 - 0.35 * stalk);
        leafTone = leafShade * lerp(float3(1.0, 1.0, 1.0), float3(1.12, 1.06, 0.78), 0.5 * edge);
        leafTone *= lerp(1.0, mottle, 0.7);
        rimScale = 0.45;
    }

    float shade = frac(_Individual * 17.37 + 0.41) * 2.0 - 1.0;
    float warmth = frac(_Individual * 31.91 + 0.07) * 2.0 - 1.0;
    float3 own = (1.0 + 0.08 * shade) * float3(1.0 + 0.05 * warmth, 1.0, 1.0 - 0.07 * warmth);

    // The guild's rim and the inner glow, as the URP body draws them, as light the body gives
    // off. In emission units at the fixed exposure TheatreGrade sets (EV 0, a multiplier of one),
    // where they keep the URP picture's brightness. Not divided by HDRP's current exposure, which
    // is not valid in every pass and turned the earth a flat, fogless colour.
    float facing = 1.0 - saturate(dot(n, v));
    float rim = pow(facing, _RimPower);
    float3 glow = _RimColor.rgb * (rim * _RimStrength * rimScale);
    glow += _RimColor.rgb * (_GlowStrength * _Reserve * rimScale * (0.30 + 0.70 * pow(facing, 0.7)));

    float3 depthLight = EvoDepthLight(PositionAWS.y);

    // The light through the body, as the URP body adds it: every directional light through a thin
    // part (the key's dimmed by depth, the others not, as URP's additional lights are not), and on
    // a blade the light from the surface above. A portrait's back light stands behind the subject
    // shining at the lens, so the light through the tissue is most of what a portrait sees: the URP
    // body with its key, its fill and its caustics at zero keeps its colour. HDRP's standard Lit has
    // no such term, and the translucent material type wants a diffusion profile the spike does not
    // build, so it is carried as emission, as the rim is.
    float3 keyColour = _EvoHdrpKeyColour.rgb * depthLight;
    float3 through = float3(0.0, 0.0, 0.0);
    int count = (int)min(_EvoHdrpLightCount, (float)EVO_HDRP_LIGHTS);
    for (int i = 0; i < count; i++)
    {
        float3 colour = _EvoHdrpLightColour[i].rgb * (_EvoHdrpLightDir[i].w > 0.5 ? depthLight : float3(1.0, 1.0, 1.0));
        through += EvoTransmission(n, v, normalize(_EvoHdrpLightDir[i].xyz), colour, thickness);
    }
    through *= _TransTint.rgb * _TransGain;
    if (Leaf.w > 0.5)
        through += _TransTint.rgb * (_TransGain * _LeafSkyGlow * EvoTransmission(n, v, float3(0.0, 1.0, 0.0), keyColour, thickness));
    BaseColor = saturate(body * leafTone * own) * depthLight;
    NormalOut = n;
    // The sheen by guild: a gut wall's gloss against a leaf's matte.
    Smoothness = lerp(0.30, 0.82, saturate(_Sheen));
    Metallic = 0.0;
    Emission = (glow + through) * leafTone * own;

    // A test view (EVOSIM_HDRP_BODY_DEBUG): 1 draws the emission as the rim colour alone, 2 the
    // rim term alone, so a picture says whether the per body colour and the rim reach HDRP.
    if (_EvoHdrpBodyDebug > 0.5 && _EvoHdrpBodyDebug < 1.5) { BaseColor = 0; Emission = _RimColor.rgb; }
    else if (_EvoHdrpBodyDebug > 1.5 && _EvoHdrpBodyDebug < 2.5) { BaseColor = 0; Emission = float3(rim, facing, saturate(dot(n, v))); }
    Alpha = _Fade;
    // HDRP's thickness runs the other way from the URP body's: one is opaque.
    ThicknessOut = saturate(1.0 - thickness * _TransGain);
}

#endif
