using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

public class VXGIRenderer : System.IDisposable {
  public DrawRendererFlags drawRendererFlags {
    get { return _renderPipeline.drawRendererFlags; }
  }
  public RendererConfiguration rendererConfiguration {
    get { return _renderPipeline.rendererConfiguration; }
  }

  const string _sampleCameraEvent = "CameraEvent.";
  const string _samplePostProcessRender = "PostProcess.Render";
  const string _samplePostProcessRenderOpaqueOnly = "PostProcess.Render.OpaqueOnly";
  const string _sampleRenderLighting = "Render.Lighting";

  float[] _renderScale;
  CommandBuffer _command;
  CommandBuffer _eventCommand;
  CullResults _cullResults;
  FilterRenderersSettings _filterSettings;
  LightingShader[] _lightingPasses;
  PostProcessRenderContext _postProcessRenderContext;
  RenderTargetBinding _gBufferBinding;
  VXGIRenderPipeline _renderPipeline;

  public VXGIRenderer(VXGIRenderPipeline renderPipeline) {
    _command = new CommandBuffer { name = "VXGI.Renderer" };
    _eventCommand = new CommandBuffer();
    _filterSettings = new FilterRenderersSettings(true);
    _renderPipeline = renderPipeline;

    _gBufferBinding = new RenderTargetBinding(
      new RenderTargetIdentifier[] { ShaderIDs._CameraGBufferTexture0, ShaderIDs._CameraGBufferTexture1, ShaderIDs._CameraGBufferTexture2, ShaderIDs._CameraGBufferTexture3 },
      new[] { RenderBufferLoadAction.DontCare, RenderBufferLoadAction.DontCare, RenderBufferLoadAction.DontCare, RenderBufferLoadAction.DontCare },
      new[] { RenderBufferStoreAction.DontCare, RenderBufferStoreAction.DontCare, RenderBufferStoreAction.DontCare, RenderBufferStoreAction.DontCare },
      ShaderIDs._CameraDepthTexture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare
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
    _eventCommand.Dispose();
  }

  public void RenderDeferred(ScriptableRenderContext renderContext, Camera camera, VXGI vxgi) {
    ScriptableCullingParameters cullingParams;
    if (!CullResults.GetCullingParameters(camera, out cullingParams)) return;
    CullResults.Cull(ref cullingParams, renderContext, ref _cullResults);

    renderContext.SetupCameraProperties(camera);

    int width = camera.pixelWidth;
    int height = camera.pixelHeight;

    _command.BeginSample(_command.name);

    if (camera.cameraType != CameraType.SceneView) {
      _command.EnableShaderKeyword("PROJECTION_PARAMS_X");
    } else {
      _command.DisableShaderKeyword("PROJECTION_PARAMS_X");
    }

    _command.GetTemporaryRT(ShaderIDs._CameraDepthTexture, width, height, 24, FilterMode.Point, RenderTextureFormat.Depth, RenderTextureReadWrite.Linear);
    _command.GetTemporaryRT(ShaderIDs._CameraGBufferTexture0, width, height, 0, FilterMode.Point, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
    _command.GetTemporaryRT(ShaderIDs._CameraGBufferTexture1, width, height, 0, FilterMode.Point, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
    _command.GetTemporaryRT(ShaderIDs._CameraGBufferTexture2, width, height, 0, FilterMode.Point, RenderTextureFormat.ARGB2101010, RenderTextureReadWrite.Linear);
    _command.GetTemporaryRT(ShaderIDs._CameraGBufferTexture3, width, height, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
    _command.GetTemporaryRT(ShaderIDs.FrameBuffer, width, height, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
    _command.SetRenderTarget(_gBufferBinding);
    _command.ClearRenderTarget(true, true, Color.clear);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();

    TriggerCameraEvent(renderContext, camera, CameraEvent.BeforeGBuffer, vxgi);
    RenderGBuffers(renderContext, camera);
    TriggerCameraEvent(renderContext, camera, CameraEvent.AfterGBuffer, vxgi);

    CopyCameraTargetToFrameBuffer(renderContext, camera);

    bool depthNormalsNeeded = (camera.depthTextureMode & DepthTextureMode.DepthNormals) != DepthTextureMode.None;

    if (depthNormalsNeeded) {
      TriggerCameraEvent(renderContext, camera, CameraEvent.BeforeDepthNormalsTexture, vxgi);
      RenderCameraDepthNormalsTexture(renderContext, camera);
      TriggerCameraEvent(renderContext, camera, CameraEvent.AfterDepthNormalsTexture, vxgi);
    }

    TriggerCameraEvent(renderContext, camera, CameraEvent.BeforeLighting, vxgi);
    RenderLighting(renderContext, camera, vxgi);
    TriggerCameraEvent(renderContext, camera, CameraEvent.AfterLighting, vxgi);

    if (camera.clearFlags == CameraClearFlags.Skybox) {
      TriggerCameraEvent(renderContext, camera, CameraEvent.BeforeSkybox, vxgi);
      RenderSkyBox(renderContext, camera);
      TriggerCameraEvent(renderContext, camera, CameraEvent.AfterSkybox, vxgi);
    }

    UpdatePostProcessingLayer(renderContext, camera, vxgi);

    TriggerCameraEvent(renderContext, camera, CameraEvent.BeforeImageEffectsOpaque, vxgi);
    RenderPostProcessingOpaqueOnly(renderContext, camera);
    TriggerCameraEvent(renderContext, camera, CameraEvent.AfterImageEffectsOpaque, vxgi);

    TriggerCameraEvent(renderContext, camera, CameraEvent.BeforeForwardAlpha, vxgi);
    RenderTransparent(renderContext, camera);
    TriggerCameraEvent(renderContext, camera, CameraEvent.AfterForwardAlpha, vxgi);

    TriggerCameraEvent(renderContext, camera, CameraEvent.BeforeImageEffects, vxgi);
    RenderPostProcessing(renderContext, camera);
    _command.Blit(ShaderIDs.FrameBuffer, BuiltinRenderTextureType.CameraTarget);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();
    TriggerCameraEvent(renderContext, camera, CameraEvent.AfterImageEffects, vxgi);

    TriggerCameraEvent(renderContext, camera, CameraEvent.AfterEverything, vxgi);

    if (depthNormalsNeeded) {
      _command.ReleaseTemporaryRT(ShaderIDs._CameraDepthNormalsTexture);
    }

    _command.ReleaseTemporaryRT(ShaderIDs._CameraDepthTexture);
    _command.ReleaseTemporaryRT(ShaderIDs._CameraGBufferTexture0);
    _command.ReleaseTemporaryRT(ShaderIDs._CameraGBufferTexture1);
    _command.ReleaseTemporaryRT(ShaderIDs._CameraGBufferTexture2);
    _command.ReleaseTemporaryRT(ShaderIDs._CameraGBufferTexture3);
    _command.ReleaseTemporaryRT(ShaderIDs.FrameBuffer);
    _command.EndSample(_command.name);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();
  }

  void CopyCameraTargetToFrameBuffer(ScriptableRenderContext renderContext, Camera camera) {
    _command.GetTemporaryRT(ShaderIDs.Dummy, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
    _command.Blit(ShaderIDs._CameraDepthTexture, BuiltinRenderTextureType.CameraTarget, UtilityShader.material, (int)UtilityShader.Pass.DepthCopy);
    _command.Blit(BuiltinRenderTextureType.CameraTarget, ShaderIDs.Dummy);
    _command.Blit(ShaderIDs.Dummy, ShaderIDs.FrameBuffer, UtilityShader.material, (int)UtilityShader.Pass.GrabCopy);
    _command.ReleaseTemporaryRT(ShaderIDs.Dummy);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();
  }

  void RenderCameraDepthNormalsTexture(ScriptableRenderContext renderContext, Camera camera) {
    _command.GetTemporaryRT(ShaderIDs._CameraDepthNormalsTexture, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
    _command.Blit(ShaderIDs._CameraDepthTexture, ShaderIDs._CameraDepthNormalsTexture, UtilityShader.material, (int)UtilityShader.Pass.EncodeDepthNormal);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();
  }

  void RenderGBuffers(ScriptableRenderContext renderContext, Camera camera) {
    var drawSettings = new DrawRendererSettings(camera, new ShaderPassName("Deferred"));
    drawSettings.flags = _renderPipeline.drawRendererFlags;
    drawSettings.rendererConfiguration = _renderPipeline.rendererConfiguration;
    drawSettings.sorting.flags = SortFlags.CommonOpaque;

    _filterSettings.renderQueueRange = RenderQueueRange.opaque;

    renderContext.DrawRenderers(_cullResults.visibleRenderers, ref drawSettings, _filterSettings);
  }

  void RenderLighting(ScriptableRenderContext renderContext, Camera camera, VXGI vxgi) {
    Matrix4x4 clipToWorld = camera.cameraToWorldMatrix * GL.GetGPUProjectionMatrix(camera.projectionMatrix, false).inverse;

    _renderScale[2] = vxgi.diffuseResolutionScale;

    _command.BeginSample(_sampleRenderLighting);
    _command.SetGlobalMatrix(ShaderIDs.ClipToVoxel, vxgi.worldToVoxel * clipToWorld);
    _command.SetGlobalMatrix(ShaderIDs.ClipToWorld, clipToWorld);
    _command.SetGlobalMatrix(ShaderIDs.VoxelToWorld, vxgi.voxelToWorld);
    _command.SetGlobalMatrix(ShaderIDs.WorldToVoxel, vxgi.worldToVoxel);

    for (int i = 0; i < _lightingPasses.Length; i++) {
      _lightingPasses[i].Execute(_command, camera, ShaderIDs.FrameBuffer, _renderScale[i]);
    }

    _command.EndSample(_sampleRenderLighting);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();
  }

  void RenderPostProcessing(ScriptableRenderContext renderContext, Camera camera) {
    var layer = camera.GetComponent<PostProcessLayer>();

    if (layer == null || !layer.isActiveAndEnabled) return;

    _postProcessRenderContext.Reset();
    _postProcessRenderContext.camera = camera;
    _postProcessRenderContext.command = _command;
    _postProcessRenderContext.destination = ShaderIDs.FrameBuffer;
    _postProcessRenderContext.source = ShaderIDs.Dummy;
    _postProcessRenderContext.sourceFormat = RenderTextureFormat.ARGBHalf;

    _command.BeginSample(_samplePostProcessRender);
    _command.GetTemporaryRT(ShaderIDs.Dummy, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
    _command.Blit(ShaderIDs.FrameBuffer, ShaderIDs.Dummy);
    layer.Render(_postProcessRenderContext);
    _command.ReleaseTemporaryRT(ShaderIDs.Dummy);
    _command.EndSample(_samplePostProcessRender);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();
  }

  void RenderPostProcessingOpaqueOnly(ScriptableRenderContext renderContext, Camera camera) {
    var layer = camera.GetComponent<PostProcessLayer>();

    if (layer == null || !layer.isActiveAndEnabled) return;

    _postProcessRenderContext.Reset();
    _postProcessRenderContext.camera = camera;
    _postProcessRenderContext.command = _command;
    _postProcessRenderContext.destination = ShaderIDs.FrameBuffer;
    _postProcessRenderContext.source = ShaderIDs.Dummy;
    _postProcessRenderContext.sourceFormat = RenderTextureFormat.ARGBHalf;

    if (!layer.HasOpaqueOnlyEffects(_postProcessRenderContext)) return;

    _command.BeginSample(_samplePostProcessRenderOpaqueOnly);
    _command.GetTemporaryRT(ShaderIDs.Dummy, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
    _command.Blit(ShaderIDs.FrameBuffer, ShaderIDs.Dummy);
    layer.RenderOpaqueOnly(_postProcessRenderContext);
    _command.ReleaseTemporaryRT(ShaderIDs.Dummy);
    _command.EndSample(_samplePostProcessRenderOpaqueOnly);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();
  }

  void RenderSkyBox(ScriptableRenderContext renderContext, Camera camera) {
    _command.SetRenderTarget(ShaderIDs.FrameBuffer, (RenderTargetIdentifier)ShaderIDs._CameraDepthTexture);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();
    renderContext.DrawSkybox(camera);
  }

  void RenderTransparent(ScriptableRenderContext renderContext, Camera camera) {
    var drawSettings = new DrawRendererSettings(camera, new ShaderPassName("ForwardBase"));
    drawSettings.flags = _renderPipeline.drawRendererFlags;
    drawSettings.rendererConfiguration = _renderPipeline.rendererConfiguration;
    drawSettings.sorting.flags = SortFlags.CommonTransparent;

    _filterSettings.renderQueueRange = RenderQueueRange.transparent;

    _command.SetRenderTarget(ShaderIDs.FrameBuffer);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();

    renderContext.DrawRenderers(_cullResults.visibleRenderers, ref drawSettings, _filterSettings);
  }

  void TriggerCameraEvent(ScriptableRenderContext renderContext, Camera camera, CameraEvent cameraEvent, VXGI vxgi) {
    var commands = camera.GetCommandBuffers(cameraEvent);

    if (commands.Length == 0) return;

    _eventCommand.name = _sampleCameraEvent + cameraEvent.ToString();
    _eventCommand.BeginSample(_eventCommand.name);
    _eventCommand.SetRenderTarget(ShaderIDs.FrameBuffer);
    renderContext.ExecuteCommandBuffer(_eventCommand);
    _eventCommand.Clear();

    foreach (var command in commands) renderContext.ExecuteCommandBuffer(command);

    _eventCommand.EndSample(_eventCommand.name);
    renderContext.ExecuteCommandBuffer(_eventCommand);
    _eventCommand.Clear();
  }

  void UpdatePostProcessingLayer(ScriptableRenderContext renderContext, Camera camera, VXGI vxgi) {
    var layer = vxgi.GetComponent<PostProcessLayer>();

    if (layer == null || !layer.isActiveAndEnabled) return;

    layer.UpdateVolumeSystem(camera, _command);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();
  }
}
