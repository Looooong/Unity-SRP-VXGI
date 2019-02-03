#ifndef VXGI_BRDFS_GENERAL
  #define VXGI_BRDFS_GENERAL

  #include "BRDFs/Diffuse.cginc"
  #include "BRDFs/Specular.cginc"
  #include "Structs/LightingData.cginc"

  float3 GeneralBRDF(LightingData data)
  {
    return DiffuseBRDF(data) + SpecularBRDF(data);
  }
#endif
