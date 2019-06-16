using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

public class LightingShader : System.IDisposable {
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
  CommandBuffer _command;
  Pass _pass;

  public LightingShader(Pass pass) {
    _pass = pass;

    _dummyID = Shader.PropertyToID("Dummy");
    _lowResColorID = Shader.PropertyToID("LowResColor");
    _lowResDepthID = Shader.PropertyToID("LowResDepth");

    _command = new CommandBuffer() { name = "VXGI.Lighting." + System.Enum.GetName(typeof(Pass), pass) };
  }

  public void Dispose() {
    _command.Dispose();
  }

  public void Execute(ScriptableRenderContext renderContext, Camera camera, float scale = 1f) {
    scale = Mathf.Clamp01(scale);

    _command.BeginSample(_command.name);
    _command.GetTemporaryRT(_dummyID, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.R8);

    if (scale == 1f) {
      _command.Blit(_dummyID, BuiltinRenderTextureType.CameraTarget, material, (int)_pass);
    } else {
      int lowResWidth = (int)(scale * camera.pixelWidth);
      int lowResHeight = (int)(scale * camera.pixelHeight);

      _command.GetTemporaryRT(_lowResColorID, lowResWidth, lowResHeight, 0, FilterMode.Bilinear, RenderTextureFormat.RGB111110Float);
      _command.GetTemporaryRT(_lowResDepthID, lowResWidth, lowResHeight, 16, FilterMode.Bilinear, RenderTextureFormat.Depth);

      _command.SetRenderTarget(_lowResColorID, (RenderTargetIdentifier)_lowResDepthID);
      _command.ClearRenderTarget(true, true, Color.clear);

      _command.Blit(_dummyID, _lowResColorID, material, (int)_pass);
      _command.Blit(_dummyID, _lowResDepthID, UtilityShader.material, (int)UtilityShader.Pass.DepthCopy);
      _command.Blit(_lowResColorID, BuiltinRenderTextureType.CameraTarget, UtilityShader.material, (int)UtilityShader.Pass.LowResComposite);

      _command.ReleaseTemporaryRT(_lowResColorID);
      _command.ReleaseTemporaryRT(_lowResDepthID);
    }

    _command.ReleaseTemporaryRT(_dummyID);
    _command.EndSample(_command.name);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();
  }
}
