#ifndef VXGI_STRUCTS_LIGHTING_DATA
  #define VXGI_STRUCTS_LIGHTING_DATA

  #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Variables.cginc"

  struct LightingData
  {
    // Positions
    float3 voxelPosition;
    float3 worldPosition;

    // Colors
    float3 diffuseColor;
    float3 specularColor;

    // Physical properties
    float glossiness;
    float metallic;
    float perceptualRoughness;
    float roughness;

    // Vectors
    float3 vecH; // Halfway
    float3 vecL; // Light
    float3 vecN; // Normal
    float3 vecR; // Reflect
    float3 vecV; // View

    // Cosines between vectors
    float LdotH;
    float NdotH;
    float NdotL;
    float NdotR;
    float NdotV;

    void Initialize()
    {
      voxelPosition = mul(WorldToVoxel, float4(worldPosition, 1.0)).xyz;

      perceptualRoughness = 1.0 - glossiness;
      roughness = 1.0 - glossiness * glossiness;
      roughness *= roughness;

      vecR = reflect(-vecV, vecN);

      NdotR = saturate(dot(vecN, vecR));
      NdotV = saturate(dot(vecN, vecV));
    }

    void Prepare(float3 lightDirection) {
      vecL = lightDirection;
      vecH = normalize(vecL + vecV);

      LdotH = saturate(dot(vecL, vecH));
      NdotH = saturate(dot(vecN, vecH));
      NdotL = saturate(dot(vecN, vecL));
    }
  };
#endif
