using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
[AddComponentMenu("Rendering/VXGI")]
public class VXGI : MonoBehaviour {
  public enum AntiAliasing { X1 = 1, X2 = 2, X4 = 4, X8 = 8 }
  public enum Resolution {
    [InspectorName("Low (32^3)")] Low = 32,
    [InspectorName("Medium (64^3)")] Medium = 64,
    [InspectorName("High (128^3)")] High = 128,
    [InspectorName("Very High (256^3)")] VeryHigh = 256,
  }
  public enum BinaryResolution
  {
    [InspectorName("Low (128^3)")] Low = 128,
    [InspectorName("Medium (256^3)")] Medium = 256,
    [InspectorName("Decent (512^3)")] Decent = 512,
  }

  public readonly static ReadOnlyCollection<LightType> SupportedLightTypes = new ReadOnlyCollection<LightType>(new[] {
    LightType.Point, LightType.Directional, LightType.Spot
  });

  public const int MaxCascadesCount = 8;
  public const int MinCascadesCount = 1;

  public bool anisotropicVoxel;
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

  public enum LightingMethod
  {
    Cones,
    Rays
  };
  public LightingMethod lightingMethod = LightingMethod.Cones;
  public BinaryResolution binaryResolution = BinaryResolution.Decent;

  [Range(.1f, 1f)]
  public float diffuseResolutionScale = 1f;
  [Range(1f, 256f)]
  public float bound = 10f;
  public bool throttleTracing = false;
  [Range(1f, 100f)]
  public float tracingRate = 10f;
  public bool followCamera = false;

  public bool resolutionPlusOne {
    get { return mipmapFilterMode == Mipmapper.Mode.Gaussian3x3x3; }
  }
  public Camera Camera => _camera;
  public Parameterizer parameterizer {
    get { return _parameterizer; }
  }

  Voxelizer colorVoxelizer;
  Voxelizer binaryVoxelizer;
  internal Voxelizer ColorVoxelizer { get { return colorVoxelizer; } }
  internal Voxelizer BinaryVoxelizer { get { return ((int)binaryResolution == (int)resolution || binaryVoxelizer==null) ? colorVoxelizer : binaryVoxelizer; } }

  bool RequiresColors { get { return true || RequiresColorMipmaps; } }
  bool RequiresColorMipmaps { get { return lightingMethod == LightingMethod.Cones; } }
  bool RequiresBinary { get { return lightingMethod == LightingMethod.Rays || RequiresBinaryStepMap; } }
  bool RequiresBinaryStepMap { get { return lightingMethod == LightingMethod.Rays; } }

  float _previousTrace = 0f;
  Camera _camera;
  CommandBuffer _command;
  Mipmapper _mipmapper;
  Parameterizer _parameterizer;
  //Vector3 _lastVoxelSpaceCenter;

  #region Rendering
  public void Render(ScriptableRenderContext renderContext, VXGIRenderer renderer) {
    Render(renderContext, _camera, renderer);
  }

  public void Render(ScriptableRenderContext renderContext, Camera camera, VXGIRenderer renderer) {
    VXGIRenderPipeline.TriggerCameraCallback(Camera, "OnPreRender", Camera.onPreRender);

    _command.BeginSample(_command.name);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();

    UpdateStorage(true);
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

    //var displacement = (voxelSpaceCenter - _lastVoxelSpaceCenter) / voxelSize;

    //if (displacement.sqrMagnitude > 0f) {
    //  _mipmapper.Shift(renderContext, Vector3Int.RoundToInt(displacement));
    //}
    if (RequiresColors || RequiresBinary)
    {
      //if (colorVoxelizer != null) Debug.Log("colorVoxelizer");
      colorVoxelizer?.Voxelize(renderContext, renderer);
      //if (binaryVoxelizer != null) Debug.Log("binaryVoxelizer");
      binaryVoxelizer?.Voxelize(renderContext, renderer);
    }

    if (RequiresColorMipmaps)
    {
      //_mipmapper.Filter(renderContext);
    }

    //_lastVoxelSpaceCenter = voxelSpaceCenter;
  }

  void SetupShaderKeywords(ScriptableRenderContext renderContext) {
    if (anisotropicVoxel) {
      _command.EnableShaderKeyword("VXGI_ANISOTROPIC_VOXEL");
    } else {
      _command.DisableShaderKeyword("VXGI_ANISOTROPIC_VOXEL");
    }

    _command.EnableShaderKeyword("VXGI_CASCADES");

    if (RequiresColors) {
      _command.EnableShaderKeyword("VXGI_COLOR");
    } else {
      _command.DisableShaderKeyword("VXGI_COLOR");
    }

    if (RequiresBinary) {
      _command.EnableShaderKeyword("VXGI_BINARY");
    } else {
      _command.DisableShaderKeyword("VXGI_BINARY");
    }

    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();
  }

