Shader "Hidden/VXGI/Voxelization"
{
  SubShader
  {
    Pass
    {
      Name "VOXELIZATION"
      Tags { "LightMode"="Voxelization" }

      Cull Off
      ZWrite Off
      ZTest Always

      HLSLPROGRAM
      #pragma require geometry
      #pragma require randomwrite
      #pragma vertex vert
      #pragma geometry geom
      #pragma fragment frag
      #pragma multi_compile _ VXGI_CASCADES
      #pragma multi_compile _ VXGI_ANISOTROPIC_VOXEL
      #pragma shader_feature _EMISSION
      #pragma shader_feature_local _METALLICGLOSSMAP
      #pragma shader_feature_local _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON

      #include "UnityCG.cginc"
      #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Variables.cginc"
      #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Structs/VoxelData.cginc"
      #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Radiances/Voxel.cginc"

      #define AXIS_X 0
      #define AXIS_Y 1
      #define AXIS_Z 2

      CBUFFER_START(UnityPerMaterial)
        half4 _Color;
        float4 _MainTex_ST;
        half _Metallic;
        half3 _EmissionColor;
      CBUFFER_END

      sampler2D _MainTex;
      sampler2D _MetallicGlossMap;
      sampler2D _EmissionMap;

      uint VXGI_CascadeIndex;
      RWStructuredBuffer<VoxelData> VoxelBuffer;
      RWTexture3D<int> VoxelPointerBuffer;


#ifdef VXGI_CASCADES
      static uint3 VXGI_TexelResolution = uint3(Resolution, Resolution, Resolution * VXGI_CascadesCount);
#else
      static uint3 VXGI_TexelResolution = Resolution;
#endif

      static float3 VXGI_DoubleTexelResolution = 2.0 * VXGI_TexelResolution;
      static float3 VXGI_DoubleTexelResolutionMinus = 2.0 * VXGI_TexelResolution - 0.000001;
      static float3 VXGI_TexelResolutionMinus = VXGI_TexelResolution - 0.000001;

      struct v2g
      {
        float4 vertex : POSITION;
        float3 normal : NORMAL;
        float2 uv : TEXCOORD;
      };

      struct g2f
      {
        float4 position : SV_POSITION;
        float3 normal : NORMAL;
        float2 uv : TEXCOORD0;
        float axis : TEXCOORD1; // Projection axis
      };

      v2g vert(appdata_base v)
      {
        v2g o;
        o.vertex = UnityObjectToClipPos(v.vertex);
        o.normal = UnityObjectToWorldNormal(v.normal);
        o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);

#ifdef UNITY_REVERSED_Z
        o.vertex.z = mad(o.vertex.z, -2.0, 1.0);
#endif

        return o;
      }

      // Swap coordinate axis for largest projection area
      float3 SwizzleAxis(float3 position, uint axis) {
        // Method 1:
        // switch (axis) {
        // case AXIS_X:
        // 	position = position.yzx;
        // 	break;
        // case AXIS_Y:
        // 	position = position.zxy;
        // 	break;
        // }

        // Method 2: Is it faster?
        uint a = axis + 1;
        float3 p = position;
        position.x = p[(0 + a) % 3];
        position.y = p[(1 + a) % 3];
        position.z = p[(2 + a) % 3];

        return position;
      }

      [maxvertexcount(3)]
      void geom(triangle v2g i[3], inout TriangleStream<g2f> triStream)
      {
        float3 normal = normalize(abs(cross(i[1].vertex - i[0].vertex, i[2].vertex - i[0].vertex)));
        uint axis = AXIS_Z;

        // Choose an axis with the largest projection area
        if (normal.x > normal.y && normal.x > normal.z) {
          axis = AXIS_X;
        } else if (normal.y > normal.x && normal.y > normal.z) {
          axis = AXIS_Y;
        }

        [unroll]
        for (int j = 0; j < 3; j++) {
          g2f o;

          o.position = float4(SwizzleAxis(i[j].vertex, axis), 1.0);

#ifdef UNITY_UV_STARTS_AT_TOP
          o.position.y = -o.position.y;
#endif

#ifdef UNITY_REVERSED_Z
          o.position.z = mad(o.position.z, 0.5, 0.5);
#endif

          o.normal = i[j].normal;
          o.axis = axis;
          o.uv = i[j].uv;

          triStream.Append(o);
        }
      }

      // Restore coordinate axis back to correct position
      float3 RestoreAxis(float3 position, uint axis) {
        // Method 1:
        // switch (axis) {
        // case AXIS_X:
        // 	position = position.zxy;
        // 	break;
        // case AXIS_Y:
        // 	position = position.yzx;
        // 	break;
        // }

        // Method 2: Is it faster?
        uint a = 2 - axis;
        float3 p = position;
        position.x = p[(0 + a) % 3];
        position.y = p[(1 + a) % 3];
        position.z = p[(2 + a) % 3];

        return position;
      }


      float4 CalculateLitColor(float3 position, float3 normal, float4 color, float3 emission)
      {
        VoxelLightingData lightingData;
        lightingData.color = color.rgb;
        lightingData.voxelPosition = position;
        lightingData.vecN = normal;
        lightingData.Initialize();

        color.rgb = emission + VoxelRadiance(lightingData);
        color.a = saturate(color.a);

        return color;
      }

      float4 Premul(float4 col)
      {
        return float4(col.rgb * col.a, col.a);
      }

      fixed frag(g2f i) : SV_TARGET
      {
        #ifdef _METALLICGLOSSMAP
          float metallic = tex2D(_MetallicGlossMap, i.uv).r;
        #else
          float metallic = _Metallic;
        #endif

        i.normal = normalize(i.normal);

        #ifdef _EMISSION
          float3 emission = _EmissionColor * tex2Dlod(_EmissionMap, float4(i.uv, 0.0, 0.0));
        #else
          float3 emission = 0.0;
        #endif
        emission += ShadeSH9(float4(i.normal, 1.0));

        float3 voxelPosition = RestoreAxis(float3(i.position.xy / Resolution, i.position.z), i.axis);

        float4 color = mad(-0.5, metallic, 1.0) * _Color * tex2Dlod(_MainTex, float4(i.uv, 0.0, 0.0));

        VoxelData d;
        d.Initialize();
        d.SetPosition(voxelPosition);
        d.SetNormal(i.normal);

        float3 position = voxelPosition;
#ifdef VXGI_CASCADES
        int level = VXGI_CascadeIndex;
        position = TransformCascadeToVoxelPosition(position, level);

#else
        position *= Resolution;
#endif
        float4 finalColor = CalculateLitColor(position, i.normal, color, emission);
#if defined(_ALPHABLEND_ON) || defined(_ALPHAPREMULTIPLY_ON)
        finalColor = Premul(finalColor);
#else
        finalColor.a = 1;
#endif
        d.SetColor(finalColor);


#ifdef VXGI_CASCADES
        position = TransformVoxelToTexelPosition(position, level);
        position = min(VXGI_TexelResolution * position, VXGI_TexelResolutionMinus);
#else
        position = min(position, VXGI_TexelResolutionMinus);
#endif

        uint prevCounter = 0;
        uint counter = VoxelBuffer.IncrementCounter();

        InterlockedExchange(VoxelPointerBuffer[position], counter, prevCounter);
        d.SetPointer(prevCounter);

        VoxelBuffer[counter] = d;

        return 0.0;
      }
      ENDHLSL
    }
  }

  Fallback Off
}
