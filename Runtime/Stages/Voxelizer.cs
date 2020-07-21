using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

internal class Voxelizer : System.IDisposable {
  internal int LightsourcesCount { get; private set; }
  internal ComputeBuffer LightSources { get; }

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

    _command = new CommandBuffer();

    LightSources = new ComputeBuffer(128, LightSource.size);

    CreateCamera();
    CreateCameraDescriptor();
    CreateCameraSettings();
    UpdateCamera();
  }

  public void Dispose() {
#if UNITY_EDITOR
    GameObject.DestroyImmediate(_camera.gameObject);
#else
    GameObject.Destroy(_camera.gameObject);
#endif

    LightSources.Dispose();
    _command.Dispose();
  }

  public void Voxelize(ScriptableRenderContext renderContext, VXGIRenderer renderer) {
    UpdateCamera();

    for (
      int cascadeIndex = 0, drawsCount = _vxgi.CascadesEnabled ? _vxgi.cascadesCount : 1, divisor = 1 << (drawsCount - 1);
      cascadeIndex < drawsCount;
      cascadeIndex++, divisor >>= 1
    ) {
      var extent = .5f * _vxgi.bound / divisor;

      _camera.farClipPlane = extent;
      _camera.nearClipPlane = -extent;
      _camera.orthographicSize = extent;

      if (!_camera.TryGetCullingParameters(out _cullingParameters)) continue;

      var cullingResults = renderContext.Cull(ref _cullingParameters);

      UpdateLightSources(cullingResults);

      _command.name = $"VXGI.Voxelizer.{cascadeIndex}.{extent}";
      _command.BeginSample(_command.name);
      _command.GetTemporaryRT(ShaderIDs.Dummy, _cameraDescriptor);
      _command.SetRenderTarget(ShaderIDs.Dummy, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare);
      _command.SetGlobalInt(ShaderIDs.Resolution, _resolution);
      _command.SetGlobalInt(ShaderIDs.VXGI_CascadeIndex, cascadeIndex);
      _command.SetRandomWriteTarget(1, _vxgi.voxelBuffer, cascadeIndex > 0);
      _command.SetViewProjectionMatrices(_camera.worldToCameraMatrix, _camera.projectionMatrix);
      renderContext.ExecuteCommandBuffer(_command);
      _command.Clear();

      renderContext.DrawRenderers(cullingResults, ref _drawingSettings, ref _filteringSettings);

      _command.ClearRandomWriteTargets();
      _command.ReleaseTemporaryRT(ShaderIDs.Dummy);
      _command.EndSample(_command.name);
      renderContext.ExecuteCommandBuffer(_command);
      _command.Clear();
    }
  }

  void CreateCamera() {
    var gameObject = new GameObject("__" + _vxgi.name + "_VOXELIZER__") { hideFlags = HideFlags.HideAndDontSave };
    gameObject.SetActive(false);

    _camera = gameObject.AddComponent<Camera>();
    _camera.allowMSAA = true;
    _camera.orthographic = true;
    _camera.pixelRect = new Rect(0f, 0f, 1f, 1f);
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

    int newResolution = (int)_vxgi.resolution;

    if (_vxgi.AnisotropicVoxel) newResolution *= 2;

    if (_resolution != newResolution) {
      _cameraDescriptor.height = _cameraDescriptor.width = _resolution = newResolution;
    }

    _camera.transform.position = _vxgi.voxelSpaceCenter;
  }

  void UpdateLightSources(CullingResults cullingResults) {
    var data = new NativeArray<LightSource>(128, Allocator.Temp);
    LightsourcesCount = 0;

    for (int i = 0; i < data.Length && i < cullingResults.visibleLights.Length; i++) {
      var light = cullingResults.visibleLights[i];

      if (VXGI.SupportedLightTypes.Contains(light.lightType) && light.finalColor.maxColorComponent > 0f) {
        data[LightsourcesCount++] = new LightSource(light, _vxgi);
      }
    }

    LightSources.SetData(data);
    data.Dispose();
  }
}
