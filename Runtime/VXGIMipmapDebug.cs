using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
[RequireComponent(typeof(VXGI))]
[AddComponentMenu("Rendering/VXGI Mipmap Debug")]
public class VXGIMipmapDebug : MonoBehaviour {
  [Range(1f, 9f)]
  public float mipmapLevel = 1f;
  [Tooltip("How big is a step when ray tracing through the voxel volume.")]
  public float rayTracingStep = .05f;
  public FilterMode filterMode = FilterMode.Point;

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

    if (filterMode == FilterMode.Point) {
      _command.EnableShaderKeyword("RADIANCE_POINT_SAMPLER");
    } else {
      _command.DisableShaderKeyword("RADIANCE_POINT_SAMPLER");
    }

    _command.SetGlobalFloat(ShaderIDs.MipmapLevel, Mathf.Min(mipmapLevel, _vxgi.radiances.Length));
    _command.SetGlobalFloat(ShaderIDs.RayTracingStep, rayTracingStep);
    _command.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
    _command.DrawProcedural(transform, VisualizationShader.material, (int)VisualizationShader.Pass.Mipmap, MeshTopology.Quads, 24, 1);
  }
}
