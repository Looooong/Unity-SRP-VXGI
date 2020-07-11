using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
[AddComponentMenu("Rendering/VXGI")]
public class VXGI : MonoBehaviour {
  public readonly static ReadOnlyCollection<LightType> supportedLightTypes = new ReadOnlyCollection<LightType>(new[] { LightType.Point, LightType.Directional, LightType.Spot });
  public enum AntiAliasing { X1 = 1, X2 = 2, X4 = 4, X8 = 8 }
  public enum Resolution {
    [InspectorName("Low (32^3)")] Low = 32,
    [InspectorName("Medium (64^3)")] Medium = 64,
    [InspectorName("High (128^3)")] High = 128,
    [InspectorName("Very High (256^3)")] VeryHigh = 256
  }
  public const int MaxCascadesCount = 8;
  public const int MinCascadesCount = 4;

  public bool cascadesEnabled;
  [Range(MinCascadesCount, MaxCascadesCount)]
  public int cascadesCount = MinCascadesCount;
  public Vector3 center;
  public AntiAliasing antiAliasing = AntiAliasing.X1;
  public Resolution resolution = Resolution.Medium;
  [Tooltip(
@"Box: fast, 2^n voxel resolution.
Gaussian 3x3x3: fast, 2^n+1 voxel resolution (recommended).
Gaussian 4x4x4: slow, 2^n voxel resolution."
  )]
  public Mipmapper.Mode mipmapFilterMode = Mipmapper.Mode.Gaussian3x3x3;
  public float indirectDiffuseModifier = 1f;
  public float indirectSpecularModifier = 1f;
  [Range(.1f, 1f)]
  public float diffuseResolutionScale = 1f;
  [Range(1f, 100f)]
  public float bound = 10f;
  public bool throttleTracing = false;
  [Range(1f, 100f)]
  public float tracingRate = 10f;
  public bool followCamera = false;

  public bool resolutionPlusOne {
    get { return mipmapFilterMode == Mipmapper.Mode.Gaussian3x3x3; }
  }
  public float BufferScale => CascadesEnabled ? 64f * CascadesCount : 64f / (_resolution - _resolution % 2);
  public float voxelSize {
    get { return bound / (_resolution - _resolution % 2); }
  }
  public int volume {
    get { return _resolution * _resolution * _resolution; }
  }
  public Camera Camera => _camera;
  public ComputeBuffer voxelBuffer {
    get { return _voxelBuffer; }
  }
  public List<LightSource> lights {
    get { return _lights; }
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

  internal bool CascadesEnabled { get; private set; }
  internal int CascadesCount { get; private set; }

  int _resolution = 0;
  float _previousTrace = 0f;
  Camera _camera;
  CommandBuffer _command;
  ComputeBuffer _lightSources;
  ComputeBuffer _voxelBuffer;
  List<LightSource> _lights;
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
    VXGIRenderPipeline.TriggerCameraCallback(Camera, "OnPreRender", Camera.onPreRender);

    _command.BeginSample(_command.name);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();

    UpdateResolution();

    float time = Time.unscaledTime;
    bool tracingThrottled = throttleTracing;

    if (tracingThrottled) {
      if (_previousTrace + 1f / tracingRate < time) {
        _previousTrace = time;

        PrePass(renderContext, renderer);
      }
    } else {
      PrePass(renderContext, renderer);
    }

    renderContext.SetupCameraProperties(camera);

    _command.ClearRenderTarget(
      (camera.clearFlags & CameraClearFlags.Depth) != 0,
      camera.clearFlags == CameraClearFlags.Color,
      camera.backgroundColor
    );
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();

    SetupShader(renderContext);

    VXGIRenderPipeline.TriggerCameraCallback(Camera, "OnPreCull", Camera.onPreCull);

    renderer.RenderDeferred(renderContext, camera, this);

    _command.EndSample(_command.name);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();

    VXGIRenderPipeline.TriggerCameraCallback(Camera, "OnPostRender", Camera.onPostRender);
  }

  void PrePass(ScriptableRenderContext renderContext, VXGIRenderer renderer) {
    if (followCamera) center = transform.position;

    var displacement = (voxelSpaceCenter - _lastVoxelSpaceCenter) / voxelSize;

    if (displacement.sqrMagnitude > 0f) {
      _mipmapper.Shift(renderContext, Vector3Int.RoundToInt(displacement));
    }

    _voxelizer.Voxelize(renderContext, renderer);
    _voxelShader.Render(renderContext);

    if (!CascadesEnabled) _mipmapper.Filter(renderContext);

    _lastVoxelSpaceCenter = voxelSpaceCenter;
  }

