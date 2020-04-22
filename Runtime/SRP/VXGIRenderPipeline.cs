using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

public class VXGIRenderPipeline : RenderPipeline {
  public DrawRendererFlags drawRendererFlags {
    get { return _drawRendererFlags; }
  }
  public RendererConfiguration rendererConfiguration {
    get { return _rendererConfiguration; }
  }

  CommandBuffer _command;
  CullResults _cullResults;
  DrawRendererFlags _drawRendererFlags;
  FilterRenderersSettings _filterSettings;
  RendererConfiguration _rendererConfiguration;
  VXGIRenderer _renderer;

  public VXGIRenderPipeline(VXGIRenderPipelineAsset asset) {
    _renderer = new VXGIRenderer(this);
    _command = new CommandBuffer() { name = "VXGIRenderPipeline" };
    _filterSettings = new FilterRenderersSettings(true) { renderQueueRange = RenderQueueRange.opaque };

    _drawRendererFlags = DrawRendererFlags.None;
    if (asset.dynamicBatching) _drawRendererFlags |= DrawRendererFlags.EnableDynamicBatching;

    _rendererConfiguration = RendererConfiguration.None;

    if (asset.environmentLighting) _rendererConfiguration |= RendererConfiguration.PerObjectLightProbe;
    if (asset.environmentReflections) _rendererConfiguration |= RendererConfiguration.PerObjectReflectionProbes;

    Shader.globalRenderPipeline = "VXGI";

    GraphicsSettings.lightsUseLinearIntensity = true;
    GraphicsSettings.useScriptableRenderPipelineBatching = asset.SRPBatching;
  }

  public override void Dispose() {
    base.Dispose();

    _command.Dispose();

    Shader.globalRenderPipeline = string.Empty;
  }

  public override void Render(ScriptableRenderContext renderContext, Camera[] cameras) {
    base.Render(renderContext, cameras);
    BeginFrameRendering(cameras);

    foreach (var camera in cameras) {
      var layer = camera.GetComponent<PostProcessLayer>();

      if (layer != null && layer.isActiveAndEnabled) {
        layer.UpdateVolumeSystem(camera, _command);
        renderContext.ExecuteCommandBuffer(_command);
        _command.Clear();
      }

      BeginCameraRendering(camera);

      var vxgi = camera.GetComponent<VXGI>();

      if (vxgi != null && vxgi.isActiveAndEnabled) {
        vxgi.Render(renderContext, _renderer);
      } else {
#if UNITY_EDITOR
        bool rendered = false;

        if (camera.cameraType == UnityEngine.CameraType.SceneView) {
          ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
        }

        if (Camera.main != null) {
          vxgi = Camera.main.GetComponent<VXGI>();

          if (vxgi != null && vxgi.isActiveAndEnabled) {
            vxgi.Render(renderContext, camera, _renderer);
            rendered = true;
          }
        }

        if (!rendered) RenderFallback(renderContext, camera);
#else
        RenderFallback(renderContext, camera);
#endif
      }

      renderContext.Submit();
    }
  }

  void RenderFallback(ScriptableRenderContext renderContext, Camera camera) {
    _command.ClearRenderTarget(true, true, Color.black);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();

    ScriptableCullingParameters cullingParams;
    if (!CullResults.GetCullingParameters(camera, out cullingParams)) return;
    CullResults.Cull(ref cullingParams, renderContext, ref _cullResults);

    var drawSettings = new DrawRendererSettings(camera, new ShaderPassName("ForwardBase"));
    drawSettings.SetShaderPassName(1, new ShaderPassName("PrepassBase"));
    drawSettings.SetShaderPassName(2, new ShaderPassName("Always"));
    drawSettings.SetShaderPassName(3, new ShaderPassName("Vertex"));
    drawSettings.SetShaderPassName(4, new ShaderPassName("VertexLMRGBM"));
    drawSettings.SetShaderPassName(5, new ShaderPassName("VertexLM"));
    drawSettings.flags = _drawRendererFlags;

    renderContext.SetupCameraProperties(camera);
    renderContext.DrawRenderers(_cullResults.visibleRenderers, ref drawSettings, _filterSettings);
    renderContext.DrawSkybox(camera);
  }
}
