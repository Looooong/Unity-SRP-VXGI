using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

public class LightingShader {
  public enum Pass {
    Emission = 0,
    DirectDiffuseSpecular = 1,
    IndirectDiffuse = 2,
    IndirectSpecular = 3
  }

  public static Material material {
    get {
      if (_material == null) _material = new Material(shader);

      return _material;
    }
  }
  public static Shader shader {
    get { return (Shader)Resources.Load("VXGI/Shader/Lighting"); }
  }

  static Material _material;

  int _dummyID;
  int _lowResColorID;
  int _lowResDepthID;
  Pass _pass;

  public LightingShader(Pass pass) {
    _pass = pass;

    _dummyID = Shader.PropertyToID("Dummy");
    _lowResColorID = Shader.PropertyToID("LowResColor");
    _lowResDepthID = Shader.PropertyToID("LowResDepth");
  }

  public void Execute(CommandBuffer command, Camera camera, RenderTargetIdentifier destination, float scale = 1f) {
    scale = Mathf.Clamp01(scale);

    command.BeginSample(_pass.ToString());
    command.GetTemporaryRT(_dummyID, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.R8, RenderTextureReadWrite.Linear);

    if (scale == 1f) {
      command.Blit(_dummyID, destination, material, (int)_pass);
    } else {
      int lowResWidth = (int)(scale * camera.pixelWidth);
      int lowResHeight = (int)(scale * camera.pixelHeight);

      command.GetTemporaryRT(_lowResColorID, lowResWidth, lowResHeight, 0, FilterMode.Bilinear, RenderTextureFormat.RGB111110Float, RenderTextureReadWrite.Linear);
      command.GetTemporaryRT(_lowResDepthID, lowResWidth, lowResHeight, 16, FilterMode.Bilinear, RenderTextureFormat.Depth, RenderTextureReadWrite.Linear);

      command.SetRenderTarget(_lowResColorID, (RenderTargetIdentifier)_lowResDepthID);
      command.ClearRenderTarget(true, true, Color.clear);

      command.Blit(_dummyID, _lowResColorID, material, (int)_pass);
      command.Blit(_dummyID, _lowResDepthID, UtilityShader.material, (int)UtilityShader.Pass.DepthCopy);
      command.Blit(_lowResColorID, destination, UtilityShader.material, (int)UtilityShader.Pass.LowResComposite);

      command.ReleaseTemporaryRT(_lowResColorID);
      command.ReleaseTemporaryRT(_lowResDepthID);
    }

    command.ReleaseTemporaryRT(_dummyID);
    command.EndSample(_pass.ToString());
  }
}
