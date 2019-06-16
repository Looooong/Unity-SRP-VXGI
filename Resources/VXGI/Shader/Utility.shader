Shader "Hidden/VXGI/Utility"
{
  Properties
  {
    _MainTex("Screen", 2D) = "white" {}
  }

  SubShader
  {
    ZWrite Off

    Pass
    {
      Name "DepthCopy"

      BlendOp Min, Max
      ZWrite On

      HLSLPROGRAM
      #pragma vertex BlitVertex
      #pragma fragment frag

      #include "UnityCG.cginc"
      #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/BlitSupport.hlsl"

      sampler2D Depth;

      float4 frag(BlitInput i, out float depth : SV_DEPTH) : SV_TARGET
      {
        depth = tex2D(Depth, i.uv).r;

        if (Linear01Depth(depth) < 1.0) {
          return float4(0.0, 0.0, 0.0, 1.0);
        } else {
          return 1.0;
        }
      }
      ENDHLSL
    }

    Pass
    {
      Name "LowResComposite"

      Blend One One

      HLSLPROGRAM
      #pragma target 4.5
      #pragma vertex BlitVertex
      #pragma fragment frag

      #include "UnityCG.cginc"
      #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/BlitSupport.hlsl"

      Texture2D<float> Depth;
      Texture2D<float3> LowResColor;
      Texture2D<float> LowResDepth;
      SamplerState point_clamp_sampler;
      SamplerState linear_clamp_sampler;
      float4 LowResColor_TexelSize;

      static int2 GatherOffsets[4] = {
        int2(0, 1),
        int2(1, 1),
        int2(1, 0),
        int2(0, 0)
      };

      float3 frag(BlitInput i) : SV_TARGET
      {
        float depth = LinearEyeDepth(Depth.Sample(point_clamp_sampler, i.uv));
        float4 neighbors = LowResDepth.Gather(point_clamp_sampler, i.uv);
        float4 distances;

        float minDist = distances[0] = distance(depth, LinearEyeDepth(neighbors[0]));
        float minIndex = 0;

        [unroll]
        for (int index = 1; index < 4; index++) {
          distances[index] = distance(depth, LinearEyeDepth(neighbors[index]));

          if (distances[index] < minDist) {
            minDist = distances[index];
            minIndex = index;
          }
        }

        if (all(distances < 0.1)) {
          return LowResColor.Sample(linear_clamp_sampler, i.uv);
        } else {
          return LowResColor.Load(int3(mad(i.uv, LowResColor_TexelSize.zw, -0.5) + GatherOffsets[minIndex], 0));
        }
      }
      ENDHLSL
    }
  }
}
