#ifndef VXGI_RADIANCES_SAMPLER
#define VXGI_RADIANCES_SAMPLER

#ifdef RADIANCE_POINT_SAMPLER
  #define RADIANCE_SAMPLER point_clamp_sampler
#else
  #define RADIANCE_SAMPLER linear_clamp_sampler
#endif

#include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Variables.cginc"
Texture3D Radiance0;

int MinSampleLevel(float3 position) {
  position = mad(position, 2.0, -1.0);
  int3 level = VXGI_CascadesCountMinusOne + ceil(log2(max(abs(position), 0.000001)));
  return min(max(max(level.x, level.y), max(level.z, 0.0)), VXGI_CascadesCountMinusOne);
}

float HalfVoxelSize(int level) {
  return exp2(level - VXGI_CascadesCount) / Resolution;
}

float VoxelSize(int level) {
  return exp2(level - VXGI_CascadesCountMinusOne) / Resolution;
}

// Normalized cascade coordinate to normalized texture coordinate based on cascade level
float3 TransformCascadeToTexelPosition(float3 position, int level) {
  position.z = clamp(position.z, 0.5 * VXGI_VoxelResolutionRcp, 1.0 - 0.5 * VXGI_VoxelResolutionRcp);
  position.z += level;
  position.z *= VXGI_CascadesCountRcp;
  return position;
}

// Normalized cascade coordinate to normalized voxel coordinate based on cascade level
float3 TransformCascadeToVoxelPosition(float3 position, int level) {
  return mad(position - 0.5, exp2(level - VXGI_CascadesCountMinusOne), 0.5);
}

// Normalized voxel coordinate to normalized cascade coordinate based on cascade level
float3 TransformVoxelToCascadePosition(float3 position, int level) {
  return mad(position - 0.5, exp2(VXGI_CascadesCountMinusOne - level), 0.5);
}

// Normalized voxel coordinate to normalized texture coordinate based on cascade level
float3 TransformVoxelToTexelPosition(float3 position, int level) {
  position = TransformVoxelToCascadePosition(position, level);
  position = TransformCascadeToTexelPosition(position, level);
  return position;
}

float SampleOcclusion(float3 position) {
  int level = MinSampleLevel(position);
  return Radiance0.SampleLevel(RADIANCE_SAMPLER, TransformVoxelToTexelPosition(position, level), 0.0).a;
}

float4 SampleRadiance(float3 position, int level) {
  level = clamp(level, MinSampleLevel(position), VXGI_CascadesCountMinusOne);
  return Radiance0.SampleLevel(RADIANCE_SAMPLER, TransformVoxelToTexelPosition(position, level), 0.0);
}

#ifdef VXGI_ANISOTROPIC_VOXEL
float3 TransformVoxelToTexelPosition(float3 position, int level, int face) {
  position = TransformVoxelToCascadePosition(position, level);
  position = TransformCascadeToTexelPosition(position, level);
  position.y = clamp(position.y, 0.5 * VXGI_VoxelResolutionRcp, 1.0 - 0.5 * VXGI_VoxelResolutionRcp);
  position.y += face;
  position.y /= 6;
  return position;
}

float SampleOcclusion(float3 position, float3 direction) {
  float3 directionSqr = direction * direction;
  int3 isNegative = direction < 0.0;
  int level = MinSampleLevel(position);

  return directionSqr.x * Radiance0.SampleLevel(RADIANCE_SAMPLER, TransformVoxelToTexelPosition(position, level, 0 + isNegative.x), 0.0).a +
          directionSqr.y * Radiance0.SampleLevel(RADIANCE_SAMPLER, TransformVoxelToTexelPosition(position, level, 2 + isNegative.y), 0.0).a +
          directionSqr.z * Radiance0.SampleLevel(RADIANCE_SAMPLER, TransformVoxelToTexelPosition(position, level, 4 + isNegative.z), 0.0).a;
}


float4 SampleRadiance(float3 position, int level, float3 direction) {
  float3 directionSqr = direction * direction;
  int3 isNegative = direction < 0.0;
  level = clamp(level, MinSampleLevel(position), VXGI_CascadesCountMinusOne);

  return directionSqr.x * Radiance0.SampleLevel(RADIANCE_SAMPLER, TransformVoxelToTexelPosition(position, level, 0 + isNegative.x), 0.0) +
          directionSqr.y * Radiance0.SampleLevel(RADIANCE_SAMPLER, TransformVoxelToTexelPosition(position, level, 2 + isNegative.y), 0.0) +
          directionSqr.z * Radiance0.SampleLevel(RADIANCE_SAMPLER, TransformVoxelToTexelPosition(position, level, 4 + isNegative.z), 0.0);
}
#else

float4 SampleRadiance(float3 position, int level, float3 direction) {
  return SampleRadiance(position, level);
}
#endif
#endif
