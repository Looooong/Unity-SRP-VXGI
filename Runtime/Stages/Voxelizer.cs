using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

public class Voxelizer : System.IDisposable {
  //Voxelizer settings
  public bool VoxelizeColors;
  public bool AnisotropicColors;
  public bool VoxelizeBinary;

  public int Resolution;
  public int AntiAliasing;//Not anti-aliasing, not sure of a better name though
  public int Cascades;
  public Vector3 Centre;
  public float Bound;

  //TODO: Implement
  //public bool WillMipmap;//Allocate an extra half a volume at the bottom to store mip maps in (won't use built-in mipmapping due to feature below)
  //public int VolumePadding;//For gaussian 3x3 will be 1, but may as well generalize

  //public IMipmapper Mipmapper = null;
  public StepMapper StepMapper = null;


  public Vector3Int ColorStorageResolution
  {
    get
    {
      return new Vector3Int(Resolution, Resolution * (AnisotropicColors ? 6 : 1), Resolution * Cascades);
    }
  }
  public Vector3Int PointerStorageResolution
  {
    get
    {
      return new Vector3Int(Resolution, Resolution, Resolution * Cascades);
    }
  }
  public Vector3Int BinaryStorageResolution
  {
    get
    {
      return new Vector3Int(Resolution, Resolution, Resolution * Cascades);
    }
  }

  internal int LightsourcesCount { get; private set; }
  internal ComputeBuffer LightSources { get; }
  public float BufferScale => Cascades * 64f / (Resolution - Resolution % 2);
  public float voxelSize
  {
    get { return Bound / Resolution; }
  }
  public int volume
  {
    get { return Resolution * Resolution * Resolution; }
  }
  public Camera Camera => _camera;
  public Matrix4x4 voxelToWorld
  {
    get { return Matrix4x4.TRS(origin, Quaternion.identity, Vector3.one * voxelSize); }
  }
  public Matrix4x4 worldToVoxel
  {
    get { return voxelToWorld.inverse; }
  }
  public Vector3 origin
  {
    get { return voxelSpaceCenter - Vector3.one * .5f * Bound; }
  }
  public Vector3 voxelSpaceCenter
  {
    get
    {
      var position = Centre;

      position /= voxelSize;
      position.x = Mathf.Floor(position.x);
      position.y = Mathf.Floor(position.y);
      position.z = Mathf.Floor(position.z);

      return position * voxelSize;
    }
  }

  Camera _camera;
  CommandBuffer _command;
  DrawingSettings _drawingSettings;
  FilteringSettings _filteringSettings;
  RenderTextureDescriptor _cameraDescriptor;
  ScriptableCullingParameters _cullingParameters;
  VoxelShader _voxelShader;

  public Voxelizer() {
    _command = new CommandBuffer();

    LightSources = new ComputeBuffer(128, LightSource.size);

    CreateCamera();
    CreateCameraDescriptor();
    CreateCameraSettings();
    _voxelShader = new VoxelShader(this);
  }

  public void Dispose() {
#if UNITY_EDITOR
    GameObject.DestroyImmediate(_camera.gameObject);
#else
    GameObject.Destroy(_camera.gameObject);
#endif

    LightSources.Dispose();
    _command.Dispose();
    _voxelShader.Dispose();
  }
  RenderTextureDescriptor _radianceDescriptor;
  RenderTextureDescriptor _pointerDescriptor;
  RenderTextureDescriptor _binaryDescriptor;

