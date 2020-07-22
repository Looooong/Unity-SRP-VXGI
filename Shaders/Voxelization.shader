Shader "Hidden/VXGI/Voxelization"
{
  SubShader
  {
    Pass
    {
      Name "VOXELIZATION"
      Tags { "LightMode"="Voxelization" }

      Cull Off
      ZTest Always
      ZWrite Off

      HLSLPROGRAM
      #pragma require geometry
      #pragma require randomwrite
      #pragma vertex vert
      #pragma geometry geom
      #pragma fragment frag
      #pragma shader_feature _EMISSION
      #pragma shader_feature_local _METALLICGLOSSMAP

      #include "UnityCG.cginc"
      #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Variables.cginc"
      #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Structs/VoxelData.cginc"

      #define AXIS_X 0
      #define AXIS_Y 1
      #define AXIS_Z 2

      CBUFFER_START(UnityPerMaterial)
        half4 _Color;
        half3 _EmissionColor;
        float4 _MainTex_ST;
        half _Metallic;
      CBUFFER_END

      sampler2D _EmissionMap;
      sampler2D _MainTex;
      sampler2D _MetallicGlossMap;

      AppendStructuredBuffer<VoxelData> VoxelBuffer;

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

        float3 voxelPosition = float3(i.position.xy, i.position.z * Resolution);

        VoxelData d;
        d.Initialize();
        d.SetPosition(RestoreAxis(voxelPosition, i.axis));
        d.SetNormal(i.normal);
        d.SetColor(mad(-0.5, metallic, 1.0) * _Color * tex2Dlod(_MainTex, float4(i.uv, 0.0, 0.0)));
        d.SetEmission(emission + ShadeSH9(float4(i.normal, 1.0)));
        VoxelBuffer.Append(d);

        return 0.0;
      }
      ENDHLSL
    }
  }

  Fallback Off
}