  void SetupShaderVariables(ScriptableRenderContext renderContext) {
    _command.SetGlobalTexture(ShaderIDs.Radiance[0], ColorVoxelizer.radiance);
    _command.SetGlobalTexture(ShaderIDs.StepMap, BinaryVoxelizer.StepMapper?.stepmap);
    _command.SetGlobalTexture(ShaderIDs.StepMapFine2x2x2Encode, BinaryVoxelizer.StepMapper?.StepMapFine2x2x2Encode);
    _command.SetGlobalTexture(ShaderIDs.Binary, BinaryVoxelizer.binary);

    _command.SetGlobalFloat(ShaderIDs.IndirectDiffuseModifier, indirectDiffuseModifier);
    _command.SetGlobalFloat(ShaderIDs.IndirectSpecularModifier, indirectSpecularModifier);
    _command.SetGlobalFloat(ShaderIDs.VXGI_VolumeExtent, .5f * bound);
    _command.SetGlobalFloat(ShaderIDs.VXGI_VolumeSize, bound);
    _command.SetGlobalInt(ShaderIDs.Resolution, ColorVoxelizer.ColorStorageResolution.x);
    _command.SetGlobalInt(ShaderIDs.BinaryResolution, BinaryVoxelizer.BinaryStorageResolution.x);
    _command.SetGlobalInt(ShaderIDs.StepMapResolution, BinaryVoxelizer.StepMapper.StepMapStorageResolution.x);
    _command.SetGlobalFloat(ShaderIDs.BinaryVoxelSize, bound*2.0f/(float)BinaryVoxelizer.BinaryStorageResolution.x);
    _command.SetGlobalInt(ShaderIDs.VXGI_CascadesCount, cascadesCount);
    _command.SetGlobalMatrix(ShaderIDs.WorldToVoxel, ColorVoxelizer.worldToVoxel);
    _command.SetGlobalVector(ShaderIDs.VXGI_VolumeCenter, ColorVoxelizer.voxelSpaceCenter);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();
  }
  #endregion

  #region Messages
  void OnDrawGizmos() {
    Gizmos.color = Color.green;
    Gizmos.DrawWireCube(ColorVoxelizer.voxelSpaceCenter, Vector3.one * ColorVoxelizer.Bound);

      for (int i = 1; i < cascadesCount; i++) {
        Gizmos.DrawWireCube(ColorVoxelizer.voxelSpaceCenter, Vector3.one * ColorVoxelizer.Bound / Mathf.Pow(2, i));
      }
  }

  void OnEnable() {

    _camera = GetComponent<Camera>();
    _command = new CommandBuffer { name = "VXGI.MonoBehaviour" };
    _mipmapper = new Mipmapper(this);
    _parameterizer = new Parameterizer();
    //_lastVoxelSpaceCenter = voxelSpaceCenter;

    UpdateStorage(true);
  }

  void OnDisable() {
    UpdateStorage(false);

    colorVoxelizer?.Dispose();
    binaryVoxelizer?.Dispose();
    _parameterizer.Dispose();
    _mipmapper.Dispose();
    _command.Dispose();
  }
  #endregion

  void UpdateStorage(bool existenceIsRequired)
  {
    cascadesCount = Mathf.Clamp(cascadesCount, MinCascadesCount, MaxCascadesCount);

    if (colorVoxelizer == null)
    {
      colorVoxelizer = new Voxelizer();
    }
    if (RequiresBinary && (int)resolution == (int)binaryResolution)
    {
      if (colorVoxelizer.StepMapper == null)
      {
        colorVoxelizer.StepMapper = new StepMapper(colorVoxelizer);
      }
      binaryVoxelizer?.Dispose();
      binaryVoxelizer = null;
    }
    else if (RequiresBinary)
    {
      colorVoxelizer.StepMapper?.Dispose();
      colorVoxelizer.StepMapper = null;
      if (binaryVoxelizer == null)
      {
        binaryVoxelizer = new Voxelizer();
        binaryVoxelizer.StepMapper = new StepMapper(binaryVoxelizer);
      }
    }


    if (colorVoxelizer!=null)
    {
      colorVoxelizer.VoxelizeColors = RequiresColors;
      colorVoxelizer.AnisotropicColors = anisotropicVoxel;
      colorVoxelizer.VoxelizeBinary = RequiresBinary && binaryVoxelizer == null;
      colorVoxelizer.Resolution = (int)resolution;
      colorVoxelizer.AntiAliasing = (int)antiAliasing;
      colorVoxelizer.Centre = center;
      colorVoxelizer.Bound = bound;
      colorVoxelizer.Cascades = cascadesCount;
      colorVoxelizer.UpdateStorage(existenceIsRequired);
    }
    if (binaryVoxelizer != null)
    {
      binaryVoxelizer.VoxelizeColors = false;
      binaryVoxelizer.AnisotropicColors = anisotropicVoxel;
      binaryVoxelizer.VoxelizeBinary = RequiresBinary;
      binaryVoxelizer.Resolution = (int)binaryResolution;
      binaryVoxelizer.AntiAliasing = (int)antiAliasing;
      binaryVoxelizer.Centre = center;
      binaryVoxelizer.Bound = bound;
      binaryVoxelizer.Cascades = cascadesCount;
      binaryVoxelizer.UpdateStorage(existenceIsRequired);
    }
  }
}
