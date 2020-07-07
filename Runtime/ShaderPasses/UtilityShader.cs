using UnityEngine;

public static class UtilityShader {
  public enum Pass {
    BlitViewport,
    DepthCopy,
    DepthCopyViewport,
    EncodeDepthNormal,
    GrabCopy,
    LowResComposite
  }

  public static Material material {
    get {
      if (_material == null) _material = new Material(shader);

      return _material;
    }
  }
  public static Shader shader {
    get { return (Shader)Resources.Load("VXGI/Shader/Utility"); }
  }

  static Material _material;
}
