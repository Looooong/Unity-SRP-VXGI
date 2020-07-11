#ifndef VXGI_RADIANCES_SAMPLER
#define VXGI_RADIANCES_SAMPLER

#ifdef RADIANCE_POINT_SAMPLER
  #define RADIANCE_SAMPLER point_clamp_sampler
#else
  #define RADIANCE_SAMPLER linear_clamp_sampler
#endif

#include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Variables.cginc"

#ifdef VXGI_CASCADES
  Texture3D Radiance0;

  int MinSampleLevel(float3 position)
  {
    position = mad(position, 2.0, -1.0);
    int3 level = VXGI_CascadesCountMinusOne + ceil(log2(max(abs(position), 0.000001)));
    return max(max(level.x, level.y), max(level.z, 0.0));
  }

  // TODO: do something about the voxel region between 2 adjacent levels
  float SampleOcclusion(float3 position) {
    // int level = MinSampleLevel(position);
    // position.z += level;
    // position.z *= VXGI_CascadesCountRcp;
    return Radiance0.SampleLevel(RADIANCE_SAMPLER, position, 0.0).a;
  }

  // TODO: do something about the voxel region between 2 adjacent levels
  float4 SampleRadiance(float3 position, int level) {
    level = clamp(level, MinSampleLevel(position), VXGI_CascadesCountMinusOne);
    position = mad(
      position,
      exp2(VXGI_CascadesCountMinusOne - level),
      0.5 - exp2(VXGI_CascadesCountMinusOne - level - 1)
    );
    position.z += level;
    position.z /= VXGI_CascadesCount;
    return Radiance0.SampleLevel(RADIANCE_SAMPLER, position, 0.0);
  }
#else
  #define LERP_RADIANCE(level, position, fraction) lerp(SAMPLE_RADIANCE(level - 1, position), SAMPLE_RADIANCE(level, position), fraction)
  #define SAMPLE_RADIANCE(level, position) Radiances[level].SampleLevel(RADIANCE_SAMPLER, position, 0.0)

  Texture3D Radiance0;
  Texture3D Radiance1;
  Texture3D Radiance2;
  Texture3D Radiance3;
  Texture3D Radiance4;
  Texture3D Radiance5;
  Texture3D Radiance6;
  Texture3D Radiance7;
  Texture3D Radiance8;
  static Texture3D Radiances[9] = {
    Radiance0,
    Radiance1,
    Radiance2,
    Radiance3,
    Radiance4,
    Radiance5,
    Radiance6,
    Radiance7,
    Radiance8,
  };

  float SampleLevel(float size)
  {
    return size <= 1.0 ? size : log2(size) + 1;
  }

  float SampleOcclusion(float3 position) {
    return Radiance0.SampleLevel(RADIANCE_SAMPLER, position, 0.0).a;
  }

  float4 SampleRadiance(float3 position, float size)
  {
    float level = clamp(SampleLevel(size), 0.0, 8.999999);
    uint levelFloor = level;

    if (level <= 1.0) {
      return lerp(0.0, SAMPLE_RADIANCE(0, position), level);
    } else {
      switch (levelFloor) {
      case 1:
        return LERP_RADIANCE(1, position, frac(level));
      case 2:
        return LERP_RADIANCE(2, position, frac(level));
      case 3:
        return LERP_RADIANCE(3, position, frac(level));
      case 4:
        return LERP_RADIANCE(4, position, frac(level));
      case 5:
        return LERP_RADIANCE(5, position, frac(level));
      case 6:
        return LERP_RADIANCE(6, position, frac(level));
      case 7:
        return LERP_RADIANCE(7, position, frac(level));
      default:
        return LERP_RADIANCE(8, position, frac(level));
      }
    }
  }
#endif // VXGI_CASCADE
#endif
