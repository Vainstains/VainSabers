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

float3 ExposureStripPosition(ExposurePath path, float radius, float edge, out float across)
{
    // Each row stays at its live axial position. Two edges enclose the entire
    // projected interval without building one translucent layer per pose.
    float padding = radius + max(0.002, radius * 0.25);
    across = lerp(min(0.0, path.distance) - padding, max(0.0, path.distance) + padding, edge);
    return path.center + path.side * across;
}

float ShutterIntegral(float age)
{
    age = saturate(age);
    // Integral of the smooth closing shutter 1 - smoothstep(0, 1, age).
    return age - age * age * age * (1.0 - 0.5 * age);
}

float IntegrateExposure(float x, float radius, float speed)
{
    float enter = (x - radius) / speed;
    float leave = (x + radius) / speed;
    return 2.0 * (ShutterIntegral(leave) - ShutterIntegral(enter));
}

float ExposureCoverage(float4 profile)
{
    float x = profile.x * sign(profile.y);
    float speed = max(abs(profile.y), 0.0001);
    float sampleOffset = 0.32 * max(fwidth(profile.x), 0.0001);
    float exposure = 0.5 * (IntegrateExposure(x - sampleOffset, profile.z, speed)
                          + IntegrateExposure(x + sampleOffset, profile.z, speed));
    float response = lerp(0.58, 0.76, saturate(_VertexBlurFade * 0.18));
    return profile.w * pow(saturate(exposure), response);
}
