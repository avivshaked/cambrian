#ifndef EVOSIM_THEATRE_VOLUME
#define EVOSIM_THEATRE_VOLUME

// The view ray through the water, for the two passes of the underwater look that are drawn by
// marching the volume rather than by putting geometry in it: the shafts (item 4,
// TheatreShaftMarch.shader) and the snow around the camera (item 5, TheatreSnowShells.shader).
//
// How a pass gets one fragment per pixel without a renderer feature. Each is drawn on a box
// around the water, its inside faces only (Cull Front) and with the depth test off, so a
// camera inside the box or outside it gets exactly one fragment wherever the box is on screen.
// The fragment's own position then says nothing but which way the ray goes: the ray is clipped
// here to the box, to the tank's circle where there is one, and to whatever the scene drew,
// from the depth buffer, so a body in front of the water behind it is never covered.
//
// A shader that includes this must include URP's Core.hlsl, TheatreWater.hlsl and
// TheatreDepth.hlsl first.

// The ray the camera sends through a fragment: from the eye for a perspective camera, and from
// the camera's plane along its forward for an orthographic one, whose rays are parallel.
// tScene is where the scene's own depth stops it, in metres along the ray.
void EvoViewRay(float3 positionWS, float4 positionCS, out float3 origin, out float3 dir, out float tScene)
{
    float3 eye = GetCameraPositionWS();
    float3 forward = -UNITY_MATRIX_V[2].xyz;

    if (unity_OrthoParams.w > 0.5)
    {
        dir = normalize(forward);
        origin = positionWS - dir * dot(positionWS - eye, dir);
    }
    else
    {
        dir = normalize(positionWS - eye);
        origin = eye;
    }

    float2 uv = GetNormalizedScreenSpaceUV(positionCS);
    float sceneEye = EvoSceneEyeDepth(uv);

    tScene = sceneEye / max(1e-4, dot(dir, normalize(forward)));
}

// Clips [t0, t1] to an axis-aligned box and, when tank.w is 1, to the upright cylinder of radius
// tank.z about (tank.x, tank.y) in x and z. False when nothing of the ray is left.
bool EvoClipToWater(float3 origin, float3 dir, float3 low, float3 high, float4 tank, inout float t0, inout float t1)
{
    float3 safe = dir + step(abs(dir), 1e-6) * 1e-6;

    float3 a = (low - origin) / safe;
    float3 b = (high - origin) / safe;
    float3 nearer = min(a, b);
    float3 farther = max(a, b);

    t0 = max(t0, max(max(nearer.x, nearer.y), nearer.z));
    t1 = min(t1, min(min(farther.x, farther.y), farther.z));

    if (tank.w > 0.5)
    {
        float2 o = origin.xz - tank.xy;
        float2 d = dir.xz;

        float qa = dot(d, d);
        float qb = dot(o, d);
        float qc = dot(o, o) - tank.z * tank.z;

        if (qa > 1e-8)
        {
            float disc = qb * qb - qa * qc;
            if (disc < 0.0) return false;

            float s = sqrt(disc);
            t0 = max(t0, (-qb - s) / qa);
            t1 = min(t1, (-qb + s) / qa);
        }
        else if (qc > 0.0)
        {
            return false;
        }
    }

    return t1 > t0;
}

// Whether a point is in the water: inside the box, under the waterline, and inside the tank's
// circle when there is one.
bool EvoInWater(float3 p, float3 low, float3 high, float4 tank)
{
    if (any(p < low) || any(p > high)) return false;

    if (tank.w > 0.5)
    {
        float2 o = p.xz - tank.xy;
        if (dot(o, o) > tank.z * tank.z) return false;
    }

    return true;
}

// The size of one pixel, in metres, at a distance along the ray: grows with distance for a
// perspective camera and is fixed for an orthographic one.
float EvoPixelMetres(float t)
{
    if (unity_OrthoParams.w > 0.5) return 2.0 * unity_OrthoParams.y / max(1.0, _ScreenParams.y);

    // abs: a projection rendering into a texture is flipped on some graphics APIs, which negates
    // this term and nothing about the pixel's size.
    return t * 2.0 / (max(1e-4, abs(UNITY_MATRIX_P[1][1])) * max(1.0, _ScreenParams.y));
}

#endif
