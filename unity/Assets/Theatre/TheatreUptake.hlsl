// The absorptive uptake look's colour, in HSL (TheatreUptake.cs, EVOSIM_THEATRE_UPTAKE).
//
// Included by TheatreBody.shader and called only where _Uptake.x is above zero, which only the dial
// sets. With the dial at 0 nothing here runs and nothing in the shader that was there before it
// changes.
//
// What it does: a tissue that is taking more in is drawn deeper within its own hue. Lightness comes
// down by up to 0.22 and saturation goes up by up to 0.15, both times u, in HSL as the palette's
// Muted does it. The hue is never touched: it is read out of the colour and put back.
//
// Why through sRGB. TheatrePalette caps and sets its lightnesses on the sRGB numbers of the guild
// colours (0.58 for the rim), and the engine hands this shader the linear ones, since the project
// is in linear colour space. A lightness taken on the linear numbers is a different, much darker
// scale, so the colour goes to the sRGB side (a 2.2 power, close enough for a tint) for the HSL
// move and comes back.

#ifndef EVOSIM_THEATRE_UPTAKE_INCLUDED
#define EVOSIM_THEATRE_UPTAKE_INCLUDED

// The rim's lightness in TheatrePalette (RimLightness): the lightness at which the full 0.22 applies.
// A darker colour (the body under the rim, a starving body) loses the same share of it as it is
// dark, so a colour at 0.18 is not driven to black by a move meant for one at 0.58.
static const float EVO_UPTAKE_REFERENCE_LIGHTNESS = 0.58;
static const float EVO_UPTAKE_LIGHTNESS_DROP = 0.22;
static const float EVO_UPTAKE_SATURATION_RISE = 0.15;

float3 EvoRgbToHsl(float3 c)
{
    float mx = max(c.r, max(c.g, c.b));
    float mn = min(c.r, min(c.g, c.b));
    float d = mx - mn;
    float l = 0.5 * (mx + mn);

    float h = 0.0;
    float s = 0.0;

    if (d > 1e-6)
    {
        s = d / max(1e-6, 1.0 - abs(2.0 * l - 1.0));

        if (mx == c.r) h = (c.g - c.b) / d + (c.g < c.b ? 6.0 : 0.0);
        else if (mx == c.g) h = (c.b - c.r) / d + 2.0;
        else h = (c.r - c.g) / d + 4.0;

        h /= 6.0;
    }

    return float3(h, s, l);
}

float3 EvoHslToRgb(float3 hsl)
{
    float3 k = saturate(abs(fmod(hsl.x * 6.0 + float3(0.0, 4.0, 2.0), 6.0) - 3.0) - 1.0);
    float chroma = (1.0 - abs(2.0 * hsl.z - 1.0)) * hsl.y;

    return hsl.z + chroma * (k - 0.5);
}

// A linear colour, deepened by u in [0, 1] within its own hue.
float3 EvoUptakeDeepen(float3 linearColour, float u)
{
    float3 srgb = pow(max(linearColour, 0.0), 1.0 / 2.2);
    float3 hsl = EvoRgbToHsl(srgb);

    float drop = EVO_UPTAKE_LIGHTNESS_DROP * u * min(1.0, hsl.z / EVO_UPTAKE_REFERENCE_LIGHTNESS);
    hsl.z = max(0.0, hsl.z - drop);

    // A grey has no hue to deepen: its saturation stays nothing, or it would turn red.
    if (hsl.y > 1e-4) hsl.y = saturate(hsl.y + EVO_UPTAKE_SATURATION_RISE * u);

    return pow(saturate(EvoHslToRgb(hsl)), 2.2);
}

#endif
