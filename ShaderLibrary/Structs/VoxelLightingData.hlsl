#ifndef VXGI_STRUCTS_VOXEL_LIGHTING_DATA
  #define VXGI_STRUCTS_VOXEL_LIGHTING_DATA

  #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Variables.cginc"

  struct VoxelLightingData
  {
    // Color
    float3 color;

    // Positions
    float3 voxelPosition;
    float3 worldPosition;

    // Vectors
    float3 vecL; // Light
    float3 vecN; // Normal

    // Cosine between vectors
    float NdotL;

    void Initialize()
    {
      worldPosition = mul(VoxelToWorld, float4(voxelPosition, 1.0)).xyz;
    }

    void Prepare(float3 lightDirection) {
      vecL = lightDirection;
      NdotL = saturate(dot(vecN, vecL));
    }
  };
#endif
