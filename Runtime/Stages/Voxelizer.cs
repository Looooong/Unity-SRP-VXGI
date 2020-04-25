using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

public class Voxelizer : System.IDisposable {
  int _antiAliasing;
  int _propDummyTarget;
  int _resolution;
  float _bound;
  Camera _camera;
  CommandBuffer _command;
  CullResults _cullResults;
  DrawRendererSettings _drawSettings;
  FilterRenderersSettings _filterSettings;
  Rect _rect;
  RenderTextureDescriptor _cameraDescriptor;
  VXGI _vxgi;

  public Voxelizer(VXGI vxgi) {
    _vxgi = vxgi;

    _command = new CommandBuffer { name = "VXGI.Voxelizer" };
    _rect = new Rect(0f, 0f, 1f, 1f);

    _propDummyTarget = Shader.PropertyToID("DummyTarget");

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

    _command.Dispose();
  }

  public void Voxelize(ScriptableRenderContext renderContext, VXGIRenderer renderer) {
    ScriptableCullingParameters cullingParams;
    if (!CullResults.GetCullingParameters(_camera, out cullingParams)) return;
    CullResults.Cull(ref cullingParams, renderContext, ref _cullResults);

    _vxgi.lights.Clear();

    foreach (var light in _cullResults.visibleLights) {
      if (VXGI.supportedLightTypes.Contains(light.lightType) && light.finalColor.maxColorComponent > 0f) {
        _vxgi.lights.Add(new LightSource(light, _vxgi.worldToVoxel));
      }
    }

    UpdateCamera();

    _camera.pixelRect = _rect;

    _command.BeginSample(_command.name);

    _command.GetTemporaryRT(_propDummyTarget, _cameraDescriptor);
    _command.SetRenderTarget(_propDummyTarget, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare);

    _command.SetGlobalInt("Resolution", _resolution);
    _command.SetGlobalMatrix("WorldToVoxel", _vxgi.worldToVoxel);
    _command.SetGlobalMatrix("VoxelToProjection", GL.GetGPUProjectionMatrix(_camera.projectionMatrix, true) * _camera.worldToCameraMatrix * _vxgi.voxelToWorld);
    _command.SetRandomWriteTarget(1, _vxgi.voxelBuffer, false);

    _drawSettings.flags = renderer.drawRendererFlags;
    _drawSettings.rendererConfiguration = renderer.rendererConfiguration;

    renderContext.ExecuteCommandBuffer(_command);
    renderContext.DrawRenderers(_cullResults.visibleRenderers, ref _drawSettings, _filterSettings);

    _command.Clear();

    _command.ClearRandomWriteTargets();
    _command.ReleaseTemporaryRT(_propDummyTarget);

    _command.EndSample(_command.name);

    renderContext.ExecuteCommandBuffer(_command);

    _command.Clear();
  }

  void CreateCamera() {
    var gameObject = new GameObject("__" + _vxgi.name + "_VOXELIZER__") { hideFlags = HideFlags.HideAndDontSave };
    gameObject.SetActive(false);

    _camera = gameObject.AddComponent<Camera>();
    _camera.allowMSAA = true;
    _camera.orthographic = true;
    _camera.nearClipPlane = 0f;
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
    _drawSettings = new DrawRendererSettings(_camera, new ShaderPassName("Voxelization"));
    _drawSettings.sorting.flags = SortFlags.OptimizeStateChanges;
    _filterSettings = new FilterRenderersSettings(true) { renderQueueRange = RenderQueueRange.all };
  }

  void ResizeCamera() {
    _camera.farClipPlane = _bound;
    _camera.orthographicSize = .5f * _camera.farClipPlane;
  }

  void UpdateCamera() {
    if (_antiAliasing != (int)_vxgi.antiAliasing) {
      _antiAliasing = (int)_vxgi.antiAliasing;
      _cameraDescriptor.msaaSamples = _antiAliasing;
    }

    if (_bound != _vxgi.bound) {
      _bound = _vxgi.bound;
      ResizeCamera();
    }

    if (_resolution != (int)_vxgi.resolution) {
      _resolution = (int)_vxgi.resolution;
      _cameraDescriptor.height = _cameraDescriptor.width = _resolution;
    }

    _camera.transform.position = _vxgi.voxelSpaceCenter - Vector3.forward * _camera.orthographicSize;
    _camera.transform.LookAt(_vxgi.voxelSpaceCenter, Vector3.up);
  }
}
