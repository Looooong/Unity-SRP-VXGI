using UnityEngine;
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

  Pass _pass;

  public LightingShader(Pass pass) {
    _pass = pass;
  }

  public void Execute(CommandBuffer command, Camera camera, RenderTargetIdentifier destination, float scale = 1f) {
    scale = Mathf.Clamp01(scale);

    command.BeginSample(_pass.ToString());
    command.GetTemporaryRT(ShaderIDs.Dummy, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.R8, RenderTextureReadWrite.Linear);

    if (scale == 1f) {
      command.Blit(ShaderIDs.Dummy, destination, material, (int)_pass);
    } else {
      int lowResWidth = (int)(scale * camera.pixelWidth);
      int lowResHeight = (int)(scale * camera.pixelHeight);

      command.GetTemporaryRT(ShaderIDs.LowResColor, lowResWidth, lowResHeight, 0, FilterMode.Bilinear, RenderTextureFormat.RGB111110Float, RenderTextureReadWrite.Linear);
      command.GetTemporaryRT(ShaderIDs.LowResDepth, lowResWidth, lowResHeight, 16, FilterMode.Bilinear, RenderTextureFormat.Depth, RenderTextureReadWrite.Linear);

      command.SetRenderTarget(ShaderIDs.LowResColor, (RenderTargetIdentifier)ShaderIDs.LowResDepth);
      command.ClearRenderTarget(true, true, Color.clear);

      command.Blit(ShaderIDs.Dummy, ShaderIDs.LowResColor, material, (int)_pass);
      command.Blit(ShaderIDs.Dummy, ShaderIDs.LowResDepth, UtilityShader.material, (int)UtilityShader.Pass.DepthCopy);
      command.Blit(ShaderIDs.LowResColor, destination, UtilityShader.material, (int)UtilityShader.Pass.LowResComposite);

      command.ReleaseTemporaryRT(ShaderIDs.LowResColor);
      command.ReleaseTemporaryRT(ShaderIDs.LowResDepth);
    }

    command.ReleaseTemporaryRT(ShaderIDs.Dummy);
    command.EndSample(_pass.ToString());
  }
}
