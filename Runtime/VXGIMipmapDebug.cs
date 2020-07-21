using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
[RequireComponent(typeof(VXGI))]
[AddComponentMenu("Rendering/VXGI Mipmap Debug")]
class VXGIMipmapDebug : MonoBehaviour {
  public bool usePointFilter = true;
  [Range(0f, 9f)]
  public float level = 1f;
  [Min(0.001f)] public float rayTracingStep = .05f;

  Camera _camera;
  CommandBuffer _command;
  VXGI _vxgi;

  void Awake() {
    _camera = GetComponent<Camera>();
    _vxgi = GetComponent<VXGI>();
  }

  void OnEnable() {
    _command = new CommandBuffer { name = "VXGI.Debug.Mipmap" };
    _camera.AddCommandBuffer(CameraEvent.AfterImageEffects, _command);
  }

  void OnDisable() {
    _camera.RemoveCommandBuffer(CameraEvent.AfterImageEffects, _command);
    _command.Dispose();
  }

  void OnDrawGizmosSelected() {
    if (_vxgi.AnisotropicVoxel) {
      Gizmos.color = Color.red;
      Gizmos.DrawLine(_vxgi.voxelSpaceCenter, _vxgi.voxelSpaceCenter + (_camera.transform.position - _vxgi.voxelSpaceCenter).normalized);
    }
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

    if (_vxgi.CascadesEnabled) {
      _command.SetGlobalFloat(ShaderIDs.MipmapLevel, Mathf.Min(level, _vxgi.cascadesCount));
    } else {
      _command.SetGlobalFloat(ShaderIDs.MipmapLevel, Mathf.Min(level + 1, _vxgi.radiances.Length));
    }

    _command.SetGlobalFloat(ShaderIDs.RayTracingStep, Mathf.Max(rayTracingStep, .001f));
    _command.SetGlobalVector("VXGI_SampleDirection", (_camera.transform.position - _vxgi.voxelSpaceCenter).normalized);
    _command.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
    _command.DrawProcedural(transform, VisualizationShader.material, (int)VisualizationShader.Pass.Mipmap, MeshTopology.Quads, 24, 1);
  }
}
