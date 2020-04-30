Shader "Hidden/VXGI/Visualization"
{
  Properties
  {
    _MainTex("Albedo", 2D) = "white" {}
  }

  SubShader
  {
    Pass
    {
      Name "Mipmap"

      Blend One OneMinusSrcAlpha
      ZTest Always
      ZWrite Off

      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile __ RADIANCE_POINT_SAMPLER

      #include "UnityCG.cginc"
      #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Utilities.cginc"
      #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Radiances/Sampler.cginc"

      struct v2f
      {
        float4 position : SV_POSITION;
        float3 view : POSITION1;
      };

      // 6 inner faces of a cube
      static float3 Vertices[24] = {
        float3(0.0, 0.0, 0.0),
        float3(1.0, 0.0, 0.0),
        float3(1.0, 1.0, 0.0),
        float3(0.0, 1.0, 0.0),

        float3(0.0, 0.0, 0.0),
        float3(0.0, 1.0, 0.0),
        float3(0.0, 1.0, 1.0),
        float3(0.0, 0.0, 1.0),

        float3(0.0, 0.0, 0.0),
        float3(0.0, 0.0, 1.0),
        float3(1.0, 0.0, 1.0),
        float3(1.0, 0.0, 0.0),

        float3(1.0, 1.0, 1.0),
        float3(1.0, 0.0, 1.0),
        float3(0.0, 0.0, 1.0),
        float3(0.0, 1.0, 1.0),

        float3(1.0, 1.0, 1.0),
        float3(1.0, 1.0, 0.0),
        float3(1.0, 0.0, 0.0),
        float3(1.0, 0.0, 1.0),

        float3(1.0, 1.0, 1.0),
        float3(0.0, 1.0, 1.0),
        float3(0.0, 1.0, 0.0),
        float3(1.0, 1.0, 0.0),
      };

      static float DitherPattern[4][4] = {
        0.0000, 0.5000, 0.1250, 0.6250,
        0.7500, 0.2200, 0.8750, 0.3750,
        0.1875, 0.6875, 0.0625, 0.5625,
        0.9375, 0.4375, 0.8125, 0.3125
      };

      float MipmapLevel;
      float RayTracingStep;
      static float MipmapSize = MipmapLevel < 1.0 ? MipmapLevel : pow(2, MipmapLevel - 1.0);

      v2f vert(uint id : SV_VertexID)
      {
        float3 v = Vertices[id];

        v2f o;
        o.position = UnityObjectToClipPos(v);
        o.view = UnityObjectToViewPos(v);
        return o;
      }

      half4 frag(v2f i) : SV_TARGET
      {
        float3 view = i.view;
        float3 unit = view * RayTracingStep / view.z;
        view += unit * DitherPattern[i.position.x % 4][i.position.y % 4];
        float3 coordinate = mul(transpose(UNITY_MATRIX_IT_MV), float4(view, 1.0));

        half4 color = half4(0.0, 0.0, 0.0, 0.0);

        while ((view.z <= 2 * RayTracingStep) && (TextureSDF(coordinate) > -0.000001)) {
          half4 sample = SampleRadiance(coordinate, MipmapSize);
          color = sample + color * (1 - sample.a);
          view += unit;
          coordinate = mul(transpose(UNITY_MATRIX_IT_MV), float4(view, 1.0));
        }

        return color;
      }

      ENDCG
    }
  }

  Fallback Off
}
