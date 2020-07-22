#ifndef VXGI_SHADERLIBRARY_VARIABLES
#define VXGI_SHADERLIBRARY_VARIABLES

#include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Structs/LightSource.hlsl"

uint Resolution;
uint LightCount;
float4x4 VoxelToWorld;
float4x4 WorldToVoxel;
SamplerState point_clamp_sampler;
SamplerState linear_clamp_sampler;
StructuredBuffer<LightSource> LightSources;

static float ConeDirectionThreshold = sin(atan(1.0/3.0));

// Cone tracing direction for indirect diffuse calculations
static float3 Directions[32] = {
  float3(+0.000000, +1.000000, +0.000000),
  float3(+0.000000, -1.000000, +0.000000),
  float3(+0.000000, +0.187595, -0.982245),
  float3(+0.000000, -0.187595, +0.982245),
  float3(+0.000000, +0.745356, -0.666656),
  float3(+0.000000, -0.745356, +0.666656),
  float3(+0.000000, +0.794654, +0.607060),
  float3(+0.000000, -0.794654, -0.607060),
  float3(+0.356818, +0.333334, +0.872681),
  float3(+0.356818, -0.333334, -0.872681),
  float3(-0.356818, +0.333334, +0.872681),
  float3(-0.356818, -0.333334, -0.872681),
  float3(+0.525736, +0.794654, -0.303524),
  float3(+0.525736, -0.794654, +0.303524),
  float3(-0.525736, +0.794654, -0.303524),
  float3(-0.525736, -0.794654, +0.303524),
  float3(+0.577350, +0.333334, -0.745356),
  float3(+0.577350, -0.333334, +0.745356),
  float3(-0.577350, +0.333334, -0.745356),
  float3(-0.577350, -0.333334, +0.745356),
  float3(+0.577350, +0.745356, +0.333334),
  float3(+0.577350, -0.745356, -0.333334),
  float3(-0.577350, +0.745356, +0.333334),
  float3(-0.577350, -0.745356, -0.333334),
  float3(+0.850648, +0.187595, +0.491129),
  float3(+0.850648, -0.187595, -0.491129),
  float3(-0.850648, +0.187595, +0.491129),
  float3(-0.850648, -0.187595, -0.491129),
  float3(+0.934174, +0.333334, -0.127319),
  float3(+0.934174, -0.333334, +0.127319),
  float3(-0.934174, +0.333334, -0.127319),
  float3(-0.934174, -0.333334, +0.127319),
};

static float3 VXGI_VoxelSizes[] = {
  1.0 / Resolution,
  2.0 / Resolution,
  4.0 / Resolution,
  8.0 / Resolution,
  16.0 / Resolution,
  32.0 / Resolution,
  64.0 / Resolution,
  128.0 / Resolution,
  256.0 / Resolution
};
#endif // VXGI_SHADERLIBRARY_VARIABLES
