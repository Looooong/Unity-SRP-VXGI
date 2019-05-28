#ifndef VXGI_BRDFS_GENERAL
  #define VXGI_BRDFS_GENERAL

  #include "Packages/com.looooong.srp.vxgi/Runtime/CGIncludes/BRDFs/Diffuse.cginc"
  #include "Packages/com.looooong.srp.vxgi/Runtime/CGIncludes/BRDFs/Specular.cginc"
  #include "Packages/com.looooong.srp.vxgi/Runtime/CGIncludes/Structs/LightingData.cginc"

  float3 GeneralBRDF(LightingData data)
  {
    return DiffuseBRDF(data) + SpecularBRDF(data);
  }
#endif