  void SetupShader(ScriptableRenderContext renderContext) {
    _lightSources.SetData(_lights);

    _command.SetGlobalBuffer(ShaderIDs.LightSources, _lightSources);
    _command.SetGlobalFloat(ShaderIDs.IndirectDiffuseModifier, indirectDiffuseModifier);
    _command.SetGlobalFloat(ShaderIDs.IndirectSpecularModifier, indirectSpecularModifier);
    _command.SetGlobalFloat(ShaderIDs.VXGI_VolumeExtent, .5f * bound);
    _command.SetGlobalFloat(ShaderIDs.VXGI_VolumeSize, bound);
    _command.SetGlobalInt(ShaderIDs.LightCount, _lights.Count);
    _command.SetGlobalInt(ShaderIDs.Resolution, _resolution);
    _command.SetGlobalInt(ShaderIDs.VXGI_CascadesCount, CascadesCount);
    _command.SetGlobalMatrix(ShaderIDs.WorldToVoxel, worldToVoxel);
    _command.SetGlobalVector(ShaderIDs.VXGI_VolumeCenter, center);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();
  }
  #endregion

  #region Messages
  void OnDrawGizmos() {
    Gizmos.color = Color.green;
    Gizmos.DrawWireCube(voxelSpaceCenter, Vector3.one * bound);

    if (CascadesEnabled) {
      for (int i = 1; i < CascadesCount; i++) {
        Gizmos.DrawWireCube(voxelSpaceCenter, Vector3.one * bound / Mathf.Pow(2, i));
      }
    }
  }

  void OnEnable() {
    _resolution = (int)resolution;

    _camera = GetComponent<Camera>();
    _command = new CommandBuffer { name = "VXGI.MonoBehaviour" };
    _lights = new List<LightSource>(64);
    _lightSources = new ComputeBuffer(64, LightSource.size);
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
    _lightSources.Dispose();
    _command.Dispose();
  }
  #endregion

  #region Buffers
  void CreateBuffers() {
    _voxelBuffer = new ComputeBuffer((int)(BufferScale * volume), VoxelData.size, ComputeBufferType.Append);
  }

  void DisposeBuffers() {
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
      msaaSamples = 1,
      sRGB = false
    };
  }

  void CreateTextures() {
    if (CascadesEnabled) {
      _radianceDescriptor.height = _radianceDescriptor.width = _resolution;
      _radianceDescriptor.volumeDepth = CascadesCount * _resolution;
      _radiances = new[] { new RenderTexture(_radianceDescriptor) };
      _radiances[0].Create();

      Shader.EnableKeyword("VXGI_CASCADES");
      Shader.SetGlobalTexture(ShaderIDs.Radiance[0], radiances[0]);
    } else {
      int resolutionModifier = _resolution % 2;

      _radiances = new RenderTexture[(int)Mathf.Log(_resolution, 2f)];

      for (
        int i = 0, currentResolution = _resolution;
        i < _radiances.Length;
        i++, currentResolution = (currentResolution - resolutionModifier) / 2 + resolutionModifier
      ) {
        _radianceDescriptor.height = _radianceDescriptor.width = _radianceDescriptor.volumeDepth = currentResolution;
        _radiances[i] = new RenderTexture(_radianceDescriptor);
        _radiances[i].Create();
      }

      Shader.DisableKeyword("VXGI_CASCADES");

      for (int i = 0; i < 9; i++) {
        Shader.SetGlobalTexture(ShaderIDs.Radiance[i], radiances[Mathf.Min(i, _radiances.Length - 1)]);
      }
    }
  }

  void DisposeTextures() {
    foreach (var radiance in _radiances) {
      radiance.DiscardContents();
      radiance.Release();
      DestroyImmediate(radiance);
    }
  }

  void ResizeTextures() {
    DisposeTextures();
    CreateTextures();
  }
  #endregion

  void UpdateResolution() {
    bool resize = false;

    if (CascadesEnabled != cascadesEnabled) {
      CascadesEnabled = cascadesEnabled;
      resize = true;
    }

    cascadesCount = Mathf.Clamp(cascadesCount, MinCascadesCount, MaxCascadesCount);

    if (CascadesCount != cascadesCount && CascadesEnabled) {
      CascadesCount = cascadesCount;
      resize = true;
    }

    int newResolution = (int)resolution;

    if (resolutionPlusOne && !CascadesEnabled) newResolution++;

    if (_resolution != newResolution) {
      _resolution = newResolution;
      resize = true;
    }

    if (resize) {
      ResizeBuffers();
      ResizeTextures();
    }
  }
}
