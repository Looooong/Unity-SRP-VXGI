#ifndef VXGI_SHADERLIBRARY_VISIBILITY
#define VXGI_SHADERLIBRARY_VISIBILITY

#include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Variables.cginc"
#include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Utilities.cginc"
#include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Radiances/Sampler.cginc"

// Calculate visibility between 2 points in voxel space
float VXGI_VoxelVisibility(float3 p1, float3 p2)
{
  uint level = 0;
  float opacity = 0.0;
  float stepDistance = distance(p1, p2);
  float stepSize = 0.5 * VXGI_VoxelSizes[level];
  float3 stepDirection = normalize(p2 - p1);
  float3 step = stepSize * stepDirection;

  for (
    p1 += step, stepDistance -= stepSize;
    opacity < 1.0 && stepDistance > 0.0;
    p1 += step, stepDistance -= stepSize
  ) {
    if (TextureSDF(p1) > 0.0) opacity += (1.0 - opacity) * VXGI_VoxelOpacity(p1);
  }

  return 1.0 - opacity;
}
#endif // VXGI_SHADERLIBRARY_VISIBILITY
