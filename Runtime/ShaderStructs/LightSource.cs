using UnityEngine;

public struct LightSource {
  public const int size = (3 + 3 + 3 + 3 + 1 + 1 + 1 + 1) * 4;

  public Vector3 color; // 3 * 4 bytes
  public Vector3 direction; // 3 * 4 bytes
  public Vector3 voxelPosition; // 3 * 4 bytes
  public Vector3 worldPosition; // 3 * 4 bytes
  public float range; // 1 * 4 bytes
  public float spotCos; // 1 * 4 bytes
  public float spotFactor; // 1 * 4 bytes
  public uint type; // 1 * 4 bytes

  public LightSource(UnityEngine.Experimental.Rendering.VisibleLight light, Matrix4x4 worldToVoxel) {
    color = (Vector4)light.finalColor;
    direction = light.localToWorld.GetColumn(2);
    worldPosition = light.localToWorld.GetColumn(3);
    voxelPosition = worldToVoxel * new Vector4(worldPosition.x, worldPosition.y, worldPosition.z, 1f);
    range = light.range;
    spotCos = 0f;
    spotFactor = 0f;
    type = (uint)light.lightType;

    if (light.lightType == LightType.Spot) {
      float halfSpotRadian = .5f * light.spotAngle * Mathf.Deg2Rad;

      spotCos = Mathf.Cos(halfSpotRadian);

      if (light.spotAngle > 0f) {
        spotFactor = 1f / (Mathf.Cos(Mathf.Atan(Mathf.Tan(halfSpotRadian) * 46f / 64f)) - spotCos);
      }
    }
  }
}
