using UnityEngine;
using UnityEngine.Experimental.Rendering;

[CreateAssetMenu(fileName = "VXGIRenderPipeline.asset", menuName = "Rendering/VXGI Render Pipeline Asset", order = 320)]
public class VXGIRenderPipelineAsset : RenderPipelineAsset {
  public bool dynamicBatching;
  public bool SRPBatching;

  [Header("Lighting Settings")]
  public bool environmentLighting = true;
  public bool environmentReflections = true;

  public override Material GetDefaultMaterial() {
    return (Material)Resources.Load("VXGI/Material/Default");
  }

  public override Material GetDefaultParticleMaterial() {
    return (Material)Resources.Load("VXGI/Material/Default-Particle");
  }

  public override Shader GetDefaultShader() {
    return Shader.Find("VXGI/Standard");
  }

  protected override IRenderPipeline InternalCreatePipeline() {
    return new VXGIRenderPipeline(this);
  }
}
