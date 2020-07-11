#ifndef VXGI_VARIABLES
  #define VXGI_VARIABLES

  #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Structs/LightSource.hlsl"

  float VXGI_VolumeExtent;
  float VXGI_VolumeSize;
  float3 VXGI_VolumeCenter;
  int VXGI_CascadesCount;
  uint Resolution;
  uint LightCount;
  float4x4 VoxelToWorld;
  float4x4 WorldToVoxel;
  SamplerState point_clamp_sampler;
  SamplerState linear_clamp_sampler;
  StructuredBuffer<LightSource> LightSources;

  static float ConeDirectionThreshold = sin(atan(1.0/3.0));
  static float VXGI_CascadesCountRcp = 1.0 / VXGI_CascadesCount;
  static float VXGI_VolumeSizeRcp = 1.0 / VXGI_VolumeSize;
  static float3 VXGI_VolumeMin = VXGI_VolumeCenter - VXGI_VolumeExtent;
  static int VXGI_CascadesCountMinusOne = VXGI_CascadesCount - 1;

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
#endif
