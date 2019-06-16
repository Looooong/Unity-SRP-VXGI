using UnityEngine;
using UnityEngine.Experimental.Rendering;

[CreateAssetMenu(fileName = "VXGIRenderPipeline.asset", menuName = "Rendering/VXGI Render Pipeline Asset", order = 320)]
public class VXGIRenderPipelineAsset : RenderPipelineAsset {
  public bool dynamicBatching;
  public bool SRPBatching;

  protected override IRenderPipeline InternalCreatePipeline() {
    return new VXGIRenderPipeline(this);
  }
}
