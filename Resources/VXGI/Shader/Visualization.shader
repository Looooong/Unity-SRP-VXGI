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

      Blend One Zero
      ZTest Always
      ZWrite Off

      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile _ RADIANCE_POINT_SAMPLER
      #pragma multi_compile _ VIEWMODE_CAMERA
      #pragma multi_compile _ VXGI_ANISOTROPIC_VOXEL
      #pragma multi_compile _ LIGHTING_RAYS
      #pragma multi_compile _ SHOW_DEPTH

      #include "UnityCG.cginc"
      #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Utilities.cginc"
      #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Radiances/Sampler.cginc"
      #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Radiances/Raytracing.cginc"

      struct v2f
      {
        float4 position : SV_POSITION;
        float3 view : POSITION1;
        float3 worldPos : POSITION2;
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
      float VXGI_layer;
      float3 VXGI_SampleDirection;
      static float MipmapSize = MipmapLevel < 1.0 ? MipmapLevel : pow(2, MipmapLevel - 1.0);


      v2f vert(uint id : SV_VertexID)
      {
        float3 v = Vertices[id];

        v2f o;
        o.position = UnityObjectToClipPos(v);
        o.view = UnityObjectToViewPos(v);
        o.worldPos = mul(unity_ObjectToWorld, float4(v, 1.0)).xyz;
        return o;
      }

      half4 frag(v2f i) : SV_TARGET
      {
#ifdef VIEWMODE_CAMERA
#ifdef LIGHTING_RAYS
        half4 color = 0;
        float3 rayPos = _WorldSpaceCameraPos;
        float3 rayDir = normalize(i.worldPos - _WorldSpaceCameraPos);
#ifdef SHOW_DEPTH
        color.rgb = VoxelRaycast(rayPos, rayDir, 30, 0).distance/20.0;
#else
        color.rgb = VoxelRaycast(rayPos, rayDir, 30, 0).color.rgb;
#endif
#else
        float3 view = i.view;
        float3 unit = view * RayTracingStep / view.z;
        view += unit * DitherPattern[i.position.x % 4][i.position.y % 4];
        float3 coordinate = mul(transpose(UNITY_MATRIX_IT_MV), float4(view, 1.0));
        half4 color = 0.0;

        while ((view.z <= 2 * RayTracingStep) && (TextureSDF(coordinate) > -0.000001)) {
#ifdef VXGI_ANISOTROPIC_VOXEL
          half4 sample = SampleRadiance(coordinate, MipmapLevel, VXGI_SampleDirection);
#else
          half4 sample = SampleRadiance(coordinate, MipmapLevel);
#endif
          color = sample + color * (1 - sample.a);
          view += unit;
          coordinate = mul(transpose(UNITY_MATRIX_IT_MV), float4(view, 1.0));
        }
#endif
#else //VIEWMODE_CAMERA why is indenting these not allowed :(
        half4 color = pow(float(StepMap.Load(uint4(float2(i.position.xy), VXGI_layer*128,0).xzyw)),1/2.2);
#endif
        return color;
      }

      ENDCG
    }
  }

  Fallback Off
}
