#ifndef VXGI_STRUCTS_LIGHT_SOURCE
  #define VXGI_STRUCTS_LIGHT_SOURCE

  #define LIGHT_SOURCE_TYPE_SPOT 0
  #define LIGHT_SOURCE_TYPE_DIRECTIONAL 1
  #define LIGHT_SOURCE_TYPE_POINT 2

  struct LightSource
  {
    float3 color;
    float3 direction;
    float3 position;
    float range;
    float spotAngle;
    uint type;

    bool NotInRange(float3 position) {
      return dot(position, position) > range * range;
    }
  };
#endif
