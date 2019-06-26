Shader "Hidden/VXGI/Lighting"
{
  Properties
  {
    _MainTex("Screen", 2D) = "white" {}
  }

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

      #include "UnityCG.cginc"
      #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/BlitSupport.hlsl"

      sampler2D Emission;

      float3 frag(BlitInput i) : SV_TARGET
      {
        return tex2D(Emission, i.uv);
      }
      ENDHLSL
    }

    Pass
    {
      Name "DirectDiffuseSpecular"

      HLSLPROGRAM
      #pragma vertex BlitVertex
      #pragma fragment frag
      #pragma multi_compile __ TRACE_SUN

      #include "UnityCG.cginc"
      #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/BlitSupport.hlsl"
      #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Radiances/Pixel.cginc"

      float4x4 ClipToWorld;
      Texture2D<float> Depth;
      Texture2D<float3> Diffuse;
      Texture2D<float2> Specular;
      Texture2D<float3> Normal;

      float3 frag(BlitInput i) : SV_TARGET
      {
        float depth = Depth.Sample(point_clamp_sampler, i.uv).r;

        if (Linear01Depth(depth) >= 1.0) return 0.0;

        LightingData data;

        float4 worldPosition = mul(ClipToWorld, float4(mad(2.0, i.uv, -1.0), DEPTH_TO_CLIP_Z(depth), 1.0));
        data.worldPosition = worldPosition.xyz / worldPosition.w;

        data.baseColor = Diffuse.Sample(point_clamp_sampler, i.uv);

        float2 specular = Specular.Sample(point_clamp_sampler, i.uv);
        data.glossiness = specular.r;
        data.metallic = specular.g;

        data.vecN = mad(Normal.Sample(point_clamp_sampler, i.uv), 2.0, -1.0);
        data.vecV = normalize(_WorldSpaceCameraPos - data.worldPosition);

        data.Initialize();

        return DirectPixelRadiance(data);
      }
      ENDHLSL
    }

    Pass
    {
      Name "IndirectDiffuse"

      HLSLPROGRAM
      #pragma vertex BlitVertex
      #pragma fragment frag

      #include "UnityCG.cginc"
      #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/BlitSupport.hlsl"
      #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Radiances/Pixel.cginc"

      Texture2D<float> Depth;
      Texture2D<float3> Diffuse;
      Texture2D<float3> Normal;
      float4x4 ClipToVoxel;

      float3 frag(BlitInput i) : SV_TARGET
      {
        float depth = Depth.Sample(point_clamp_sampler, i.uv).r;

        if (Linear01Depth(depth) >= 1.0) return 0.0;

        float4 voxel = mul(ClipToVoxel, float4(mad(2.0, i.uv, -1.0), DEPTH_TO_CLIP_Z(depth), 1.0));
        float3 position = voxel.xyz / voxel.w;

        float3 normal = normalize(mad(Normal.Sample(point_clamp_sampler, i.uv), 2.0, -1.0));

        float3 color = Diffuse.Sample(point_clamp_sampler, i.uv);
        return color * IndirectDiffusePixelRadiance(position, normal);
      }
      ENDHLSL
    }

    Pass
    {
      Name "IndirectSpecular"

      HLSLPROGRAM
      #pragma vertex BlitVertex
      #pragma fragment frag

      #include "UnityCG.cginc"
      #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/BlitSupport.hlsl"
      #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Radiances/Pixel.cginc"

      float4x4 ClipToWorld;
      Texture2D<float> Depth;
      Texture2D<float3> Diffuse;
      Texture2D<float2> Specular;
      Texture2D<float3> Normal;

      float3 frag(BlitInput i) : SV_TARGET
      {
        float depth = Depth.Sample(point_clamp_sampler, i.uv).r;

        if (Linear01Depth(depth) >= 1.0) return 0.0;

        LightingData data;

        float4 worldPosition = mul(ClipToWorld, float4(mad(2.0, i.uv, -1.0), DEPTH_TO_CLIP_Z(depth), 1.0));
        data.worldPosition = worldPosition.xyz / worldPosition.w;

        data.baseColor = Diffuse.Sample(point_clamp_sampler, i.uv);

        float2 specular = Specular.Sample(point_clamp_sampler, i.uv);
        data.glossiness = specular.r;
        data.metallic = specular.g;

        data.vecN = normalize(mad(Normal.Sample(point_clamp_sampler, i.uv), 2.0, -1.0));
        data.vecV = normalize(_WorldSpaceCameraPos - data.worldPosition);

        data.Initialize();

        return IndirectSpecularPixelRadiance(data);
      }
      ENDHLSL
    }

    Pass
    {
      Name "SphericalHarmonics"

      HLSLPROGRAM
      #pragma vertex BlitVertex
      #pragma fragment frag

      #include "UnityCG.cginc"
      #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/BlitSupport.hlsl"

      sampler2D Normal;

      float3 frag(BlitInput i) : SV_TARGET
      {
        return ShadeSH9(float4(mad(tex2D(Normal, i.uv).rgb, 2.0, -1.0), 1.0));
      }
      ENDHLSL
    }
  }
}
