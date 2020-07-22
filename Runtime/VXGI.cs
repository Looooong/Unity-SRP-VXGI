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
    [InspectorName("VeryHigh (256^3)")] VeryHigh = 256
  }

  [Header("Voxel Volume")]
  [Tooltip("Make the voxel volume center follow the camera position.")]
  public bool followCamera = false;
  [Tooltip("The center of the voxel volume in World Space")]
  public Vector3 center;
  [Min(0.001f), Tooltip("The size of the voxel volume in World Space.")]
  public float bound = 10f;
  [Tooltip("The resolution of the voxel volume.")]
  public Resolution resolution = Resolution.Medium;
  [Tooltip("The anti-aliasing level of the voxelization process.")]
  public AntiAliasing antiAliasing = AntiAliasing.X1;
  [Tooltip(
@"Specify the method to generate the voxel mipmap volume:
Box: fast, 2^n voxel resolution.
Gaussian 3x3x3: fast, 2^n+1 voxel resolution (recommended).
Gaussian 4x4x4: slow, 2^n voxel resolution."
  )]
  public Mipmapper.Mode mipmapFilterMode = Mipmapper.Mode.Box;
  [Tooltip("Limit the voxel volume refresh rate.")]
  public bool limitRefreshRate = false;
  [Min(0f), Tooltip("The target refresh rate of the voxel volume.")]
  public float refreshRate = 30f;

  [Header("Rendering")]
  [Min(0f), Tooltip("How strong the diffuse cone tracing can affect the scene.")]
  public float indirectDiffuseModifier = 1f;
  [Min(0f), Tooltip("How strong the specular cone tracing can affect the scene.")]
  public float indirectSpecularModifier = 1f;
  [Range(.1f, 1f), Tooltip("Downscale the diffuse cone tracing pass.")]
  public float diffuseResolutionScale = 1f;

  public bool resolutionPlusOne {
    get { return mipmapFilterMode == Mipmapper.Mode.Gaussian3x3x3; }
  }
  public float bufferScale {
    get { return 64f / (_resolution - _resolution % 2); }
  }
  public float voxelSize {
    get { return bound / (_resolution - _resolution % 2); }
  }
  public int volume {
    get { return _resolution * _resolution * _resolution; }
  }
  public Camera Camera { get; private set; }
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

  int _resolution = 0;
  float _previousRefresh = 0f;
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
    Render(renderContext, Camera, renderer);
  }

  public void Render(ScriptableRenderContext renderContext, Camera camera, VXGIRenderer renderer) {
    VXGIRenderPipeline.TriggerCameraCallback(Camera, "OnPreRender", Camera.onPreRender);

    _command.BeginSample(_command.name);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();

    UpdateResolution();

    var time = Time.realtimeSinceStartup;

    if (!limitRefreshRate || (_previousRefresh + 1f / refreshRate < time)) {
      _previousRefresh = time;

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
    _mipmapper.Filter(renderContext);

    _lastVoxelSpaceCenter = voxelSpaceCenter;
  }

  void SetupShader(ScriptableRenderContext renderContext) {
    _lightSources.SetData(_lights);

    _command.SetGlobalBuffer(ShaderIDs.LightSources, _lightSources);
    _command.SetGlobalFloat(ShaderIDs.IndirectDiffuseModifier, indirectDiffuseModifier);
    _command.SetGlobalFloat(ShaderIDs.IndirectSpecularModifier, indirectSpecularModifier);
    _command.SetGlobalInt(ShaderIDs.LightCount, _lights.Count);
    _command.SetGlobalInt(ShaderIDs.Resolution, _resolution);
    _command.SetGlobalMatrix(ShaderIDs.WorldToVoxel, worldToVoxel);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();
  }
  #endregion

  #region Messages
  void OnDrawGizmosSelected() {
    Gizmos.color = Color.green;
    Gizmos.DrawWireCube(voxelSpaceCenter, Vector3.one * bound);
  }

  void OnEnable() {
    _resolution = (int)resolution;

    Camera = GetComponent<Camera>();
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
    _voxelBuffer = new ComputeBuffer((int)(bufferScale * volume), VoxelData.size, ComputeBufferType.Append);
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

    for (int i = 0; i < 9; i++) {
      Shader.SetGlobalTexture(ShaderIDs.Radiance[i], radiances[Mathf.Min(i, _radiances.Length - 1)]);
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
    int newResolution = (int)resolution;

    if (resolutionPlusOne) newResolution++;

    if (_resolution != newResolution) {
      _resolution = newResolution;
      ResizeBuffers();
      ResizeTextures();
    }
  }
}
