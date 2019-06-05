Shader "Voxel-based Shader/Basic"
{
  Properties
  {
    _Color("Color", Color) = (1,1,1,1)
    _MainTex("Albedo", 2D) = "white" {}

    _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

    _Glossiness("Smoothness", Range(0.0, 1.0)) = 0.0
    _GlossMapScale("Smoothness Scale", Range(0.0, 1.0)) = 1.0
    [Enum(Metallic Alpha,0,Albedo Alpha,1)] _SmoothnessTextureChannel ("Smoothness texture channel", Float) = 0

    [Gamma] _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
    _MetallicGlossMap("Metallic", 2D) = "white" {}

    [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
    [ToggleOff] _GlossyReflections("Glossy Reflections", Float) = 1.0

    _BumpScale("Scale", Float) = 1.0
    _BumpMap("Normal Map", 2D) = "bump" {}

    _Parallax ("Height Scale", Range (0.005, 0.08)) = 0.02
    _ParallaxMap ("Height Map", 2D) = "black" {}

    _OcclusionStrength("Strength", Range(0.0, 1.0)) = 1.0
    _OcclusionMap("Occlusion", 2D) = "white" {}

    _EmissionColor("Color", Color) = (0,0,0)
    _EmissionMap("Emission", 2D) = "white" {}

    _DetailMask("Detail Mask", 2D) = "white" {}

    _DetailAlbedoMap("Detail Albedo x2", 2D) = "grey" {}
    _DetailNormalMapScale("Scale", Float) = 1.0
    _DetailNormalMap("Normal Map", 2D) = "bump" {}

    [Enum(UV0,0,UV1,1)] _UVSec ("UV Set for secondary textures", Float) = 0

    // Blending state
    [HideInInspector] _Mode ("__mode", Float) = 0.0
    [HideInInspector] _SrcBlend ("__src", Float) = 1.0
    [HideInInspector] _DstBlend ("__dst", Float) = 0.0
    [HideInInspector] _ZWrite ("__zw", Float) = 1.0
  }

  SubShader
  {
    Tags { "RenderPipeline"="VXGI" }

    Pass
    {
      Tags { "LightMode"="Deferred" }

      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma shader_feature _EMISSION
      #pragma shader_feature _METALLICGLOSSMAP

      #include "UnityCG.cginc"
      #include "UnityStandardUtils.cginc"
      #include "Packages/com.looooong.srp.vxgi/Shaders/Basic/Variables.cginc"

      struct v2f
      {
        float4 vertex : SV_POSITION;
        float2 uv : TEXCOORD0;
        float3 tangentX : TEXCOORD1;
        float3 tangentY : TEXCOORD2;
        float3 tangentZ : TEXCOORD3;
      };

      struct FragmentOutput {
        float4 diffuse   : SV_TARGET0;
        float4 specular  : SV_TARGET1;
        float3 normal    : SV_TARGET2;
        float3 emission  : SV_TARGET3;
      };

      v2f vert (appdata_tan v)
      {
        float3 normal = UnityObjectToWorldNormal(v.normal);
        float3 tangent = UnityObjectToWorldDir(v.tangent.xyz);
        float3 bitangent = cross(normal, tangent) * v.tangent * unity_WorldTransformParams.w;

        v2f o;
        o.vertex = UnityObjectToClipPos(v.vertex);
        o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
        o.tangentX = float3(tangent.x, bitangent.x, normal.x);
        o.tangentY = float3(tangent.y, bitangent.y, normal.y);
        o.tangentZ = float3(tangent.z, bitangent.z, normal.z);

        return o;
      }

      FragmentOutput frag (v2f i)
      {
        float4 color = _Color * tex2D(_MainTex, i.uv);

        float3 localNormal = UnpackScaleNormal(tex2D(_BumpMap, i.uv), _BumpScale);
        float3 worldNormal = float3(
          dot(i.tangentX, localNormal),
          dot(i.tangentY, localNormal),
          dot(i.tangentZ, localNormal)
        );

        float glossiness = _SmoothnessTextureChannel > 0.0 ? color.a * _GlossMapScale : _Glossiness;

        #ifdef _METALLICGLOSSMAP
          float metallic = tex2D(_MetallicGlossMap, i.uv).r;
        #else
          float metallic = _Metallic;
        #endif

        #ifdef _EMISSION
          float3 emission = _EmissionColor * tex2D(_EmissionMap, i.uv).rgb;
        #else
          float3 emission = 0.0;
        #endif

        FragmentOutput o;
        o.diffuse = color;
        o.normal = mad(normalize(worldNormal), 0.5, 0.5);
        o.emission = emission;
        o.specular = float4(glossiness, metallic, 0.0, 0.0);

        return o;
      }
      ENDCG
    }

    Pass
    {
      Name "Voxelization"

      Tags { "LightMode"="Voxelization" }

      Cull Off
      ZWrite Off
      ZTest Always

      CGPROGRAM
      #pragma require geometry
      #pragma target 4.5
      #pragma vertex vert
      #pragma geometry geom
      #pragma fragment frag
      #pragma shader_feature _EMISSION

      #include "UnityCG.cginc"
      #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Variables.cginc"
      #include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Structs/VoxelData.cginc"
      #include "Packages/com.looooong.srp.vxgi/Shaders/Basic/Variables.cginc"

      #define AXIS_X 0
      #define AXIS_Y 1
      #define AXIS_Z 2

      // Map depth [0, 1] to Z coordinate [0, Resolution)
      static float DepthResolution = Resolution * 0.999999;
      float4x4 VoxelToProjection;

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

      v2g vert (appdata_base v)
      {
        v2g o;
        o.vertex = mul(WorldToVoxel, mul(unity_ObjectToWorld, v.vertex));
        o.normal = UnityObjectToWorldNormal(v.normal);
        o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
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
      void geom (triangle v2g i[3], inout TriangleStream<g2f> triStream)
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

          o.position = mul(VoxelToProjection, float4(SwizzleAxis(i[j].vertex, axis), 1.0));

          #if defined(UNITY_REVERSED_Z)
            o.position.z = 1.0 - o.position.z;
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

      fixed frag (g2f i) : SV_TARGET
      {
        #ifdef _METALLICGLOSSMAP
          float metallic = tex2D(_MetallicGlossMap, i.uv).r;
        #else
          float metallic = _Metallic;
        #endif

        VoxelData d;
        d.Initialize();
        d.SetPosition(RestoreAxis(float3(i.position.xy, i.position.z * DepthResolution), i.axis));
        d.SetNormal(normalize(i.normal));
        d.SetColor(mad(-0.5, metallic, 1.0) * _Color * tex2Dlod(_MainTex, float4(i.uv, 0.0, 0.0)));
#ifdef _EMISSION
        d.SetEmission(_EmissionColor * tex2Dlod(_EmissionMap, float4(i.uv, 0.0, 0.0)));
#endif
        VoxelBuffer.Append(d);

        return 0.0;
      }
      ENDCG
    }

    UsePass "Standard/FORWARD"
  }

  Fallback "Standard"
  CustomEditor "StandardShaderGUI"
}
