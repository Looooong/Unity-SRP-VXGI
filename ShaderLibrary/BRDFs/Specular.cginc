#ifndef VXGI_BRDFS_SPECULAR
  #define VXGI_BRDFS_SPECULAR

  #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Utilities.cginc"
  #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Structs/LightingData.cginc"

  // Trowbridge-Reitz NDF
  float SpecularNDF(LightingData data)
  {
    float roughness2 = data.roughness * data.roughness;
    float distribution = mad(data.NdotH * data.NdotH, roughness2 - 1.0, 1.0);
    return roughness2 / (distribution * distribution);
  }

  // Schlick-GGX GSF
  float SpecularGSF(LightingData data)
  {
    float k = 0.5 * data.roughness;
    return data.NdotL * data.NdotV / (mad(data.NdotL, 1.0 - k, k) * mad(data.NdotV, 1.0 - k, k));
  }

  // Schlick Fresnel
  float3 SpecularFresnel(LightingData data)
  {
    return mad(Pow5(1.0 - data.LdotH), 1.0 - data.specularColor, data.specularColor);
  }

  float3 SpecularBRDF(LightingData data)
  {
    return SpecularNDF(data) * SpecularGSF(data) * SpecularFresnel(data) / (4.0 * data.NdotL * data.NdotV);
  }
#endif
