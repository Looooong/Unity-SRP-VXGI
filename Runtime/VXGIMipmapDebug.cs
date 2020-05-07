using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
[RequireComponent(typeof(VXGI))]
[AddComponentMenu("Rendering/VXGI Mipmap Debug")]
class VXGIMipmapDebug : MonoBehaviour {
  public bool usePointFilter = true;
  [Range(1f, 9f)]
  public float level = 1f;
  public float rayTracingStep = .05f;

  Camera _camera;
  CommandBuffer _command;
  VXGI _vxgi;

  void Awake() {
    _camera = GetComponent<Camera>();
    _vxgi = GetComponent<VXGI>();
  }

  void OnEnable() {
    _command = new CommandBuffer { name = "VXGI.Debug.Mipmap" };
    _camera.AddCommandBuffer(CameraEvent.AfterEverything, _command);
  }

  void OnDisable() {
    _camera.RemoveCommandBuffer(CameraEvent.AfterEverything, _command);
    _command.Dispose();
  }

  void OnPreRender() {
    if (!isActiveAndEnabled || !_vxgi.isActiveAndEnabled) return;

    _command.Clear();

    var transform = Matrix4x4.TRS(_vxgi.origin, Quaternion.identity, Vector3.one * _vxgi.bound);

    if (usePointFilter) {
      _command.EnableShaderKeyword("RADIANCE_POINT_SAMPLER");
    } else {
      _command.DisableShaderKeyword("RADIANCE_POINT_SAMPLER");
    }

    _command.SetGlobalFloat(ShaderIDs.MipmapLevel, Mathf.Min(level, _vxgi.radiances.Length));
    _command.SetGlobalFloat(ShaderIDs.RayTracingStep, rayTracingStep);
    _command.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
    _command.DrawProcedural(transform, VisualizationShader.material, (int)VisualizationShader.Pass.Mipmap, MeshTopology.Quads, 24, 1);
  }
}
