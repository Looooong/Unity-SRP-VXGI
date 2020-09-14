using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEditor;
using System;

[RequireComponent(typeof(Light))]
[AddComponentMenu("Rendering/VXGI Light")]
public class VXGILight : MonoBehaviour
{
  public enum ShadowRayCount
  {
    Fixed,
    Dynamic
  };
  [Range(0,2.5f)]
  public float radius = 0.1f;

  [Tooltip(
@"Fixed: Uses some shadowRayPercent of the per-pixel shadow rays setting.
Dynamic: Increases the ray count as the radius increases."
  )]
  public ShadowRayCount shadowRayCount = ShadowRayCount.Dynamic;
  [Range(0, 1.0f)]
  public float shadowRayPercentage = 1.0f;

  public int dynamicModeMaxRays = 10;
}