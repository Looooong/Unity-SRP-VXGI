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
      Name "BlitViewport"

      HLSLPROGRAM
      #pragma vertex BlitViewportVertex
      #pragma fragment frag

      #include "UnityCG.cginc"
      #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/BlitSupport.hlsl"

      sampler2D _MainTex;

      float4 frag(BlitInput i) : SV_TARGET
      {
        return tex2D(_MainTex, i.uv);
      }
      ENDHLSL
    }

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

      sampler2D _CameraDepthTexture;

      float4 frag(BlitInput i, out float depth : SV_DEPTH) : SV_TARGET
      {
        depth = tex2D(_CameraDepthTexture, i.uv).r;

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
      Name "DepthCopyViewport"

      BlendOp Min, Max
      ZWrite On

      HLSLPROGRAM
      #pragma vertex BlitViewportVertex
      #pragma fragment frag

      #include "UnityCG.cginc"
      #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/BlitSupport.hlsl"

      sampler2D _CameraDepthTexture;

      float4 frag(BlitInput i, out float depth : SV_DEPTH) : SV_TARGET
      {
        depth = tex2D(_CameraDepthTexture, i.uv).r;

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
      Name "EncodeDepthNormal"

      HLSLPROGRAM
      #pragma vertex BlitVertex
      #pragma fragment frag

      #include "UnityCG.cginc"
      #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/BlitSupport.hlsl"

      sampler2D _CameraDepthTexture;
      sampler2D _CameraGBufferTexture2;

      float4 frag(BlitInput i) : SV_TARGET
      {
        return EncodeDepthNormal(
          tex2D(_CameraDepthTexture, i.uv).r,
          normalize(mad(tex2D(_CameraGBufferTexture2, i.uv).rgb, 2.0, -1.0))
        );
      }
      ENDHLSL
    }

    Pass
    {
      Name "GrabCopy"

      HLSLPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile _ PROJECTION_PARAMS_X

      #include "UnityCG.cginc"

      struct v2f
      {
        float4 position : SV_POSITION;
        float4 uv : TEXCOORD;
      };

      sampler2D _MainTex;
      float4 BlitViewport;

      v2f vert(appdata_base v)
      {
        v.vertex.xy = (v.vertex.xy - BlitViewport.zw) / BlitViewport.xy;

        v2f o;
        o.position = UnityObjectToClipPos(v.vertex);
        o.uv = ComputeScreenPos(o.position);
        o.uv.xy = mad(o.uv.xy, BlitViewport.xy, BlitViewport.zw);

#ifdef PROJECTION_PARAMS_X
        if (_ProjectionParams.x < 0.0) o.uv.y = 1.0 - o.uv.y;
#endif

        return o;
      }

      float4 frag(v2f i) : SV_TARGET
      {
        return tex2Dproj(_MainTex, i.uv);
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

      Texture2D<float> _CameraDepthTexture;
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
        float depth = LinearEyeDepth(_CameraDepthTexture.Sample(point_clamp_sampler, i.uv));
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

  Fallback Off
}
