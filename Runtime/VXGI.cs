using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEditor;
using System;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
[AddComponentMenu("Rendering/VXGI")]
public class VXGI : MonoBehaviour {
  public enum AntiAliasing { X1 = 1, X2 = 2, X4 = 4, X8 = 8 }
  public enum Resolution {
    [InspectorName("Low (32^3)        0.25mb")] Low = 32,
    [InspectorName("Medium (64^3)     2mb")] Medium = 64,
    [InspectorName("High (128^3)      16mb")] High = 128,
    [InspectorName("Very High (256^3) 128mb")] VeryHigh = 256,
  }
  public enum BinaryResolution
  {
    [InspectorName("Low (128^3)    2mb")] Low = 128,
    [InspectorName("Medium (256^3) 16mb")] Medium = 256,
    [InspectorName("Decent (512^3) 128mb")] Decent = 512,
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
  public Resolution resolution = Resolution.VeryHigh;
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
  public LightingMethod lightingMethod = LightingMethod.Rays;
  public BinaryResolution binaryResolution = BinaryResolution.Medium;

  [Range(.1f, 1f)]
  public float diffuseResolutionScale = 1f;
  [Range(1f, 256f)]
  public float bound = 10f;
  public bool throttleTracing = false;
  [Range(1f, 100f)]
  public float tracingRate = 10f;
  public bool followCamera = false;

  int NoiseNum = 0;
  public bool AnimateNoise = false;
  public bool AllowUnsafeValues = false;
  public int PerPixelPerLightShadowRays = 5;
  public int PerPixelGIRays = 4;
  public int PerVoxelGIRays = 1;
  public Color AmbientColor;

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
  internal Voxelizer BinaryVoxelizer { get { return ((int)binaryResolution == (int)clampedColorResolution || binaryVoxelizer==null) ? colorVoxelizer : binaryVoxelizer; } }

  int clampedColorResolution
  {
    get
    {
      return (RequiresBinary && (int)binaryResolution < (int)resolution) ? (int)binaryResolution : (int)resolution;
    }
  }

  public bool RequiresColors { get { return true || RequiresColorMipmaps; } }
  public bool RequiresColorMipmaps { get { return lightingMethod == LightingMethod.Cones; } }
  public bool RequiresBinary { get { return lightingMethod == LightingMethod.Rays || RequiresBinaryStepMap; } }
  public bool RequiresBinaryStepMap { get { return lightingMethod == LightingMethod.Rays; } }

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

    if (AnimateNoise)
      NoiseNum += 1;
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

    _command.SetGlobalColor(ShaderIDs.TempSkyColor, AmbientColor);
    _command.SetGlobalFloat(ShaderIDs.IndirectDiffuseModifier, indirectDiffuseModifier);
    _command.SetGlobalFloat(ShaderIDs.IndirectSpecularModifier, indirectSpecularModifier);
    _command.SetGlobalFloat(ShaderIDs.VXGI_VolumeExtent, .5f * bound);
    _command.SetGlobalFloat(ShaderIDs.VXGI_VolumeSize, bound);
    _command.SetGlobalFloat(ShaderIDs.NoiseNum, (float)NoiseNum);
    _command.SetGlobalInt(ShaderIDs.PerPixelGIRayCount, PerPixelGIRays);
    _command.SetGlobalInt(ShaderIDs.PerVoxelGIRayCount, PerVoxelGIRays);
    _command.SetGlobalInt(ShaderIDs.PerPixelGIRayCountSqrt, (int)Math.Sqrt(PerPixelGIRays));
    _command.SetGlobalInt(ShaderIDs.PerVoxelGIRayCountSqrt, (int)Math.Sqrt(PerVoxelGIRays));
    _command.SetGlobalInt(ShaderIDs.Resolution, ColorVoxelizer.ColorStorageResolution.x);
    _command.SetGlobalInt(ShaderIDs.BinaryResolution, BinaryVoxelizer.BinaryStorageResolution.x);
    _command.SetGlobalInt(ShaderIDs.StepMapResolution, BinaryVoxelizer.StepMapper.StepMapStorageResolution.x);
    _command.SetGlobalFloat(ShaderIDs.BinaryVoxelSize, bound*2.0f/(float)BinaryVoxelizer.BinaryStorageResolution.x);
    _command.SetGlobalInt(ShaderIDs.VXGI_CascadesCount, cascadesCount);
    _command.SetGlobalMatrix(ShaderIDs.WorldToVoxel, BinaryVoxelizer.worldToVoxel);
    _command.SetGlobalVector(ShaderIDs.VXGI_VolumeCenter, BinaryVoxelizer.renderedVoxelSpaceCenter);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();
  }
  #endregion

