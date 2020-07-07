#ifndef VXGI_BLIT_SUPPORT_HLSL
#define VXGI_BLIT_SUPPORT_HLSL

#if defined(UNITY_REVERSED_Z)
  #define DEPTH_TO_CLIP_Z(depth) depth
#else
  #define DEPTH_TO_CLIP_Z(depth) mad(2.0, depth, -1.0)
#endif

struct BlitInput
{
  float4 vertex : SV_POSITION;
  float2 uv : TEXCOORD;
};

float4 BlitViewport;

BlitInput BlitVertex(appdata_base v)
{
  BlitInput o;
  o.vertex = UnityObjectToClipPos(v.vertex);
  o.uv = v.texcoord;
  return o;
}

BlitInput BlitViewportVertex(appdata_base v)
{
  v.vertex.xy = mad(v.vertex.xy, BlitViewport.xy, BlitViewport.zw);

  BlitInput o;
  o.vertex = UnityObjectToClipPos(v.vertex);
  o.uv = v.texcoord;
  return o;
}

#endif
