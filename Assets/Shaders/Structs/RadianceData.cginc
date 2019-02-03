#ifndef VXGI_RADIANCE_DATA
  #define VXGI_RADIANCE_DATA

  #define RADIANCE_PRECISION 65536.0

  #include "Variables.cginc"

  struct RadianceData
  {
    float4 color;
    uint count;

    uint CalculateAddress(uint3 position)
    {
      return 20 * mad(Resolution, mad(Resolution, position.x, position.y), position.z);
    }

    void InterlockedAdd(RWByteAddressBuffer buffer, uint3 position)
    {
      uint address = CalculateAddress(position);
      uint4 rawColor = round(color * RADIANCE_PRECISION);

      for (uint i = 0; i < 4; i++) buffer.InterlockedAdd(mad(4, i, address), rawColor[i]);
      buffer.InterlockedAdd(address + 16, count);
    }

    void Load(RWByteAddressBuffer buffer, uint3 position)
    {
      uint address = CalculateAddress(position);

      color = buffer.Load4(address) / RADIANCE_PRECISION;
      count = buffer.Load(address + 16);
    }

    void Store(RWByteAddressBuffer buffer, uint3 position)
    {
      uint address = CalculateAddress(position);

      buffer.Store4(address, round(color * RADIANCE_PRECISION));
      buffer.Store(address + 16, count);
    }
  };
#endif
