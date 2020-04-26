#ifndef VXGI_STRUCTS_LIGHT_SOURCE
  #define VXGI_STRUCTS_LIGHT_SOURCE

  #define LIGHT_SOURCE_TYPE_SPOT 0
  #define LIGHT_SOURCE_TYPE_DIRECTIONAL 1
  #define LIGHT_SOURCE_TYPE_POINT 2

  struct LightSource
  {
    float3 color;
    float3 direction;
    float3 voxelPosition;
    float3 worldposition;
    float range;
    float spotCos;
    float spotFactor;
    uint type;

    float3 Attenuation(float3 otherPosition)
    {
      return color / max(0.01, dot(otherPosition, otherPosition));
    }

    bool NotInRange(float3 otherPosition) {
      return dot(otherPosition, otherPosition) > range * range;
    }

    float SpotFalloff(float3 otherDirection) {
      return type == LIGHT_SOURCE_TYPE_SPOT ? min(1.0, spotFactor * (dot(otherDirection, direction) - spotCos)) : 1.0;
    }
  };
#endif
