using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
[AddComponentMenu("Rendering/VXGI")]
public class VXGI : MonoBehaviour {
  public enum AntiAliasing { X1 = 1, X2 = 2, X4 = 4, X8 = 8 }
  public enum Resolution {
    [InspectorName("Low (32^3)")] Low = 32,
    [InspectorName("Medium (64^3)")] Medium = 64,
    [InspectorName("High (128^3)")] High = 128,
    [InspectorName("Very High (256^3)")] VeryHigh = 256
  }

  public readonly static ReadOnlyCollection<LightType> SupportedLightTypes = new ReadOnlyCollection<LightType>(new[] {
    LightType.Point, LightType.Directional, LightType.Spot
  });

  public const int MaxCascadesCount = 8;
  public const int MinCascadesCount = 4;

  public bool anisotropicVoxel;
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
  [Range(1f, 200f)]
  public float bound = 10f;
  public bool throttleTracing = false;
  [Range(1f, 100f)]
  public float tracingRate = 10f;
  public bool followCamera = false;

  public bool resolutionPlusOne {
    get { return mipmapFilterMode == Mipmapper.Mode.Gaussian3x3x3; }
  }
  public float BufferScale => (CascadesEnabled ? CascadesCount : 1f) * 64f / (_resolution - _resolution % 2);
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
  public RenderTexture voxelPointerBuffer{
    get { return _voxelPointerBuffer; }
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

  internal bool AnisotropicVoxel { get; private set; }
  internal bool CascadesEnabled { get; private set; }
  internal int CascadesCount { get; private set; }
  internal Voxelizer Voxelizer { get; private set; }

  int _resolution = 0;
  float _previousTrace = 0f;
  Camera _camera;
  CommandBuffer _command;
  ComputeBuffer _voxelBuffer;
  RenderTexture _voxelPointerBuffer;
  Mipmapper _mipmapper;
  Parameterizer _parameterizer;
  RenderTexture[] _radiances;
  RenderTextureDescriptor _radianceDescriptor;
  Vector3 _lastVoxelSpaceCenter;
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
    SetupShaderKeywords(renderContext);
    SetupShaderVariables(renderContext);

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

    VXGIRenderPipeline.TriggerCameraCallback(Camera, "OnPreCull", Camera.onPreCull);

    SetupShaderVariables(renderContext);
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

    Voxelizer.Voxelize(renderContext, renderer);
    _voxelShader.Render(renderContext);
    _voxelBuffer.SetCounterValue(0);

    if (!CascadesEnabled) _mipmapper.Filter(renderContext);

    _lastVoxelSpaceCenter = voxelSpaceCenter;
  }

  void SetupShaderKeywords(ScriptableRenderContext renderContext) {
    if (AnisotropicVoxel) {
      _command.EnableShaderKeyword("VXGI_ANISOTROPIC_VOXEL");
    } else {
      _command.DisableShaderKeyword("VXGI_ANISOTROPIC_VOXEL");
    }

    if (CascadesEnabled) {
      _command.EnableShaderKeyword("VXGI_CASCADES");
    } else {
      _command.DisableShaderKeyword("VXGI_CASCADES");
    }

    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();
  }

  void SetupShaderVariables(ScriptableRenderContext renderContext) {
    foreach (var radiance in radiances) {
      if (!radiance.IsCreated()) radiance.Create();
    }

    if (CascadesEnabled) {
      _command.SetGlobalTexture(ShaderIDs.Radiance[0], radiances[0]);
    } else {
      for (int i = 0; i < 9; i++) {
        _command.SetGlobalTexture(ShaderIDs.Radiance[i], radiances[Mathf.Min(i, _radiances.Length - 1)]);
      }
    }

    _command.SetGlobalFloat(ShaderIDs.IndirectDiffuseModifier, indirectDiffuseModifier);
    _command.SetGlobalFloat(ShaderIDs.IndirectSpecularModifier, indirectSpecularModifier);
    _command.SetGlobalFloat(ShaderIDs.VXGI_VolumeExtent, .5f * bound);
    _command.SetGlobalFloat(ShaderIDs.VXGI_VolumeSize, bound);
    _command.SetGlobalInt(ShaderIDs.Resolution, _resolution);
    _command.SetGlobalInt(ShaderIDs.VXGI_CascadesCount, CascadesCount);
    _command.SetGlobalMatrix(ShaderIDs.WorldToVoxel, worldToVoxel);
    _command.SetGlobalVector(ShaderIDs.VXGI_VolumeCenter, voxelSpaceCenter);
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
    _mipmapper = new Mipmapper(this);
    _parameterizer = new Parameterizer();
    Voxelizer = new Voxelizer(this);
    _voxelShader = new VoxelShader(this);
    _lastVoxelSpaceCenter = voxelSpaceCenter;

    UpdateResolutionVars();
    CreateBuffers();
    CreateTextureDescriptor();
    CreateTextures();
  }

  void OnDisable() {
    DisposeTextures();
    DisposeBuffers();

    _voxelShader.Dispose();
    Voxelizer.Dispose();
    _parameterizer.Dispose();
    _mipmapper.Dispose();
    _command.Dispose();
  }
  #endregion

  #region Buffers
  void CreateBuffers() {
    _voxelBuffer = new ComputeBuffer((int)(BufferScale * volume), VoxelData.size, ComputeBufferType.Counter);
    _voxelPointerBuffer = new RenderTexture(new RenderTextureDescriptor()
    {
      colorFormat = RenderTextureFormat.RInt,
      dimension = TextureDimension.Tex3D,
      enableRandomWrite = true,
      msaaSamples = 1,
      sRGB = false,
      height = _resolution,
      width = _resolution,
      volumeDepth = _resolution * CascadesCount
    });
    _voxelPointerBuffer.enableRandomWrite = true;
  }

  void DisposeBuffers() {
    _voxelBuffer.Dispose();
    _voxelPointerBuffer.Release();
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

      if (AnisotropicVoxel) _radianceDescriptor.height *= 6;

      _radiances = new[] { new RenderTexture(_radianceDescriptor) };
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
      }
    }
  }

  void DisposeTextures() {
    foreach (var radiance in _radiances) {
      if (radiance.IsCreated()) radiance.Release();
      DestroyImmediate(radiance);
    }
  }

  void ResizeTextures() {
    DisposeTextures();
    CreateTextures();
  }
  #endregion

  bool UpdateResolutionVars() {
    bool resize = false;

    if (AnisotropicVoxel != anisotropicVoxel) {
      AnisotropicVoxel = anisotropicVoxel;
      resize = true;
    }

    if (CascadesEnabled != cascadesEnabled) {
      CascadesEnabled = cascadesEnabled;
      resize = true;
    }

    cascadesCount = Mathf.Clamp(cascadesCount, MinCascadesCount, MaxCascadesCount);
    int realCascadesCount = CascadesEnabled ? cascadesCount : 1;

    if (CascadesCount != realCascadesCount) {
      CascadesCount = realCascadesCount;
      resize = true;
    }

    int newResolution = (int)resolution;

    if (resolutionPlusOne && !CascadesEnabled) newResolution++;

    if (_resolution != newResolution) {
      _resolution = newResolution;
      resize = true;
    }
    return resize;
  }

  void UpdateResolution() {

    if (UpdateResolutionVars()) {
      ResizeBuffers();
      ResizeTextures();
    }
  }
}
