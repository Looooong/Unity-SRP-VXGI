using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class VXGI : MonoBehaviour {
  public enum AntiAliasing { X1 = 1, X2 = 2, X4 = 4, X8 = 8 }
  public enum Resolution { Low = 33, Medium = 65, High = 129, VeryHigh = 257 }

  public Vector3 center;
  public Light sun;
  public AntiAliasing antiAliasing = AntiAliasing.X1;
  public Resolution resolution = Resolution.Medium;
  [Range(.1f, 1f)]
  public float diffuseResolutionScale = 1f;
  [Range(1f, 100f)]
  public float bound = 10f;
  public bool throttleTracing = false;
  [Range(1f, 100f)]
  public float tracingRate = 10f;
  public VXGIRenderer.Pass pass = VXGIRenderer.Pass.ConeTracing;
  public VXGIRenderer.MipmapSampler mipmapSampler = VXGIRenderer.MipmapSampler.Linear;
  [Range(0f, 9f)]
  public float level = 1f;
  [Range(.02f, 1f)]
  public float step = .1f;
  public bool followCamera = false;

  public float bufferScale {
    get { return 64f / (_resolution - 1); }
  }
  public float voxelSize {
    get { return bound / (float)resolution; }
  }
  public int volume {
    get { return _resolution * _resolution * _resolution; }
  }
  public ComputeBuffer radianceBuffer {
    get { return _radianceBuffer; }
  }
  public ComputeBuffer voxelBuffer {
    get { return _voxelBuffer; }
  }
  public Matrix4x4 voxelToWorld {
    get { return Matrix4x4.TRS(origin, Quaternion.identity, Vector3.one * voxelSize); }
  }
  public Matrix4x4 worldToVoxel {
    get { return voxelToWorld.inverse; }
  }
  public Parameterizer parameterizer {
    get { return _parameterizer; }
  }
  public RenderTexture[] radiances {
    get { return _radiances; }
  }
  public Vector3 origin {
    get { return voxelSpaceCenter - Vector3.one * .5f * bound; }
  }
  public Vector3 voxelSpaceCenter {
    get {
      var position = center;

      position /= voxelSize;
      position.x = Mathf.Floor(position.x);
      position.y = Mathf.Floor(position.y);
      position.z = Mathf.Floor(position.z);

      return position * voxelSize;
    }
  }
  public Voxelizer voxelizer {
    get { return _voxelizer; }
  }

  int _resolution = 0;
  float _previousTrace = 0f;
  Camera _camera;
  CommandBuffer _command;
  ComputeBuffer _radianceBuffer;
  ComputeBuffer _voxelBuffer;
  Mipmapper _mipmapper;
  Parameterizer _parameterizer;
  RenderTexture[] _radiances;
  RenderTextureDescriptor _radianceDescriptor;
  Vector3 _lastVoxelSpaceCenter;
  Voxelizer _voxelizer;
  VoxelShader _voxelShader;

  #region Rendering
  public void Render(ScriptableRenderContext renderContext, VXGIRenderer renderer) {
    Render(renderContext, _camera, renderer);
  }

  public void Render(ScriptableRenderContext renderContext, Camera camera, VXGIRenderer renderer) {
    UpdateResolution();

    float realtime = Time.realtimeSinceStartup;
    bool tracingThrottled = throttleTracing;

#if UNITY_EDITOR
    tracingThrottled &= UnityEditor.EditorApplication.isPlaying;
#endif

    if (tracingThrottled) {
      if (_previousTrace + 1f / tracingRate < realtime) {
        _previousTrace = realtime;

        PrePass(renderContext, renderer);
      }
    } else {
      PrePass(renderContext, renderer);
    }

    renderContext.SetupCameraProperties(camera);

    _command.ClearRenderTarget(true, true, Color.clear);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();

    if (pass == VXGIRenderer.Pass.Mipmap) {
      renderer.RenderMipmap(renderContext, camera, this);
    } else {
      SetupShader(renderContext);
      renderer.RenderDeferred(renderContext, camera, this);
    }

    renderContext.Submit();
  }

  void PrePass(ScriptableRenderContext renderContext, VXGIRenderer renderer) {
    if (followCamera) center = transform.position;

    var displacement = (voxelSpaceCenter - _lastVoxelSpaceCenter) / voxelSize;

    if (displacement.sqrMagnitude > 0f) {
      _mipmapper.Shift(renderContext, Vector3Int.RoundToInt(displacement));
    }

    _voxelizer.Voxelize(renderContext, renderer);
    _voxelShader.Render(renderContext);
    _mipmapper.Filter(renderContext);

    _lastVoxelSpaceCenter = voxelSpaceCenter;
  }

  void SetupShader(ScriptableRenderContext renderContext) {
    if ((sun != null) && (sun.isActiveAndEnabled)) {
      Debug.Assert(sun.type == LightType.Directional, "The sun is not directional.", sun);

      _command.EnableShaderKeyword("TRACE_SUN");
      _command.SetGlobalColor("SunColor", sun.color * sun.intensity);
      _command.SetGlobalVector("SunDirection", sun.transform.forward);
    } else {
      _command.DisableShaderKeyword("TRACE_SUN");
    }

    _command.SetGlobalInt("LightCount", _voxelizer.lightColors.Count);

    if (_voxelizer.lightColors.Count > 0) {
      _command.SetGlobalVectorArray("LightColors", _voxelizer.lightColors);
      _command.SetGlobalVectorArray("LightPositions", _voxelizer.lightPositions);
    }

    _command.SetGlobalInt("Resolution", _resolution);
    _command.SetGlobalMatrix("WorldToVoxel", worldToVoxel);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();
  }
  #endregion

  #region Messages
  void OnDrawGizmosSelected() {
    Gizmos.color = new Color(0, 0, 1, .2f);
    Gizmos.DrawWireCube(voxelSpaceCenter, Vector3.one * bound);
  }

  void OnEnable() {
    _resolution = (int)resolution;

    _camera = GetComponent<Camera>();
    _command = new CommandBuffer { name = "VXGI" };
    _mipmapper = new Mipmapper(this);
    _parameterizer = new Parameterizer();
    _voxelizer = new Voxelizer(this);
    _voxelShader = new VoxelShader(this);
    _lastVoxelSpaceCenter = voxelSpaceCenter;

    CreateBuffers();
    CreateTextureDescriptor();
    CreateTextures();
  }

  void OnDisable() {
    DisposeTextures();
    DisposeBuffers();

    _voxelShader.Dispose();
    _voxelizer.Dispose();
    _parameterizer.Dispose();
    _mipmapper.Dispose();
    _command.Dispose();
  }
  #endregion

  #region Buffers
  void CreateBuffers() {
    _radianceBuffer = new ComputeBuffer(5 * volume, 4, ComputeBufferType.Raw);
    _voxelBuffer = new ComputeBuffer((int)(bufferScale * volume), VoxelData.size, ComputeBufferType.Append);
  }

  void DisposeBuffers() {
    _radianceBuffer.Dispose();
    _voxelBuffer.Dispose();
  }

  void ResizeBuffers() {
    DisposeBuffers();
    CreateBuffers();
  }
  #endregion

  #region RenderTextures
  void CreateTextureDescriptor() {
    _radianceDescriptor = new RenderTextureDescriptor() {
      colorFormat = RenderTextureFormat.ARGBHalf,
      dimension = TextureDimension.Tex3D,
      enableRandomWrite = true,
      msaaSamples = 1
    };
  }

  void CreateTextures() {
    int currentResolution = _resolution;
    _radianceDescriptor.height = _radianceDescriptor.width = _radianceDescriptor.volumeDepth = currentResolution;

    _radiances = new RenderTexture[(int)Mathf.Log(_resolution - 1, 2)];

    for (int i = 0; i < _radiances.Length; i++) {
      _radiances[i] = new RenderTexture(_radianceDescriptor);
      _radiances[i].Create();
      _radianceDescriptor.height = _radianceDescriptor.width = _radianceDescriptor.volumeDepth = currentResolution = (currentResolution - 1) / 2 + 1;
    }

    for (int i = 0; i < 9; i++) {
      Shader.SetGlobalTexture("Radiance" + i, radiances[Mathf.Min(i, _radiances.Length - 1)]);
    }
  }

  void DisposeTextures() {
    foreach (var radiance in _radiances) {
      radiance.DiscardContents();
      radiance.Release();
    }
  }

  void ResizeTextures() {
    DisposeTextures();
    CreateTextures();
  }
  #endregion

  void UpdateResolution() {
    if (_resolution != (int)resolution) {
      _resolution = (int)resolution;
      ResizeBuffers();
      ResizeTextures();
    }
  }
}
