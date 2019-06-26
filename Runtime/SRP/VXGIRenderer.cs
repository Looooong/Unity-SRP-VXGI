using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

public class VXGIRenderer : System.IDisposable {
  public enum MipmapSampler {
    Linear,
    Point
  }

  public DrawRendererFlags drawRendererFlags {
    get { return _renderPipeline.drawRendererFlags; }
  }
  public RendererConfiguration rendererConfiguration {
    get { return _renderPipeline.rendererConfiguration; }
  }

  int _cameraDepthTextureID;
  int _cameraDepthNormalsTextureID;
  int _cameraGBufferTexture0ID;
  int _cameraGBufferTexture1ID;
  int _cameraGBufferTexture2ID;
  int _cameraGBufferTexture3ID;
  int _dummyID;
  int _frameBufferID;
  float[] _renderScale;
  CommandBuffer _command;
  CullResults _cullResults;
  FilterRenderersSettings _filterSettings;
  LightingShader[] _lightingPasses;
  PostProcessRenderContext _postProcessRenderContext;
  RenderTargetBinding _gBufferBinding;
  VXGIRenderPipeline _renderPipeline;

  public VXGIRenderer(VXGIRenderPipeline renderPipeline) {
    _command = new CommandBuffer { name = "VXGIRenderer" };
    _filterSettings = new FilterRenderersSettings(true) { renderQueueRange = RenderQueueRange.all };
    _renderPipeline = renderPipeline;

    _cameraDepthTextureID = Shader.PropertyToID("_CameraDepthTexture");
    _cameraDepthNormalsTextureID = Shader.PropertyToID("_CameraDepthNormalsTexture");
    _cameraGBufferTexture0ID = Shader.PropertyToID("_CameraGBufferTexture0");
    _cameraGBufferTexture1ID = Shader.PropertyToID("_CameraGBufferTexture1");
    _cameraGBufferTexture2ID = Shader.PropertyToID("_CameraGBufferTexture2");
    _cameraGBufferTexture3ID = Shader.PropertyToID("_CameraGBufferTexture3");
    _dummyID = Shader.PropertyToID("Dummy");
    _frameBufferID = Shader.PropertyToID("FrameBuffer");

    _gBufferBinding = new RenderTargetBinding(
      new RenderTargetIdentifier[] { _cameraGBufferTexture0ID, _cameraGBufferTexture1ID, _cameraGBufferTexture2ID, _cameraGBufferTexture3ID },
      new[] { RenderBufferLoadAction.DontCare, RenderBufferLoadAction.DontCare, RenderBufferLoadAction.DontCare, RenderBufferLoadAction.DontCare },
      new[] { RenderBufferStoreAction.DontCare, RenderBufferStoreAction.DontCare, RenderBufferStoreAction.DontCare, RenderBufferStoreAction.DontCare },
      _cameraDepthTextureID, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare
    );

    _renderScale = new float[] { 1f, 1f, 1f, 1f };

    _lightingPasses = new LightingShader[] {
      new LightingShader(LightingShader.Pass.Emission),
      new LightingShader(LightingShader.Pass.DirectDiffuseSpecular),
      new LightingShader(LightingShader.Pass.IndirectDiffuse),
      new LightingShader(LightingShader.Pass.IndirectSpecular)
    };

    _postProcessRenderContext = new PostProcessRenderContext();
  }

  public void Dispose() {
    _command.Dispose();

    foreach (var pass in _lightingPasses) pass.Dispose();
  }