  #region Messages
  void OnDrawGizmos() {
    Gizmos.color = Color.green;
    Gizmos.DrawWireCube(ColorVoxelizer.renderedVoxelSpaceCenter, Vector3.one * ColorVoxelizer.Bound);

      for (int i = 1; i < cascadesCount; i++) {
        Gizmos.DrawWireCube(ColorVoxelizer.renderedVoxelSpaceCenter, Vector3.one * ColorVoxelizer.Bound / Mathf.Pow(2, i));
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
    colorVoxelizer = null;
    binaryVoxelizer = null;
    _parameterizer.Dispose();
    _mipmapper.Dispose();
    _command.Dispose();
  }
  #endregion

  void UpdateStorage(bool existenceIsRequired)
  {
    if (followCamera) center = transform.position;
    cascadesCount = Mathf.Clamp(cascadesCount, MinCascadesCount, MaxCascadesCount);

    if (colorVoxelizer == null)
    {
      colorVoxelizer = new Voxelizer();
    }
    if (RequiresBinary && (int)clampedColorResolution == (int)binaryResolution)
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
      colorVoxelizer.Resolution = (int)clampedColorResolution;
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

[CustomEditor(typeof(VXGI))]
public class VXGIEditor : Editor
{
  Func<Enum, bool> showEnumValue = ShowEnumValue;
  // my custom function
  public static bool ShowEnumValue(Enum lightingMethod)
  {
    return (VXGI.LightingMethod)lightingMethod == VXGI.LightingMethod.Rays;
  }
  public int SafeIntSlider(string text, int value, int range0, int range1, bool notsafe)
  {
    if (!notsafe)
      return EditorGUILayout.IntSlider(text, value, range0, range1);
    else
      return EditorGUILayout.IntField(text, value);
  }
  public override void OnInspectorGUI()
  {
    var _vxgi = target as VXGI;


    EditorGUILayout.LabelField("Lighting", EditorStyles.boldLabel);
    _vxgi.lightingMethod = (VXGI.LightingMethod)EditorGUILayout.EnumPopup(new GUIContent(""), _vxgi.lightingMethod, showEnumValue, true);// EditorGUILayout.EnumPopup(new GUIContent(""), myScript.lightingMethod, ShowEnumValue, false);
    _vxgi.diffuseResolutionScale = EditorGUILayout.Slider("Resolution Scale", _vxgi.diffuseResolutionScale, 0.1f, 1.0f);
    _vxgi.throttleTracing = EditorGUILayout.Toggle("Throttle Tracing", _vxgi.throttleTracing);
    if (_vxgi.throttleTracing)
      _vxgi.tracingRate = EditorGUILayout.Slider("Tracing Rate", _vxgi.tracingRate, 1.0f, 100.0f);
    if (_vxgi.lightingMethod == VXGI.LightingMethod.Rays)
    {
      _vxgi.AllowUnsafeValues = EditorGUILayout.Toggle("Allow Unsafe Values", _vxgi.AllowUnsafeValues);
      _vxgi.PerPixelGIRays = SafeIntSlider("Per-Pixel GI Quality", (int)Math.Sqrt((double)_vxgi.PerPixelGIRays), 0, 10, _vxgi.AllowUnsafeValues);
      _vxgi.PerPixelGIRays *= _vxgi.PerPixelGIRays;
      if (_vxgi.PerPixelGIRays == 0)
        EditorGUILayout.LabelField("Per Pixel: Disabled");
      else
        EditorGUILayout.LabelField("Per Pixel: " + _vxgi.PerPixelGIRays.ToString() + " rays");

      _vxgi.PerPixelPerLightShadowRays = SafeIntSlider("Per-Pixel Shadow Rays for light with radius 1", _vxgi.PerPixelPerLightShadowRays, 0, 20, _vxgi.AllowUnsafeValues);

      _vxgi.PerVoxelGIRays = SafeIntSlider("Per-Voxel GI Quality", (int)Math.Sqrt((double)_vxgi.PerVoxelGIRays), 0, 5, _vxgi.AllowUnsafeValues);
      _vxgi.PerVoxelGIRays *= _vxgi.PerVoxelGIRays;
      if (_vxgi.PerVoxelGIRays == 0)
        EditorGUILayout.LabelField("Per Voxel: Disabled");
      else
        EditorGUILayout.LabelField("Per Voxel: " + _vxgi.PerVoxelGIRays.ToString() + " rays");

      _vxgi.AnimateNoise = EditorGUILayout.Toggle("Animate Per-Pixel Noise", _vxgi.AnimateNoise);
      _vxgi.AmbientColor = EditorGUILayout.ColorField("Sky Color", _vxgi.AmbientColor);
      _vxgi.indirectDiffuseModifier = EditorGUILayout.FloatField("Indirect Diffuse Modifier", _vxgi.indirectDiffuseModifier);
      GUI.enabled = false;
        _vxgi.indirectSpecularModifier = EditorGUILayout.FloatField("Indirect Specular Modifier", _vxgi.indirectSpecularModifier);
      _vxgi.indirectSpecularModifier = 0.0f;
      GUI.enabled = true;
    }


    EditorGUILayout.LabelField("Voxelization", EditorStyles.boldLabel);
    GUI.enabled = false;
      _vxgi.cascadesCount = EditorGUILayout.IntSlider("Cascades", _vxgi.cascadesCount, VXGI.MinCascadesCount, VXGI.MaxCascadesCount);
      _vxgi.cascadesCount = 1;
    GUI.enabled = true;
    _vxgi.bound = EditorGUILayout.Slider("Bounds", _vxgi.bound, 0f, 256f);

    _vxgi.followCamera = EditorGUILayout.Toggle("Follow Camera", _vxgi.followCamera);
    GUI.enabled = !_vxgi.followCamera;
      _vxgi.center = EditorGUILayout.Vector3Field("Center", _vxgi.center);
    GUI.enabled = true;
    GUI.enabled = false;
      _vxgi.anisotropicVoxel = EditorGUILayout.Toggle("Anistropic Colors", _vxgi.anisotropicVoxel);
      _vxgi.anisotropicVoxel = false;
    GUI.enabled = true;
    _vxgi.resolution = (VXGI.Resolution)EditorGUILayout.EnumPopup("Color Resolution", _vxgi.resolution);
    if (_vxgi.RequiresBinary)
    {
      _vxgi.binaryResolution = (VXGI.BinaryResolution)EditorGUILayout.EnumPopup("Binary Resolution", _vxgi.binaryResolution);

      if ((int)_vxgi.binaryResolution < (int)_vxgi.resolution)
      {
        EditorGUILayout.HelpBox("Color resolution must <= to binary resolution.", MessageType.Error);
      }
      if ((int)_vxgi.binaryResolution != (int)_vxgi.resolution)
      {
        EditorGUILayout.HelpBox("Currently there's a slow down when the two resolutions don't match, hoping to improve at some point.", MessageType.Warning);
      }
    }
    _vxgi.antiAliasing = (VXGI.AntiAliasing)EditorGUILayout.EnumPopup("Anti Aliasing", _vxgi.antiAliasing);
  }
}
