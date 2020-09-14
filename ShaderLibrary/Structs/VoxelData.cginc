#ifndef VXGI_VOXEL_DATA
  #define VXGI_VOXEL_DATA

  #define VOXEL_DATA_UINTS 6

  struct VoxelData
  {
    // index      : (bit) name
    // rawData[0] : (16) Position.x | (16) Position.y
    // rawData[1] : (16) Position.z | (16) Normal.x
    // rawData[2] : (16) Normal.y   | (16) Normal.z
    // rawData[3] : (16) Color.r    | (16) Color.g
    // rawData[4] : (16) Color.b    | (16) Color.a
    // rawData[5] : (32) linked-list pointer
    uint rawData[VOXEL_DATA_UINTS]; // 6 * 4 bytes

    void Initialize()
    {
      [unroll]
      for (uint i = 0; i < VOXEL_DATA_UINTS; i++) rawData[i] = 0;
    }

    uint GetPointer()
    {
      return rawData[5];
    }

    void SetPointer(uint prev)
    {
      rawData[5] = prev;
    }

    float3 GetPosition()
    {
      return f16tof32(
        uint4(
          rawData[0],
          rawData[0] >> 16,
          rawData[1],
          0
        )
      ).xyz;
    }

    void SetPosition(float3 position)
    {
      uint4 raw = f32tof16(float4(position, 0.0));
      rawData[0] = raw[0] | (raw[1] << 16);
      rawData[1] &= 0xffff0000;
      rawData[1] |= raw[2];
    }

    float3 GetNormal()
    {
      return f16tof32(
        uint4(
          rawData[1] >> 16,
          rawData[2],
          rawData[2] >> 16,
          0
        )
      ).xyz;
    }

    void SetNormal(float3 normal)
    {
      uint4 raw = f32tof16(float4(normal, 0.0));
      rawData[1] &= 0x0000ffff;
      rawData[1] |= (raw[0] << 16);
      rawData[2] = raw[1] | (raw[2] << 16);
    }

    float4 GetColor()
    {
      return f16tof32(
        uint4(
          rawData[3],
          rawData[3] >> 16,
          rawData[4],
          rawData[4] >> 16
        )
      );
    }

    void SetColor(float4 color)
    {
      uint4 raw = f32tof16(color);
      rawData[3] = raw[0] | (raw[1] << 16);
      rawData[4] = raw[2] | (raw[3] << 16);
    }
  };
#endif
