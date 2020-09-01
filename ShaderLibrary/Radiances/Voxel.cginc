#ifndef VXGI_SHADERLIBRARY_RADIANCES_VOXEL
#define VXGI_SHADERLIBRARY_RADIANCES_VOXEL

#include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Variables.cginc"
#include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Utilities.cginc"
#include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Visibility.cginc"
#include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Radiances/Sampler.cginc"
#include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Structs/VoxelLightingData.hlsl"
#include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Radiances/Raytracing.cginc"
#include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Noise.cginc"

float3 DirectVoxelRadiance(VoxelLightingData data)
{
  float level = MinSampleLevel(data.voxelPosition);
  float voxelSize = VoxelSize(level);
  float3 radiance = 0.0;

  for (uint i = 0; i < LightCount; i++) {
    LightSource lightSource = LightSources[i];

    bool notInRange;
    float3 localPosition;

    [branch]
    if (lightSource.type == LIGHT_SOURCE_TYPE_DIRECTIONAL) {
      localPosition = -lightSource.direction;
      notInRange = false;
      lightSource.voxelPosition = mad(1.732, localPosition, data.voxelPosition);
    } else {
      localPosition = lightSource.worldposition - data.worldPosition;
      notInRange = lightSource.NotInRange(localPosition);
    }

    data.Prepare(normalize(localPosition));

    float spotFalloff = lightSource.SpotFalloff(-data.vecL);

    if (notInRange || (spotFalloff <= 0.0) || (data.NdotL <= 0.0)) continue;

    float influence = //VoxelVisibility(mad(2.0 * voxelSize, data.vecN, data.voxelPosition), lightSource.voxelPosition) * 
      data.NdotL
      * spotFalloff
      * lightSource.Attenuation(localPosition);

    if (influence > 0)
    {
      float shadow = 0;
      raycastResult raycast = VoxelRaycastBias(data.worldPosition, normalize(data.vecL), normalize(data.vecN), 40, lightSource.type == LIGHT_SOURCE_TYPE_DIRECTIONAL ? 0 : (distance(data.voxelPosition, lightSource.voxelPosition) * 256));
      shadow = (raycast.distlimit || raycast.sky) ? 1 : 0;

      influence *= shadow;

      radiance += influence * lightSource.color;
    }
  }

  return radiance;
}

float3 IndirectVoxelRadiance(VoxelLightingData data)
{
  if (TextureSDF(data.voxelPosition) < 0.0) return 0.0;

  float3 radiance = StratifiedHemisphereSample(data.worldPosition, normalize(data.vecN), 25, 1, hash(data.worldPosition));

  return radiance;
}

float3 VoxelRadiance(VoxelLightingData data)
{
  return data.color * (DirectVoxelRadiance(data) + IndirectVoxelRadiance(data));
}
#endif // VXGI_SHADERLIBRARY_RADIANCES_VOXEL
