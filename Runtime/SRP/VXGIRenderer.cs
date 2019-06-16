using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

public class VXGIRenderer : System.IDisposable {
  public enum MipmapSampler {
    Linear,
    Point
  }

  public DrawRendererFlags drawRendererFlags {
    get { return _renderPipeline.drawRendererFlags; }
  }

  int _propDepth;
  int _propDiffuse;
  int _propEmission;
  int _propNormal;
  int _propSpecular;
  float[] _renderScale;
  CommandBuffer _command;
  CullResults _cullResults;
  FilterRenderersSettings _filterSettings;
  LightingShader[] _lightingPasses;
  RenderTargetBinding _gBufferBinding;
  VXGIRenderPipeline _renderPipeline;

  public VXGIRenderer(VXGIRenderPipeline renderPipeline) {
    _command = new CommandBuffer { name = "VXGIRenderer" };
    _filterSettings = new FilterRenderersSettings(true) { renderQueueRange = RenderQueueRange.all };
    _renderPipeline = renderPipeline;

    _propDepth = Shader.PropertyToID("Depth");
    _propDiffuse = Shader.PropertyToID("Diffuse");
    _propEmission = Shader.PropertyToID("Emission");
    _propNormal = Shader.PropertyToID("Normal");
    _propSpecular = Shader.PropertyToID("Specular");

    _gBufferBinding = new RenderTargetBinding(
      new RenderTargetIdentifier[] { _propDiffuse, _propSpecular, _propNormal, _propEmission },
      new[] { RenderBufferLoadAction.DontCare, RenderBufferLoadAction.DontCare, RenderBufferLoadAction.DontCare, RenderBufferLoadAction.DontCare },
      new[] { RenderBufferStoreAction.DontCare, RenderBufferStoreAction.DontCare, RenderBufferStoreAction.DontCare, RenderBufferStoreAction.DontCare },
      _propDepth, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare
    );

    _renderScale = new float[] { 1f, 1f, 1f, 1f };

    _lightingPasses = new LightingShader[] {
      new LightingShader(LightingShader.Pass.Emission),
      new LightingShader(LightingShader.Pass.DirectDiffuseSpecular),
      new LightingShader(LightingShader.Pass.IndirectDiffuse),
      new LightingShader(LightingShader.Pass.IndirectSpecular)
    };
  }

  public void Dispose() {
    _command.Dispose();

    foreach (var pass in _lightingPasses) pass.Dispose();
  }

  public void RenderDeferred(ScriptableRenderContext renderContext, Camera camera, VXGI vxgi) {
    ScriptableCullingParameters cullingParams;
    if (!CullResults.GetCullingParameters(camera, out cullingParams)) return;
    CullResults.Cull(ref cullingParams, renderContext, ref _cullResults);

    _command.BeginSample(_command.name);

    _command.GetTemporaryRT(_propDepth, camera.pixelWidth, camera.pixelHeight, 16, FilterMode.Point, RenderTextureFormat.Depth);
    _command.GetTemporaryRT(_propDiffuse, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGB32);
    _command.GetTemporaryRT(_propSpecular, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGB32);
    _command.GetTemporaryRT(_propNormal, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGB2101010);
    _command.GetTemporaryRT(_propEmission, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf);

    renderContext.SetupCameraProperties(camera);

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

    _command.SetGlobalMatrix("ClipToWorld", clipToWorld);
    _command.SetGlobalMatrix("ClipToVoxel", vxgi.worldToVoxel * clipToWorld);
    _command.SetGlobalMatrix("WorldToVoxel", vxgi.worldToVoxel);
    _command.SetGlobalMatrix("VoxelToWorld", vxgi.voxelToWorld);
    _command.Blit(_propDepth, BuiltinRenderTextureType.CameraTarget, UtilityShader.material, (int)UtilityShader.Pass.DepthCopy);
    _command.EndSample(_command.name);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();

    _renderScale[2] = vxgi.diffuseResolutionScale;

    for (int i = 0; i < _lightingPasses.Length; i++) {
      _lightingPasses[i].Execute(renderContext, camera, _renderScale[i]);
    }

    _command.ReleaseTemporaryRT(_propDepth);
    _command.ReleaseTemporaryRT(_propDiffuse);
    _command.ReleaseTemporaryRT(_propSpecular);
    _command.ReleaseTemporaryRT(_propNormal);
    _command.ReleaseTemporaryRT(_propEmission);

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

    _command.SetGlobalFloat("MipmapLevel", Mathf.Min(vxgi.level, vxgi.radiances.Length));
    _command.SetGlobalFloat("TracingStep", vxgi.step);
    _command.DrawProcedural(transform, VisualizationShader.material, (int)VisualizationShader.Pass.Mipmap, MeshTopology.Quads, 24, 1);

    _command.EndSample(_command.name);

    renderContext.ExecuteCommandBuffer(_command);

    _command.Clear();
  }
}
