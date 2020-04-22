Shader "Hidden/VXGI/Lighting"
{
  Properties
  {
    _MainTex("Screen", 2D) = "white" {}
  }

  HLSLINCLUDE
    #include "UnityCG.cginc"
    #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/BlitSupport.hlsl"
    #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Radiances/Pixel.cginc"

    float4x4 ClipToVoxel;
    float4x4 ClipToWorld;
    Texture2D<float> _CameraDepthTexture;
    Texture2D<float3> _CameraGBufferTexture0;
    Texture2D<float4> _CameraGBufferTexture1;
    Texture2D<float3> _CameraGBufferTexture2;
    Texture2D<float3> _CameraGBufferTexture3;

    LightingData ConstructLightingData(BlitInput i, float depth)
    {
      LightingData data;

      float4 worldPosition = mul(ClipToWorld, float4(mad(2.0, i.uv, -1.0), DEPTH_TO_CLIP_Z(depth), 1.0));
      data.worldPosition = worldPosition.xyz / worldPosition.w;

      float3 gBuffer0 = _CameraGBufferTexture0.Sample(point_clamp_sampler, i.uv);
      float4 gBuffer1 = _CameraGBufferTexture1.Sample(point_clamp_sampler, i.uv);
      float3 gBuffer2 = _CameraGBufferTexture2.Sample(point_clamp_sampler, i.uv);

      data.diffuseColor = gBuffer0;
      data.specularColor = gBuffer1.rgb;
      data.glossiness = gBuffer1.a;

      data.vecN = mad(gBuffer2, 2.0, -1.0);
      data.vecV = normalize(_WorldSpaceCameraPos - data.worldPosition);

      data.Initialize();

      return data;
    }
  ENDHLSL

  SubShader
  {
    Blend One One
    ZWrite Off

    Pass
    {
      Name "Emission"

      HLSLPROGRAM
      #pragma vertex BlitVertex
      #pragma fragment frag
      #pragma multi_compile _ UNITY_HDR_ON

      float3 frag(BlitInput i) : SV_TARGET
      {
        float depth = _CameraDepthTexture.Sample(point_clamp_sampler, i.uv).r;

        if (Linear01Depth(depth) >= 1.0) return 0.0;

        float3 emissiveColor = _CameraGBufferTexture3.Sample(point_clamp_sampler, i.uv);

#ifndef UNITY_HDR_ON
        // Decode value provided by built-in Unity g-buffer generator
        emissiveColor = -log2(emissiveColor);
#endif

        return emissiveColor;
      }
      ENDHLSL
    }

    Pass
    {
      Name "DirectDiffuseSpecular"

      HLSLPROGRAM
      #pragma vertex BlitVertex
      #pragma fragment frag

      float3 frag(BlitInput i) : SV_TARGET
      {
        float depth = _CameraDepthTexture.Sample(point_clamp_sampler, i.uv).r;

        if (Linear01Depth(depth) >= 1.0) return 0.0;

        return DirectPixelRadiance(ConstructLightingData(i, depth));
      }
      ENDHLSL
    }

    Pass
    {
      Name "IndirectDiffuse"

      HLSLPROGRAM
      #pragma vertex BlitVertex
      #pragma fragment frag

      float3 frag(BlitInput i) : SV_TARGET
      {
        float depth = _CameraDepthTexture.Sample(point_clamp_sampler, i.uv).r;

        if (Linear01Depth(depth) >= 1.0) return 0.0;

        return IndirectDiffusePixelRadiance(ConstructLightingData(i, depth));
      }
      ENDHLSL
    }

    Pass
    {
      Name "IndirectSpecular"

      HLSLPROGRAM
      #pragma vertex BlitVertex
      #pragma fragment frag

      float3 frag(BlitInput i) : SV_TARGET
      {
        float depth = _CameraDepthTexture.Sample(point_clamp_sampler, i.uv).r;

        if (Linear01Depth(depth) >= 1.0) return 0.0;

        return IndirectSpecularPixelRadiance(ConstructLightingData(i, depth));
      }
      ENDHLSL
    }
  }

  Fallback Off
}
