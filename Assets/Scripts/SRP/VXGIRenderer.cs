using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

public class VXGIRenderer : System.IDisposable {
  public enum ConeType {
    All,
    Diffuse,
    Reflectance,
    Transmittance
  }

  public enum GBufferType {
    Diffuse,
    Depth,
    Normal,
    Emission,
    Glossiness,
    Metallic
  }

  public enum MipmapSampler {
    Linear,
    Point
  }

  public enum Pass {
    ConeTracing = 0,
    DiffuseConeTracing = 1,
    GBuffer = 2,
    Mipmap = 3
  }

  public Material material {
    get {
      if (_material == null) {
        _material = new Material(Shader.Find("Hidden/VXGI"));
      }

      return _material;
    }
  }

  int _propDepth;
  int _propDiffuse;
  int _propEmission;
  int _propIrradiance;
  int _propNormal;
  int _propOther;
  CommandBuffer _command;
  CommandBuffer _commandDiffuse;
  CommandBuffer _commandReflection;
  ConeType _coneType;
  CullResults _cullResults;
  FilterRenderersSettings _filterSettings;
  GBufferType _gBufferType = GBufferType.Diffuse;
  Material _material;

  public VXGIRenderer() {
    _command = new CommandBuffer { name = "VXGIRenderer" };
    _commandDiffuse = new CommandBuffer { name = "VXGIRenderer.Diffuse" };
    _commandReflection = new CommandBuffer { name = "VXGIRenderer.Reflection" };
    _filterSettings = new FilterRenderersSettings(true) { renderQueueRange = RenderQueueRange.all };

    _propDepth = Shader.PropertyToID("Depth");
    _propDiffuse = Shader.PropertyToID("Diffuse");
    _propEmission = Shader.PropertyToID("Emission");
    _propIrradiance = Shader.PropertyToID("Irradiance");
    _propNormal = Shader.PropertyToID("Normal");
    _propOther = Shader.PropertyToID("Other");
  }

  public void Dispose() {
    _command.Dispose();
  }

