#ifndef VXGI_RADIANCES_PIXEL
  #define VXGI_RADIANCES_PIXEL

  #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Variables.cginc"
  #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Utilities.cginc"
  #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Visibility.cginc"
  #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/BRDFs/General.cginc"
  #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Radiances/Sampler.cginc"
  #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Structs/LightingData.cginc"

  float3 DirectPixelRadiance(LightingData data)
  {
    float3 radiance = 0.0;

    for (uint i = 0; i < LightCount; i++) {
      float3 lightPosition = LightPositions[i];
      float3 relativePosition = lightPosition - data.worldPosition;
      data.Prepare(normalize(relativePosition));

      if (data.NdotL <= 0.0) continue;

      float3 attenuation = data.NdotL * LightAttenuation(LightColors[i], relativePosition);

      if (all(attenuation < 0.01)) continue;

      lightPosition = mul(WorldToVoxel, float4(lightPosition, 1.0)).xyz;
      radiance += VoxelVisibility(data.voxelPosition + data.vecN, lightPosition) * GeneralBRDF(data) * attenuation;
    }

#ifdef TRACE_SUN
    data.Prepare(-SunDirection);
    float3 attenuation = data.NdotL * SunColor;

    if (any(attenuation >= 0.05)) {
      radiance += VoxelVisibility(data.voxelPosition + data.vecN, mad(data.vecL, Resolution << 1, data.voxelPosition)) * GeneralBRDF(data) * attenuation;
    }
#endif

    return radiance;
  }

  float3 IndirectSpecularPixelRadiance(LightingData data)
  {
    float4 radiance = 0.0;
    float ratio = mad(-1.0, data.glossiness, 2.0);
    float size = max(0.1, data.glossiness);
    float3 step = data.vecR / Resolution;

    for (
      float3 coordinate = data.voxelPosition / Resolution + 1.5 * step / data.NdotR;
      radiance.a < 1.0 && TextureSDF(coordinate) > 0.0;
      size *= ratio, coordinate += 0.5 * size * step
    ) {
      radiance += (1.0 - radiance.a) * SampleRadiance(coordinate, size);
    }

    if (radiance.a < 1.0) {
      half4 skyData = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, data.vecR, 6.0 * data.perceptualRoughness);
      radiance += (1 - radiance.a) * half4(DecodeHDR(skyData, unity_SpecCube0_HDR), 1.0);
    }

    return data.specularColor * radiance.rgb * data.NdotR;
  }

  float3 IndirectDiffusePixelRadiance(float3 voxelPosition, float3 normal)
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
#endif
