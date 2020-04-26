#ifndef VXGI_RADIANCES_VOXEL
  #define VXGI_RADIANCES_VOXEL

  #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Variables.cginc"
  #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Utilities.cginc"
  #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Visibility.cginc"
  #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Radiances/Sampler.cginc"
  #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Structs/VoxelLightingData.hlsl"

  float3 DirectVoxelRadiance(VoxelLightingData data)
  {
    float3 radiance = 0.0;

    for (uint i = 0; i < LightCount; i++) {
      LightSource lightSource = LightSources[i];

      bool notInRange;
      float3 localPosition;

      [branch]
      if (lightSource.type == LIGHT_SOURCE_TYPE_DIRECTIONAL) {
        localPosition = -lightSource.direction;
        notInRange = false;
        lightSource.voxelPosition = mad(localPosition, Resolution << 1, data.voxelPosition);
      } else {
        localPosition = lightSource.worldposition - data.worldPosition;
        notInRange = lightSource.NotInRange(localPosition);
      }

      data.Prepare(normalize(localPosition));

      float spotFalloff = lightSource.SpotFalloff(-data.vecL);

      if (notInRange || (spotFalloff <= 0.0) || (data.NdotL <= 0.0)) continue;

      radiance +=
        VoxelVisibility(data.voxelPosition + data.vecL / data.NdotL, lightSource.voxelPosition)
        * data.NdotL
        * spotFalloff
        * lightSource.Attenuation(localPosition);
    }

    return radiance;
  }

  float3 IndirectVoxelRadiance(VoxelLightingData data)
  {
    if (TextureSDF(data.voxelPosition / Resolution) < 0.0) return 0.0;

    float3 apex = mad(0.5, data.vecN, data.voxelPosition) / Resolution;
    float3 radiance = 0.0;
    uint cones = 0;

    for (uint i = 0; i < 32; i++) {
      float3 unit = Directions[i];
      float NdotL = dot(data.vecN, unit);

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

  float3 VoxelRadiance(VoxelLightingData data)
  {
    return data.color * (DirectVoxelRadiance(data) + IndirectVoxelRadiance(data));
  }
#endif
