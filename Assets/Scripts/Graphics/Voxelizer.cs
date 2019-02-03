using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

public class Voxelizer : System.IDisposable {
  public Vector3 center = new Vector3(0f, 0f, 0f);
  public List<Vector4> lightColors {
    get { return _lightColors; }
  }
  public List<Vector4> lightPositions {
    get { return _lightPositions; }
  }

  int _antiAliasing;
  int _propDummyTarget;
  int _resolution;
  float _bound;
  Camera _camera;
  CommandBuffer _command;
  CullResults _cullResults;
  DrawRendererSettings _drawSettings;
  FilterRenderersSettings _filterSettings;
  List<Vector4> _lightColors;
  List<Vector4> _lightPositions;
  Rect _rect;
  RenderTextureDescriptor _cameraDescriptor;
  VXGI _vxgi;

  public Voxelizer(VXGI vxgi) {
    _vxgi = vxgi;

    _command = new CommandBuffer { name = "Voxelizer" };
    _lightColors = new List<Vector4>(16);
    _lightPositions = new List<Vector4>(16);
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

  public void Voxelize(ScriptableRenderContext renderContext) {
    ScriptableCullingParameters cullingParams;
    if (!CullResults.GetCullingParameters(_camera, out cullingParams)) return;
    CullResults.Cull(ref cullingParams, renderContext, ref _cullResults);

    _lightColors.Clear();
    _lightPositions.Clear();

    foreach (var light in _cullResults.visibleLights) {
      if (light.lightType == LightType.Point) {
        _lightColors.Add(light.finalColor);
        _lightPositions.Add(light.light.transform.position);
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
      volumeDepth = 1
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
