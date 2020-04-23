#ifndef VXGI_UTILITIES
  #define VXGI_UTILITIES

  float Pow5(float x)
  {
    float x2 = x * x;
    return x2 * x2 * x;
  }

  float TextureSDF(float3 position)
  {
    position = 0.5 - abs(position - 0.5);
    return min(min(position.x, position.y), position.z);
  }
#endif
