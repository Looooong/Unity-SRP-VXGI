#ifndef VXGI_VISIBILITY
  #define VXGI_VISIBILITY

  #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Variables.cginc"
  #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Utilities.cginc"
  #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Radiances/Sampler.cginc"

  // Calculate visibility between 2 points in voxel space
  float VoxelVisibility(float3 p1, float3 p2)
  {
    float occlusion = 0.0;
    float3 direction = normalize(p2 - p1);
    float3 step = direction / Resolution;

    for (
      float3 coordinate = p1 / Resolution + step;
      occlusion < 1.0 && TextureSDF(coordinate) > 0.0 && dot(mad(-coordinate, Resolution, p2), direction) > 0.5;
      coordinate += 0.5 * step
    ) {
      occlusion += (1.0 - occlusion) * SampleOcclusion(coordinate, 1.0);
    }

    return 1.0 - occlusion;
  }
#endif
