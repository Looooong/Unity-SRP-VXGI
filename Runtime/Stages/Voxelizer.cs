using UnityEngine;
using UnityEngine.Rendering;

public class Voxelizer : System.IDisposable {
  int _antiAliasing;
  int _resolution;
  Camera _camera;
  CommandBuffer _command;
  DrawingSettings _drawingSettings;
  FilteringSettings _filteringSettings;
  RenderTextureDescriptor _cameraDescriptor;
  ScriptableCullingParameters _cullingParameters;
  VXGI _vxgi;

  public Voxelizer(VXGI vxgi) {
    _vxgi = vxgi;

    _command = new CommandBuffer { name = "VXGI.Voxelizer" };

    CreateCamera();
    CreateCameraDescriptor();
    CreateCameraSettings();
  }

  public void Dispose() {
#if UNITY_EDITOR
    GameObject.DestroyImmediate(_camera.gameObject);
#else
    GameObject.Destroy(_camera.gameObject);
#endif

    _command.Dispose();
  }

  public void Voxelize(ScriptableRenderContext renderContext, VXGIRenderer renderer) {
    if (!_camera.TryGetCullingParameters(out _cullingParameters)) return;
  
    var cullingResults = renderContext.Cull(ref _cullingParameters);

    _vxgi.lights.Clear();

    foreach (var light in cullingResults.visibleLights) {
      if (VXGI.supportedLightTypes.Contains(light.lightType) && light.finalColor.maxColorComponent > 0f) {
        _vxgi.lights.Add(new LightSource(light, _vxgi.worldToVoxel));
      }
    }

    UpdateCamera();

    _command.BeginSample(_command.name);

    _command.GetTemporaryRT(ShaderIDs.Dummy, _cameraDescriptor);
    _command.SetRenderTarget(ShaderIDs.Dummy, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare);

    _command.SetGlobalInt(ShaderIDs.Resolution, _resolution);
    _command.SetRandomWriteTarget(1, _vxgi.voxelBuffer, false);
    _command.SetViewProjectionMatrices(_camera.worldToCameraMatrix, _camera.projectionMatrix);

    _drawingSettings.perObjectData = renderer.RenderPipeline.PerObjectData;

    renderContext.ExecuteCommandBuffer(_command);
    renderContext.DrawRenderers(cullingResults, ref _drawingSettings, ref _filteringSettings);

    _command.Clear();

    _command.ClearRandomWriteTargets();
    _command.ReleaseTemporaryRT(ShaderIDs.Dummy);

    _command.EndSample(_command.name);

    renderContext.ExecuteCommandBuffer(_command);

    _command.Clear();
  }

  void CreateCamera() {
    var gameObject = new GameObject("__" + _vxgi.name + "_VOXELIZER__") { hideFlags = HideFlags.HideAndDontSave };
    gameObject.SetActive(false);

    _camera = gameObject.AddComponent<Camera>();
    _camera.allowMSAA = true;
    _camera.aspect = 1f;
    _camera.orthographic = true;
  }

  void CreateCameraDescriptor() {
    _cameraDescriptor = new RenderTextureDescriptor() {
      colorFormat = RenderTextureFormat.R8,
      dimension = TextureDimension.Tex2D,
      memoryless = RenderTextureMemoryless.Color | RenderTextureMemoryless.Depth | RenderTextureMemoryless.MSAA,
      volumeDepth = 1,
      sRGB = false
    };
  }

  void CreateCameraSettings() {
    var sortingSettings = new SortingSettings(_camera) { criteria = SortingCriteria.OptimizeStateChanges };
    _drawingSettings = new DrawingSettings(ShaderTagIDs.Voxelization, sortingSettings);
    _filteringSettings = new FilteringSettings(RenderQueueRange.all);
  }

  void UpdateCamera() {
    if (_antiAliasing != (int)_vxgi.antiAliasing) {
      _antiAliasing = (int)_vxgi.antiAliasing;
      _cameraDescriptor.msaaSamples = _antiAliasing;
    }

    if (_resolution != (int)_vxgi.resolution) {
      _resolution = (int)_vxgi.resolution;
      _cameraDescriptor.height = _cameraDescriptor.width = _resolution;
    }

    _camera.farClipPlane = .5f * _vxgi.bound;
    _camera.nearClipPlane = -.5f * _vxgi.bound;
    _camera.orthographicSize = .5f * _vxgi.bound;
    _camera.transform.position = _vxgi.voxelSpaceCenter;
  }
}
