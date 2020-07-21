#ifndef VXGI_VISIBILITY
#define VXGI_VISIBILITY

#include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Variables.cginc"
#include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Utilities.cginc"
#include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Radiances/Sampler.cginc"

#ifdef VXGI_CASCADES
  float VoxelVisibility(float3 p1, float3 p2)
  {
    float occlusion = 0.0;
    float stepDistance = distance(p2, p1);
    float stepSize = HalfVoxelSize(MinSampleLevel(p1));
    float3 direction = normalize(p2 - p1);

    for(
      p1 += direction * stepSize;
      occlusion < 1.0 && stepDistance > 0.0;
      stepSize = HalfVoxelSize(MinSampleLevel(p1)), p1 += direction * stepSize, stepDistance -= stepSize
    ) {
      if (TextureSDF(p1) > 0.0) {
#ifdef VXGI_ANISOTROPIC_VOXEL
      occlusion += (1.0 - occlusion) * SampleOcclusion(p1, -direction);
#else // VXGI_ANISOTROPIC_VOXEL
      occlusion += (1.0 - occlusion) * SampleOcclusion(p1);
#endif // VXGI_ANISOTROPIC_VOXEL
      }
    }

    return 1.0 - occlusion;
  }
#else // VXGI_CASCADES
  // Calculate visibility between 2 points in voxel space
  float VoxelVisibility(float3 p1, float3 p2)
  {
    float occlusion = 0.0;
    float3 direction = normalize(p2 - p1);
    float3 step = direction / Resolution;
    p1 /= Resolution;
    p2 /= Resolution;

    for (
      p1 += step;
      occlusion < 1.0 && TextureSDF(p1) > 0.0 && Resolution * dot(p2 - p1, direction) > 0.5;
      p1 += 0.5 * step
    ) {
      occlusion += (1.0 - occlusion) * SampleOcclusion(p1);
    }

    return 1.0 - occlusion;
  }
#endif // VXGI_CASCADES
#endif
