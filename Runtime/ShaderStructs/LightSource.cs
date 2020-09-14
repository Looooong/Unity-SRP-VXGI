using UnityEngine;
using UnityEngine.Rendering;

public struct LightSource {
  public const int size = (3 + 3 + 3 + 3 + 1 + 1 + 1 + 1 + 2) * 4;

  public Vector3 color; // 3 * 4 bytes
  public Vector3 direction; // 3 * 4 bytes
  public Vector3 voxelPosition; // 3 * 4 bytes
  public Vector3 worldPosition; // 3 * 4 bytes
  public float range; // 1 * 4 bytes
  public float spotCos; // 1 * 4 bytes
  public float spotFactor; // 1 * 4 bytes
  public uint type; // 1 * 4 bytes
  public Vector2 radius; // 2 * 4 bytes

  public LightSource(VisibleLight light, VXGI vxgi, Voxelizer voxelizer) {
    color = (Vector4)light.finalColor;
    direction = light.localToWorldMatrix.GetColumn(2);
    worldPosition = light.localToWorldMatrix.GetColumn(3);
    range = light.range;
    spotCos = 0f;
    spotFactor = 0f;
    type = (uint)light.lightType;
    VXGILight extraInfo = light.light.gameObject.GetComponent<VXGILight>();
    radius = new Vector2(0, 0);
    if (extraInfo != null)
    {
      if (extraInfo.shadowRayPercentage > 0)
      {
        if (vxgi == null)
        {
          radius = new Vector2(extraInfo.radius, 1f);
        }
        else
        {
          if (extraInfo.shadowRayCount == VXGILight.ShadowRayCount.Dynamic)
            radius = new Vector2(extraInfo.radius, Mathf.Min(extraInfo.dynamicModeMaxRays, (int)(Mathf.Max(1, Mathf.Sqrt(extraInfo.radius) * extraInfo.shadowRayPercentage * vxgi.PerPixelPerLightShadowRays))));
          else
            radius = new Vector2(extraInfo.radius, Mathf.Max(1, (int)(extraInfo.shadowRayPercentage * vxgi.PerPixelPerLightShadowRays)));
        }
      }
    }
    
    voxelPosition = (worldPosition - voxelizer.origin) / voxelizer.Bound;

    if (light.lightType == LightType.Spot) {
      float halfSpotRadian = .5f * light.spotAngle * Mathf.Deg2Rad;

      spotCos = Mathf.Cos(halfSpotRadian);

      if (light.spotAngle > 0f) {
        spotFactor = 1f / (Mathf.Cos(Mathf.Atan(Mathf.Tan(halfSpotRadian) * 46f / 64f)) - spotCos);
      }
    }
  }
}