  public void RenderDeferred(ScriptableRenderContext renderContext, Camera camera, VXGI vxgi) {
    ScriptableCullingParameters cullingParams;
    if (!CullResults.GetCullingParameters(camera, out cullingParams)) return;
    CullResults.Cull(ref cullingParams, renderContext, ref _cullResults);

    renderContext.SetupCameraProperties(camera);

    int width = camera.pixelWidth;
    int height = camera.pixelHeight;

    _command.GetTemporaryRT(_cameraDepthTextureID, width, height, 16, FilterMode.Point, RenderTextureFormat.Depth, RenderTextureReadWrite.Linear);
    _command.GetTemporaryRT(_cameraGBufferTexture0ID, width, height, 0, FilterMode.Point, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
    _command.GetTemporaryRT(_cameraGBufferTexture1ID, width, height, 0, FilterMode.Point, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
    _command.GetTemporaryRT(_cameraGBufferTexture2ID, width, height, 0, FilterMode.Point, RenderTextureFormat.ARGB2101010, RenderTextureReadWrite.Linear);
    _command.GetTemporaryRT(_cameraGBufferTexture3ID, width, height, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
    _command.GetTemporaryRT(_frameBufferID, width, height, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
    _command.SetRenderTarget(_gBufferBinding);
    _command.ClearRenderTarget(true, true, Color.clear);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();

    var drawSettings = new DrawRendererSettings(camera, new ShaderPassName("Deferred"));
    drawSettings.flags = _renderPipeline.drawRendererFlags;
    drawSettings.rendererConfiguration = _renderPipeline.rendererConfiguration;
    drawSettings.sorting.flags = SortFlags.CommonOpaque;

    renderContext.DrawRenderers(_cullResults.visibleRenderers, ref drawSettings, _filterSettings);

    _command.GetTemporaryRT(_dummyID, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
    _command.Blit(_cameraDepthTextureID, BuiltinRenderTextureType.CameraTarget, UtilityShader.material, (int)UtilityShader.Pass.DepthCopy);
    _command.Blit(BuiltinRenderTextureType.CameraTarget, _dummyID);
    _command.Blit(_dummyID, _frameBufferID, UtilityShader.material, (int)UtilityShader.Pass.GrabCopy);
    _command.ReleaseTemporaryRT(_dummyID);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();

    Matrix4x4 clipToWorld = camera.cameraToWorldMatrix * GL.GetGPUProjectionMatrix(camera.projectionMatrix, false).inverse;

    _command.SetGlobalMatrix("ClipToWorld", clipToWorld);
    _command.SetGlobalMatrix("ClipToVoxel", vxgi.worldToVoxel * clipToWorld);
    _command.SetGlobalMatrix("WorldToVoxel", vxgi.worldToVoxel);
    _command.SetGlobalMatrix("VoxelToWorld", vxgi.voxelToWorld);

    bool depthNormalsNeeded = (camera.depthTextureMode & DepthTextureMode.DepthNormals) != DepthTextureMode.None;

    if (depthNormalsNeeded) {
      _command.GetTemporaryRT(_cameraDepthNormalsTextureID, width, height, 0, FilterMode.Point, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
      _command.Blit(_cameraDepthTextureID, _cameraDepthNormalsTextureID, UtilityShader.material, (int)UtilityShader.Pass.EncodeDepthNormal);
    }

    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();

    _renderScale[2] = vxgi.diffuseResolutionScale;

    for (int i = 0; i < _lightingPasses.Length; i++) {
      _lightingPasses[i].Execute(renderContext, camera, _frameBufferID, _renderScale[i]);
    }

    RenderPostProcessing(renderContext, camera);

    _command.Blit(_frameBufferID, BuiltinRenderTextureType.CameraTarget);

    RenderPostProcessingDebug(renderContext, camera);

    if (depthNormalsNeeded) {
      _command.ReleaseTemporaryRT(_cameraDepthNormalsTextureID);
    }

    _command.ReleaseTemporaryRT(_cameraDepthTextureID);
    _command.ReleaseTemporaryRT(_cameraGBufferTexture0ID);
    _command.ReleaseTemporaryRT(_cameraGBufferTexture1ID);
    _command.ReleaseTemporaryRT(_cameraGBufferTexture2ID);
    _command.ReleaseTemporaryRT(_cameraGBufferTexture3ID);
    _command.ReleaseTemporaryRT(_frameBufferID);
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

  public void RenderPostProcessing(ScriptableRenderContext renderContext, Camera camera) {
    var layer = camera.GetComponent<PostProcessLayer>();

    if (layer == null || !layer.isActiveAndEnabled) return;

    _command.GetTemporaryRT(_dummyID, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);

    _postProcessRenderContext.Reset();
    _postProcessRenderContext.camera = camera;
    _postProcessRenderContext.command = _command;
    _postProcessRenderContext.destination = _frameBufferID;
    _postProcessRenderContext.source = _dummyID;
    _postProcessRenderContext.sourceFormat = RenderTextureFormat.ARGBHalf;

    if (layer.HasOpaqueOnlyEffects(_postProcessRenderContext)) {
      _command.Blit(_frameBufferID, _dummyID);
      layer.RenderOpaqueOnly(_postProcessRenderContext);
    }

    _command.Blit(_frameBufferID, _dummyID);
    layer.Render(_postProcessRenderContext);

    _command.ReleaseTemporaryRT(_dummyID);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();
  }

  public void RenderPostProcessingDebug(ScriptableRenderContext renderContext, Camera camera) {
    var postProcessDebug = camera.GetComponent<PostProcessDebug>();

    if (postProcessDebug == null) return;

    postProcessDebug.SendMessage("OnPostRender");

    foreach (var command in camera.GetCommandBuffers(CameraEvent.AfterImageEffects)) {
      renderContext.ExecuteCommandBuffer(command);
    }
  }
}
