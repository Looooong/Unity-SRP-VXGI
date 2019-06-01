#ifndef SHADERS_BASIC_VARIABLES
#define SHADERS_BASIC_VARIABLES

CBUFFER_START(UnityPerMaterial)
  half4 _Color;
  float4 _MainTex_ST;
  half _Glossiness;
  half _GlossMapScale;
  half _SmoothnessTextureChannel;
  half _Metallic;
  half _BumpScale;
  half3 _EmissionColor;
CBUFFER_END

sampler2D _MainTex;
sampler2D _MetallicGlossMap;
sampler2D _BumpMap;
sampler2D _EmissionMap;

#endif