  public void RenderDeferred(ScriptableRenderContext renderContext, Camera camera, VXGI vxgi) {
    ScriptableCullingParameters cullingParams;
    if (!CullResults.GetCullingParameters(camera, out cullingParams)) return;
    CullResults.Cull(ref cullingParams, renderContext, ref _cullResults);

    _command.BeginSample(_command.name);

    _command.GetTemporaryRT(_propDepth, camera.pixelWidth, camera.pixelHeight, 24, FilterMode.Point, RenderTextureFormat.Depth);
    _command.GetTemporaryRT(_propDiffuse, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf);
    _command.GetTemporaryRT(_propNormal, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGBFloat);
    _command.GetTemporaryRT(_propEmission, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf);
    _command.GetTemporaryRT(_propOther, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf);
    _command.GetTemporaryRT(_propIrradiance,
      (int)(vxgi.diffuseResolutionScale * camera.pixelWidth),
      (int)(vxgi.diffuseResolutionScale * camera.pixelHeight),
      0, FilterMode.Bilinear, RenderTextureFormat.ARGBHalf
    );

    var binding = new RenderTargetBinding(
      new RenderTargetIdentifier[] { _propDiffuse, _propNormal, _propEmission, _propOther },
      new[] { RenderBufferLoadAction.DontCare, RenderBufferLoadAction.DontCare, RenderBufferLoadAction.DontCare, RenderBufferLoadAction.DontCare },
      new[] { RenderBufferStoreAction.DontCare, RenderBufferStoreAction.DontCare, RenderBufferStoreAction.DontCare, RenderBufferStoreAction.DontCare },
      _propDepth, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare
    );

    _command.SetRenderTarget(binding);
    _command.ClearRenderTarget(true, true, Color.clear);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();

    var drawSettings = new DrawRendererSettings(camera, new ShaderPassName("Deferred"));
    drawSettings.sorting.flags = SortFlags.CommonOpaque;

    renderContext.DrawRenderers(_cullResults.visibleRenderers, ref drawSettings, _filterSettings);

    if (vxgi.pass == Pass.ConeTracing) {
      renderContext.SetupCameraProperties(camera);
      renderContext.DrawSkybox(camera);
    }

    if (_gBufferType != vxgi.gBufferType) {
      _command.DisableShaderKeyword(GetGBufferKeyword(_gBufferType));
      _command.EnableShaderKeyword(GetGBufferKeyword(vxgi.gBufferType));
      _gBufferType = vxgi.gBufferType;
    }

    if (_coneType != vxgi.coneType) {
      var keyword = GetConeKeyword(_coneType);
      if (keyword != null) _command.DisableShaderKeyword(keyword);

      keyword = GetConeKeyword(vxgi.coneType);
      if (keyword != null) _command.EnableShaderKeyword(keyword);

      _coneType = vxgi.coneType;
    }

    if (vxgi.skybox == null) {
      _command.DisableShaderKeyword("REFLECT_SKYBOX");
    } else {
      _command.EnableShaderKeyword("REFLECT_SKYBOX");
      _command.SetGlobalTexture("Skybox", vxgi.skybox);
    }

    Matrix4x4 clipToWorld = camera.cameraToWorldMatrix * GL.GetGPUProjectionMatrix(camera.projectionMatrix, false).inverse;

    _command.SetGlobalVector("CameraPosition", camera.transform.position);
    _command.SetGlobalMatrix("ClipToWorld", clipToWorld);
    _command.SetGlobalMatrix("ClipToVoxel", vxgi.worldToVoxel * clipToWorld);
    _command.SetGlobalMatrix("WorldToVoxel", vxgi.worldToVoxel);
    _command.SetGlobalMatrix("VoxelToWorld", vxgi.voxelToWorld);

    _command.EndSample(_command.name);

    renderContext.ExecuteCommandBuffer(_command);

    _command.Clear();

    if (vxgi.pass == Pass.ConeTracing) {
      _commandDiffuse.BeginSample(_commandDiffuse.name);
      _commandDiffuse.Blit(_propDiffuse, _propIrradiance, material, (int)Pass.DiffuseConeTracing);
      _commandDiffuse.EndSample(_commandDiffuse.name);

      renderContext.ExecuteCommandBuffer(_commandDiffuse);

      _commandDiffuse.Clear();
    }

    _commandReflection.BeginSample(_commandReflection.name);
    _commandReflection.Blit(_propDiffuse, BuiltinRenderTextureType.CameraTarget, material, (int)vxgi.pass);
    _commandReflection.EndSample(_commandReflection.name);

    renderContext.ExecuteCommandBuffer(_commandReflection);

    _commandReflection.Clear();

    _command.BeginSample(_command.name);

    _command.ReleaseTemporaryRT(_propDepth);
    _command.ReleaseTemporaryRT(_propDiffuse);
    _command.ReleaseTemporaryRT(_propNormal);
    _command.ReleaseTemporaryRT(_propEmission);
    _command.ReleaseTemporaryRT(_propOther);
    _command.ReleaseTemporaryRT(_propIrradiance);

    _command.EndSample(_command.name);

    renderContext.ExecuteCommandBuffer(_command);

    _command.Clear();
  }

  public void RenderMipmap(ScriptableRenderContext renderContext, Camera camera, VXGI vxgi) {
    var transform = Matrix4x4.TRS(vxgi.origin, Quaternion.identity, Vector3.one * vxgi.bound);

    _command.BeginSample(_command.name);

    if (vxgi.mipmapSampler == MipmapSampler.Point) {
      _command.EnableShaderKeyword("RADIANCE_POINT_SAMPLER");
    } else {
      _command.DisableShaderKeyword("RADIANCE_POINT_SAMPLER");
    }

    _command.SetGlobalFloat("Level", Mathf.Min(vxgi.level, vxgi.radiances.Length));
    _command.SetGlobalFloat("Step", vxgi.step);
    _command.DrawProcedural(transform, material, (int)Pass.Mipmap, MeshTopology.Quads, 24, 1);

    _command.EndSample(_command.name);

    renderContext.DrawSkybox(camera);
    renderContext.ExecuteCommandBuffer(_command);

    _command.Clear();
  }

  string GetConeKeyword(ConeType type) {
    switch (type) {
      case ConeType.Diffuse: return "TRACE_DIFFUSE";
      case ConeType.Reflectance: return "TRACE_REFLECTANCE";
      case ConeType.Transmittance: return "TRACE_TRANSMITTANCE";
      default: return null;
    }
  }

  string GetGBufferKeyword(GBufferType type) {
    switch (type) {
      case GBufferType.Diffuse: return "GBUFFER_DIFFUSE";
      case GBufferType.Depth: return "GBUFFER_DEPTH";
      case GBufferType.Normal: return "GBUFFER_NORMAL";
      case GBufferType.Emission: return "GBUFFER_EMISSION";
      case GBufferType.Glossiness: return "GBUFFER_GLOSSINESS";
      case GBufferType.Metallic: return "GBUFFER_METALLIC";
      default: return "GBUFFER_DIFFUSE";
    }
  }
}