  public RenderTexture radiance;
  public RenderTexture binary;
  public ComputeBuffer voxelBuffer;
  public RenderTexture voxelPointerBuffer;
  public void UpdateStorage(bool existenceIsRequired)
  {
    _radianceDescriptor = new RenderTextureDescriptor()
    {
      graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat,
      dimension = TextureDimension.Tex3D,
      enableRandomWrite = true,
      msaaSamples = 1,
    };
    _binaryDescriptor = new RenderTextureDescriptor()
    {
      graphicsFormat = GraphicsFormat.R8_UNorm,
      dimension = TextureDimension.Tex3D,
      enableRandomWrite = true,
      msaaSamples = 1,
    };
    _pointerDescriptor = new RenderTextureDescriptor()
    {
      graphicsFormat = GraphicsFormat.R32_SInt,
      dimension = TextureDimension.Tex3D,
      enableRandomWrite = true,
      msaaSamples = 1,
    };

    radiance = TextureUtil.UpdateTexture(radiance, ColorStorageResolution, _radianceDescriptor, VoxelizeColors && existenceIsRequired);

    binary = TextureUtil.UpdateTexture(binary, BinaryStorageResolution, _binaryDescriptor, VoxelizeBinary && existenceIsRequired);


    voxelBuffer = TextureUtil.UpdateBuffer(voxelBuffer, (int)(BufferScale * volume), VoxelData.size, ComputeBufferType.Counter, VoxelizeColors && existenceIsRequired);
    voxelPointerBuffer = TextureUtil.UpdateTexture(voxelPointerBuffer, PointerStorageResolution, _pointerDescriptor, VoxelizeColors && existenceIsRequired);

    StepMapper?.UpdateStorage(VoxelizeBinary && existenceIsRequired);
  }

  public void Voxelize(ScriptableRenderContext renderContext, VXGIRenderer renderer) {
    UpdateCamera();

    for (
      int cascadeIndex = 0, drawsCount = Cascades, divisor = 1 << (drawsCount - 1);
      cascadeIndex < drawsCount;
      cascadeIndex++, divisor >>= 1
    ) {
      var extent = .5f * Bound / divisor;

      _camera.farClipPlane = extent;
      _camera.nearClipPlane = -extent;
      _camera.orthographicSize = extent;

      if (!_camera.TryGetCullingParameters(out _cullingParameters)) continue;

      var cullingResults = renderContext.Cull(ref _cullingParameters);

      UpdateLightSources(cullingResults);

      _command.name = $"VXGI.Voxelizer.{cascadeIndex}.{extent}";
      _command.BeginSample(_command.name);
      _command.SetRenderTarget(binary, 0, CubemapFace.Unknown, -1);
      _command.ClearRenderTarget(true, true, Color.clear);
      _command.GetTemporaryRT(ShaderIDs.Dummy, _cameraDescriptor);
      _command.SetRenderTarget(ShaderIDs.Dummy, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare);
      _command.SetGlobalInt(ShaderIDs.Resolution, Resolution);
      _command.SetGlobalInt(ShaderIDs.VXGI_CascadeIndex, cascadeIndex);

      _command.SetGlobalFloat(ShaderIDs.VXGI_VolumeExtent, .5f * Bound);
      _command.SetGlobalFloat(ShaderIDs.VXGI_VolumeSize, Bound);
      _command.SetGlobalInt(ShaderIDs.VXGI_CascadesCount, Cascades);
      _command.SetGlobalMatrix(ShaderIDs.VoxelToWorld, voxelToWorld);
      _command.SetGlobalMatrix(ShaderIDs.WorldToVoxel, worldToVoxel);

      if (VoxelizeColors)
      {
        _command.SetRandomWriteTarget(1, voxelBuffer, cascadeIndex > 0);
        _command.SetRandomWriteTarget(2, voxelPointerBuffer);
      }
      if (VoxelizeBinary)
      {
        _command.SetRandomWriteTarget(3, binary);
      }
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


    if (VoxelizeColors)
    {
      _voxelShader.Render(renderContext);
      voxelBuffer.SetCounterValue(0);
    }
    if (VoxelizeBinary)
    {
      StepMapper?.Filter(renderContext);
    }
  }

  void CreateCamera() {
    var gameObject = new GameObject("__VOXELIZER__") { hideFlags = HideFlags.HideAndDontSave };
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
    _cameraDescriptor.msaaSamples = AntiAliasing;

    _cameraDescriptor.height = _cameraDescriptor.width = Resolution;

    _camera.transform.position = Centre;
  }

  void UpdateLightSources(CullingResults cullingResults) {
    var data = new NativeArray<LightSource>(128, Allocator.Temp);
    LightsourcesCount = 0;

    for (int i = 0; i < data.Length && i < cullingResults.visibleLights.Length; i++) {
      var light = cullingResults.visibleLights[i];

      if (VXGI.SupportedLightTypes.Contains(light.lightType) && light.finalColor.maxColorComponent > 0f) {
        data[LightsourcesCount++] = new LightSource(light, this);
      }
    }

    LightSources.SetData(data);
    data.Dispose();
  }
}
