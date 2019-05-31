#ifndef VXGI_RADIANCES_VOXEL
  #define VXGI_RADIANCES_VOXEL

  #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Variables.cginc"
  #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Utilities.cginc"
  #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Visibility.cginc"
  #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Radiances/Sampler.cginc"

  float3 DirectVoxelRadiance(float3 voxelPosition, float3 normal)
  {
    float3 worldPosition = mul(VoxelToWorld, float4(voxelPosition, 1.0)).xyz;
    float3 radiance = 0.0;

    for (uint i = 0; i < LightCount; i++) {
      float3 lightPosition = LightPositions[i];
      float3 relativePosition = lightPosition - worldPosition;
      float3 vecL = normalize(relativePosition);
      float NdotL = dot(normal, vecL);

      if (NdotL <= 0.0) continue;

      float3 attenuation = NdotL * LightAttenuation(LightColors[i], relativePosition);

      if (all(attenuation < 0.05)) continue;

      lightPosition = mul(WorldToVoxel, float4(lightPosition, 1.0)).xyz;
      radiance += VoxelVisibility(voxelPosition + vecL / NdotL, lightPosition) * attenuation;
    }

#ifdef TRACE_SUN
    float3 vecL = -SunDirection;
    float NdotL = dot(normal, vecL);
    float3 attenuation = NdotL * SunColor;

    if ((NdotL > 0.0) && all(attenuation >= 0.05)) {
      radiance += VoxelVisibility(voxelPosition + vecL / NdotL, voxelPosition + vecL * (Resolution << 1)) * attenuation;
    }
#endif

    return radiance;
  }

  float3 IndirectVoxelRadiance(float3 voxelPosition, float3 normal)
  {
    if (TextureSDF(voxelPosition / Resolution) < 0.0) return 0.0;

    float3 apex = mad(0.5, normal, voxelPosition) / Resolution;
    float3 radiance = 0.0;
    uint cones = 0;

    for (uint i = 0; i < 32; i++) {
      float3 unit = Directions[i];
      float NdotL = dot(normal, unit);

      if (NdotL <= 0.0) continue;

      float4 incoming = 0.0;
      float size = 1.0;
      float3 direction = 1.5 * size * unit / Resolution;

      for (
        float3 coordinate = apex + direction;
        incoming.a < 0.95 && TextureSDF(coordinate) > 0.0;
        size *= 1.5, direction *= 1.5, coordinate = apex + direction
      ) {
        incoming += 0.5 * (1.0 - incoming.a) * SampleRadiance(coordinate, size);
      }

      radiance += incoming.rgb * NdotL;
      cones++;
    }

    return radiance * 2.0 / cones;
  }

  float3 VoxelRadiance(float3 voxelPosition, float3 normal, float3 color)
  {
    return color * (
      DirectVoxelRadiance(voxelPosition, normal)
      + IndirectVoxelRadiance(voxelPosition, normal)
    );
  }
#endif
