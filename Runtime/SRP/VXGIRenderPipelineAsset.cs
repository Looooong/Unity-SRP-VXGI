using UnityEngine;
using UnityEngine.Experimental.Rendering;

[CreateAssetMenu(fileName = "VXGIRenderPipeline.asset", menuName = "Rendering/VXGI Render Pipeline Asset", order = 320)]
public class VXGIRenderPipelineAsset : RenderPipelineAsset {
  public bool dynamicBatching;
  public bool SRPBatching;

  public override Material GetDefaultMaterial() {
    return (Material)Resources.Load("VXGI/Material/Default");
  }

  public override Shader GetDefaultShader() {
    return Shader.Find("Voxel-based Shader/Basic");
  }

  protected override IRenderPipeline InternalCreatePipeline() {
    return new VXGIRenderPipeline(this);
  }
}
