#ifndef VXGI_RADIANCES_VOXEL
  #define VXGI_RADIANCES_VOXEL

  #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Variables.cginc"
  #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Utilities.cginc"
  #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Visibility.cginc"
  #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Radiances/Sampler.cginc"
  #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Structs/VoxelLightingData.hlsl"

  void DirectVoxelRadianceDirectional(VoxelLightingData data, LightSource lightSource, inout float3 radiance) {
    data.Prepare(-lightSource.direction);

    if (data.NdotL <= 0.0) return;

    float3 attenuation = data.NdotL * lightSource.color;

    radiance += VoxelVisibility(data.voxelPosition + data.vecL / data.NdotL, data.voxelPosition + data.vecL * (Resolution << 1)) * attenuation;
  }

  void DirectVoxelRadiancePoint(VoxelLightingData data, LightSource lightSource, inout float3 radiance) {
    float3 relativePosition = lightSource.position - data.worldPosition;
    data.Prepare(normalize(relativePosition));

    if (
      (data.NdotL <= 0.0) ||
      lightSource.NotInRange(relativePosition)
    ) return;

    float3 attenuation = data.NdotL * LightAttenuation(lightSource.color, relativePosition);

    lightSource.position = mul(WorldToVoxel, float4(lightSource.position, 1.0)).xyz;
    radiance += VoxelVisibility(data.voxelPosition + data.vecL / data.NdotL, lightSource.position) * attenuation;
  }

  void DirectVoxelRadianceSpot(VoxelLightingData data, LightSource lightSource, inout float3 radiance) {
    float3 relativePosition = lightSource.position - data.worldPosition;
    data.Prepare(normalize(relativePosition));

    if (
      (data.NdotL <= 0.0) ||
      lightSource.NotInRange(relativePosition) ||
      (acos(dot(-data.vecL, lightSource.direction)) > radians(.5 * lightSource.spotAngle))
    ) return;

    float3 attenuation = data.NdotL * LightAttenuation(lightSource.color, relativePosition);

    lightSource.position = mul(WorldToVoxel, float4(lightSource.position, 1.0)).xyz;
    radiance += VoxelVisibility(data.voxelPosition + data.vecL / data.NdotL, lightSource.position) * attenuation;
  }

  float3 DirectVoxelRadiance(VoxelLightingData data)
  {
    float3 radiance = 0.0;

    for (uint i = 0; i < LightCount; i++) {
      LightSource lightSource = LightSources[i];

      switch (lightSource.type) {
        case LIGHT_SOURCE_TYPE_DIRECTIONAL:
          DirectVoxelRadianceDirectional(data, lightSource, radiance);
          break;
        case LIGHT_SOURCE_TYPE_POINT:
          DirectVoxelRadiancePoint(data, lightSource, radiance);
          break;
        case LIGHT_SOURCE_TYPE_SPOT:
          DirectVoxelRadianceSpot(data, lightSource, radiance);
          break;
      }
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
