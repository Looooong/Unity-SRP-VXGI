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
    colorVoxelizer = null;
    binaryVoxelizer = null;
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

#if UNITY_EDITOR

[CustomEditor(typeof(VXGI))]
public class VXGIEditor : Editor
{
  Func<Enum, bool> showEnumValue = ShowEnumValue;
  // my custom function
  public static bool ShowEnumValue(Enum lightingMethod)
  {
    return (VXGI.LightingMethod)lightingMethod == VXGI.LightingMethod.Rays;
  }
  public override void OnInspectorGUI()
  {
    var myScript = target as VXGI;


    EditorGUILayout.LabelField("Lighting", EditorStyles.boldLabel);
    myScript.lightingMethod = (VXGI.LightingMethod)EditorGUILayout.EnumPopup(new GUIContent(""), myScript.lightingMethod, showEnumValue, true);// EditorGUILayout.EnumPopup(new GUIContent(""), myScript.lightingMethod, ShowEnumValue, false);
    myScript.diffuseResolutionScale = EditorGUILayout.Slider("Resolution Scale", myScript.diffuseResolutionScale, 0.1f, 1.0f);
    myScript.throttleTracing = EditorGUILayout.Toggle("Throttle Tracing", myScript.throttleTracing);
    if (myScript.throttleTracing)
      myScript.tracingRate = EditorGUILayout.Slider("Tracing Rate", myScript.tracingRate, 1.0f, 100.0f);
    if (myScript.lightingMethod == VXGI.LightingMethod.Rays)
    {
      myScript.PerPixelGIRays = EditorGUILayout.IntSlider("Per-Pixel GI Quality", (int)Math.Sqrt((double)myScript.PerPixelGIRays), 0, 10);
      myScript.PerPixelGIRays *= myScript.PerPixelGIRays;
      if (myScript.PerPixelGIRays == 0)
        EditorGUILayout.LabelField("Per Pixel: Disabled");
      else
        EditorGUILayout.LabelField("Per Pixel: " + myScript.PerPixelGIRays.ToString() + " rays");

      myScript.PerVoxelGIRays = EditorGUILayout.IntSlider("Per-Voxel GI Quality", (int)Math.Sqrt((double)myScript.PerVoxelGIRays), 0, 5);
      myScript.PerVoxelGIRays *= myScript.PerVoxelGIRays;
      if (myScript.PerVoxelGIRays == 0)
        EditorGUILayout.LabelField("Per Voxel: Disabled");
      else
        EditorGUILayout.LabelField("Per Voxel: " + myScript.PerVoxelGIRays.ToString() + " rays");

      myScript.AnimateNoise = EditorGUILayout.Toggle("Animate Per-Pixel Noise", myScript.AnimateNoise);
      myScript.AmbientColor = EditorGUILayout.ColorField("Sky Color", myScript.AmbientColor);
      myScript.indirectDiffuseModifier = EditorGUILayout.FloatField("Indirect Diffuse Modifier", myScript.indirectDiffuseModifier);
      GUI.enabled = false;
        myScript.indirectSpecularModifier = EditorGUILayout.FloatField("Indirect Specular Modifier", myScript.indirectSpecularModifier);
      myScript.indirectSpecularModifier = 0.0f;
      GUI.enabled = true;
    }


    EditorGUILayout.LabelField("Voxelization", EditorStyles.boldLabel);
    GUI.enabled = false;
      myScript.cascadesCount = EditorGUILayout.IntSlider("Cascades", myScript.cascadesCount, VXGI.MinCascadesCount, VXGI.MaxCascadesCount);
      myScript.cascadesCount = 1;
    GUI.enabled = true;
    myScript.bound = EditorGUILayout.Slider("Bounds", myScript.bound, 0f, 256f);

    myScript.followCamera = EditorGUILayout.Toggle("Follow Camera", myScript.followCamera);
    GUI.enabled = !myScript.followCamera;
      myScript.center = EditorGUILayout.Vector3Field("Center", myScript.center);
    GUI.enabled = true;
    GUI.enabled = false;
      myScript.anisotropicVoxel = EditorGUILayout.Toggle("Anistropic Colors", myScript.anisotropicVoxel);
      myScript.anisotropicVoxel = false;
    GUI.enabled = true;
    myScript.resolution = (VXGI.Resolution)EditorGUILayout.EnumPopup("Color Resolution", myScript.resolution);
    if (myScript.RequiresBinary)
    {
      myScript.binaryResolution = (VXGI.BinaryResolution)EditorGUILayout.EnumPopup("Binary Resolution", myScript.binaryResolution);

      if ((int)myScript.binaryResolution < (int)myScript.resolution)
      {
        EditorGUILayout.HelpBox("Color resolution must <= to binary resolution.", MessageType.Error);
      }
      if ((int)myScript.binaryResolution != (int)myScript.resolution)
      {
        EditorGUILayout.HelpBox("Currently there's a slow down when the two resolutions don't match, hoping to improve at some point.", MessageType.Warning);
      }
    }
    myScript.antiAliasing = (VXGI.AntiAliasing)EditorGUILayout.EnumPopup("Anti Aliasing", myScript.antiAliasing);
  }

} // class VXGIEditor

#endif  // UNITY_EDITOR

