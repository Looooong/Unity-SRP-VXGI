using UnityEngine;
using UnityEngine.Experimental.Rendering;

[CreateAssetMenu(fileName = "VXGIRenderPipeline.asset", menuName = "Rendering/VXGI Render Pipeline Asset", order = 320)]
[ExecuteInEditMode]
public class VXGIRenderPipelineAsset : RenderPipelineAsset {
  public bool useBatching = false;

  protected override IRenderPipeline InternalCreatePipeline() {
    return new VXGIRenderPipeline(this);
  }
}
