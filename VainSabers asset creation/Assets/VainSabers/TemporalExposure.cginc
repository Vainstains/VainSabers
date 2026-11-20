struct ExposurePath
{
    float3 center;
    float3 side;
    float3 view;
    float distance;
    float weight;
};

float3 ExposurePoseAxis(float3 axis, int sample)
{
    float3 fwd = _HistFwd[sample].xyz;
    float3 up = _HistUp[sample].xyz;
    return cross(up, fwd) * axis.x + up * axis.y + fwd * axis.z;
}

float ExposureActivation(float travel, float radius, float sideLength)
{
    float widths = travel / max(2.0 * radius, 0.006);
    return smoothstep(0.14, 0.9, widths * saturate(_VainSaberBlurSoftness))
         * smoothstep(0.025, 0.13, sideLength);
}

ExposurePath GetExposurePath(float3 localCenter, float3 localAxis, float radius, float halfLength)
{
    ExposurePath path;
    path.center = _HistPos[0].xyz + ExposurePoseAxis(localCenter, 0);
    path.view = ViewDirectionInPart(_HistPos[0].xyz, _HistUp[0].xyz);
    float3 firstAxis = ExposurePoseAxis(localAxis, 0);
    float3 lastAxis = ExposurePoseAxis(localAxis, GPU_HIST_COUNT - 1);
    float3 side = cross(path.view, NormalizeOr(firstAxis + lastAxis, firstAxis));
    float sideLength = length(side);
    path.side = NormalizeOr(side, _HistUp[0].xyz);
    path.distance = 0.0;
    path.weight = 0.0;

    if (sideLength < 0.025 || _VainSaberBlurSoftness <= 0.0)
        return path;

    if (halfLength > 0.0)
    {
        // Static OBJ and sprite vertices share one fade. Evaluate two probes
        // instead of walking the history for every vertex of the original mesh.
        float travel = 0.0;
        [unroll]
        for (int i = 3; i < GPU_HIST_COUNT; i += 4)
        {
            float3 center = _HistPos[i].xyz + ExposurePoseAxis(localCenter, i);
            float3 axis = ExposurePoseAxis(localAxis, i);
            float distance = abs(dot(path.side, center - path.center));
            float rotation = halfLength * abs(dot(path.side, axis - firstAxis));
            travel = max(travel, distance + rotation);
        }
        path.weight = ExposureActivation(travel, radius, sideLength);
        return path;
    }

    // A sampled angular reversal can be lateral to one eye even when the 3D
    // history still advances. Keep the connected prefix in screen space.
    float tolerance = clamp(radius * 0.12, 0.00035, 0.0015);
    float direction = 0.0;
    float farthest = 0.0;
    [loop]
    for (int i = 1; i < GPU_HIST_COUNT; i++)
    {
        float3 center = _HistPos[i].xyz + ExposurePoseAxis(localCenter, i);
        float across = dot(path.side, center - path.center);
        if (direction == 0.0 && abs(across) > tolerance)
            direction = sign(across);
        float progress = direction * across;
        if (progress < farthest - tolerance)
            break;
        farthest = max(farthest, progress);
    }

    path.distance = direction * farthest;
    path.weight = ExposureActivation(farthest, radius, sideLength);
    return path;
}

float ExposureFeather(float distance, float radius)
{
    float softness = saturate(_VainSaberBlurSoftness);
    float spread = lerp(0.035, 0.12, softness);
#if defined(_GEOMETRY_OBJ) || defined(_GEOMETRY_SPRITE)
    spread *= 0.55;
#endif
    return max(0.002, radius * 0.65 + abs(distance) * spread);
}

float3 ExposureStripPosition(ExposurePath path, float radius, float edge, out float across)
{
    // Each row stays at its live axial position. Two edges enclose the entire
    // projected interval without building one translucent layer per pose.
    float padding = radius + 2.2 * ExposureFeather(path.distance, radius) + 0.002;
    across = lerp(min(0.0, path.distance) - padding, max(0.0, path.distance) + padding, edge);
    return path.center + path.side * across;
}

float IntegratedShutterPrimitive(float age)
{
    float t = saturate(age);
    float t2 = t * t;
    // Second primitive of 1 - smoothstep(0, 1, age), continued linearly past 1.
    return t2 * (0.5 + t2 * (0.1 * t - 0.25)) + 0.5 * max(0.0, age - 1.0);
}

float FilteredExposure(float x, float aperture, float travel, float feather)
{
    float invTravel = rcp(travel);
    float low = (x - feather) * invTravel;
    float high = (x + feather) * invTravel;
    float r = aperture * invTravel;
    float upper = IntegratedShutterPrimitive(high + r) - IntegratedShutterPrimitive(high - r);
    float lower = IntegratedShutterPrimitive(low + r) - IntegratedShutterPrimitive(low - r);
    return max(0.0, (upper - lower) * (travel / feather));
}

float ExposureCoverage(float4 profile)
{
    float x = profile.x * sign(profile.y);
    float travel = max(abs(profile.y), 0.0001);
    float feather = max(ExposureFeather(travel, profile.z), 0.65 * fwidth(profile.x));
    // Filter a narrow core and a wider halo analytically, without sampling the scene.
    float core = FilteredExposure(x, profile.z, travel, feather);
    float halo = FilteredExposure(x, profile.z, travel, 2.2 * feather);
    float exposure = lerp(core, halo, 0.27);
    float response = lerp(0.52, 0.68, saturate(_VertexBlurFade * 0.18));
    return profile.w * pow(saturate(exposure), response);
}
