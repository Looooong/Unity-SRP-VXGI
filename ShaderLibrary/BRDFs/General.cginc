#ifndef VXGI_BRDFS_GENERAL
  #define VXGI_BRDFS_GENERAL

  #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/BRDFs/Diffuse.cginc"
  #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/BRDFs/Specular.cginc"
  #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Structs/LightingData.cginc"

  float3 GeneralBRDF(LightingData data)
  {
    return DiffuseBRDF(data) + SpecularBRDF(data);
  }
#endif
