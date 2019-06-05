using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

public class VXGIRenderer : System.IDisposable {
  public enum MipmapSampler {
    Linear,
    Point
  }

  public enum Pass {
    ConeTracing = 0,
    DiffuseConeTracing = 1,
    Mipmap = 2
  }

  public DrawRendererFlags drawRendererFlags {
    get { return _renderPipeline.drawRendererFlags; }
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
  int _propSpecular;
  CommandBuffer _command;
  CommandBuffer _commandDiffuse;
  CommandBuffer _commandReflection;
  CullResults _cullResults;
  FilterRenderersSettings _filterSettings;
  Material _material;
  RenderTargetBinding _gBufferBinding;
  VXGIRenderPipeline _renderPipeline;

  public VXGIRenderer(VXGIRenderPipeline renderPipeline) {
    _command = new CommandBuffer { name = "VXGIRenderer" };
    _commandDiffuse = new CommandBuffer { name = "VXGIRenderer.Diffuse" };
    _commandReflection = new CommandBuffer { name = "VXGIRenderer.Reflection" };
    _filterSettings = new FilterRenderersSettings(true) { renderQueueRange = RenderQueueRange.all };
    _renderPipeline = renderPipeline;

    _propDepth = Shader.PropertyToID("Depth");
    _propDiffuse = Shader.PropertyToID("Diffuse");
    _propEmission = Shader.PropertyToID("Emission");
    _propIrradiance = Shader.PropertyToID("Irradiance");
    _propNormal = Shader.PropertyToID("Normal");
    _propSpecular = Shader.PropertyToID("Specular");

    _gBufferBinding = new RenderTargetBinding(
      new RenderTargetIdentifier[] { _propDiffuse, _propSpecular, _propNormal, _propEmission },
      new[] { RenderBufferLoadAction.DontCare, RenderBufferLoadAction.DontCare, RenderBufferLoadAction.DontCare, RenderBufferLoadAction.DontCare },
      new[] { RenderBufferStoreAction.DontCare, RenderBufferStoreAction.DontCare, RenderBufferStoreAction.DontCare, RenderBufferStoreAction.DontCare },
      _propDepth, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare
    );
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
    _command.GetTemporaryRT(_propDiffuse, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGB32);
    _command.GetTemporaryRT(_propSpecular, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGB32);
    _command.GetTemporaryRT(_propNormal, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGB2101010);
    _command.GetTemporaryRT(_propEmission, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf);
    _command.GetTemporaryRT(_propIrradiance,
      (int)(vxgi.diffuseResolutionScale * camera.pixelWidth),
      (int)(vxgi.diffuseResolutionScale * camera.pixelHeight),
      0, FilterMode.Bilinear, RenderTextureFormat.ARGBHalf
    );

    if (vxgi.pass == Pass.ConeTracing) renderContext.SetupCameraProperties(camera);

    _command.SetRenderTarget(_gBufferBinding);
    _command.ClearRenderTarget(true, true, Color.clear);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();

    var drawSettings = new DrawRendererSettings(camera, new ShaderPassName("Deferred"));
    drawSettings.flags = _renderPipeline.drawRendererFlags;
    drawSettings.rendererConfiguration |= RendererConfiguration.PerObjectReflectionProbes;
    drawSettings.sorting.flags = SortFlags.CommonOpaque;

    renderContext.DrawRenderers(_cullResults.visibleRenderers, ref drawSettings, _filterSettings);

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

    if (vxgi.pass == Pass.ConeTracing) renderContext.DrawSkybox(camera);

    _command.BeginSample(_command.name);

    _command.ReleaseTemporaryRT(_propDepth);
    _command.ReleaseTemporaryRT(_propDiffuse);
    _command.ReleaseTemporaryRT(_propSpecular);
    _command.ReleaseTemporaryRT(_propNormal);
    _command.ReleaseTemporaryRT(_propEmission);
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
}
