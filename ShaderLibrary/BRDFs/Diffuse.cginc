#ifndef VXGI_BRDFS_DIFFUSE
  #define VXGI_BRDFS_DIFFUSE

  #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Utilities.cginc"
  #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Structs/LightingData.cginc"

  // Schlick Fresnel
  float DiffuseFresnel(LightingData data)
  {
    // Diffuse Fresnel at 90 degrees minus 1
    float FD90m1 = mad(2.0 * data.roughness, data.LdotH * data.LdotH, -0.5);
    return mad(FD90m1, Pow5(1.0 - data.NdotL), 1.0) * mad(FD90m1, Pow5(1.0 - data.NdotV), 1.0);
  }

  float3 DiffuseBRDF(LightingData data)
  {
    return data.diffuseColor * DiffuseFresnel(data);
  }
#endif
