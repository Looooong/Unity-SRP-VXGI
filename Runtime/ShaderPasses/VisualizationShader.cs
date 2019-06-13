using UnityEngine;

public static class VisualizationShader {
  public enum Pass {
    Mipmap = 0
  }

  public static Material material {
    get {
      if (_material == null) _material = new Material(shader);

      return _material;
    }
  }
  public static Shader shader {
    get { return (Shader)Resources.Load("VXGI/Shader/Visualization"); }
  }

  static Material _material;
}
