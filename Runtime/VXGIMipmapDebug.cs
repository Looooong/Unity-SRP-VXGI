using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
[RequireComponent(typeof(VXGI))]
[AddComponentMenu("Rendering/VXGI Mipmap Debug")]
class VXGIMipmapDebug : MonoBehaviour {
  public enum ViewMode
  {
    Camera,
    StepMap
  }
  public ViewMode viewMode = ViewMode.Camera;
  public bool usePointFilter = true;
  [Range(0f, 9f)]
  public float level = 1f;
  [Min(0.001f)] public float rayTracingStep = .05f;
  [Range(0f, 1f)]
  public float layer = 0.5f;
  public bool showDepth;

  Camera _camera;
  CommandBuffer _command;
  VXGI _vxgi;

  void Awake() {
    _camera = GetComponent<Camera>();
    _vxgi = GetComponent<VXGI>();
  }

  void OnEnable() {
    _command = new CommandBuffer { name = "VXGI.Debug.Mipmap" };
    _camera.AddCommandBuffer(CameraEvent.AfterForwardAlpha, _command);
  }

  void OnDisable() {
    _camera.RemoveCommandBuffer(CameraEvent.AfterForwardAlpha, _command);
    _command.Dispose();
  }

  void OnDrawGizmosSelected() {
    if (_vxgi.ColorVoxelizer.AnisotropicColors) {
      Gizmos.color = Color.red;
      Gizmos.DrawLine(_vxgi.ColorVoxelizer.voxelSpaceCenter, _vxgi.ColorVoxelizer.voxelSpaceCenter + (_camera.transform.position - _vxgi.ColorVoxelizer.voxelSpaceCenter).normalized);
    }
  }

  void OnPreRender() {
    if (!isActiveAndEnabled || !_vxgi.isActiveAndEnabled) return;
    _command.Clear();

    var transform = Matrix4x4.TRS(_vxgi.ColorVoxelizer.origin, Quaternion.identity, Vector3.one * _vxgi.bound);

    if (usePointFilter) {
      _command.EnableShaderKeyword("RADIANCE_POINT_SAMPLER");
    } else {
      _command.DisableShaderKeyword("RADIANCE_POINT_SAMPLER");
    }
    if (viewMode == ViewMode.Camera) {
      _command.EnableShaderKeyword("VIEWMODE_CAMERA");
    }
    else {
      _command.DisableShaderKeyword("VIEWMODE_CAMERA");
    }
    if (_vxgi.lightingMethod == VXGI.LightingMethod.Rays) {
      _command.EnableShaderKeyword("LIGHTING_RAYS");
    }
    else {
      _command.DisableShaderKeyword("LIGHTING_CONES");
    }
    if (showDepth)
    {
      _command.EnableShaderKeyword("SHOW_DEPTH");
    }
    else
    {
      _command.DisableShaderKeyword("SHOW_DEPTH");
    }

    _command.SetGlobalFloat(ShaderIDs.MipmapLevel, Mathf.Min(level, _vxgi.cascadesCount));

    _command.SetGlobalFloat(ShaderIDs.RayTracingStep, Mathf.Max(rayTracingStep, .001f));
    _command.SetGlobalFloat("VXGI_layer", layer);
    _command.SetGlobalVector("VXGI_SampleDirection", (_camera.transform.position - _vxgi.ColorVoxelizer.voxelSpaceCenter).normalized);
    _command.DrawProcedural(transform, VisualizationShader.material, (int)VisualizationShader.Pass.Mipmap, MeshTopology.Quads, 24, 1);
  }
}
