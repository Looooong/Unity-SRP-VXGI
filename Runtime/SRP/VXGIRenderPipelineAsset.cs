using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(fileName = "VXGIRenderPipeline.asset", menuName = "Rendering/VXGI Render Pipeline Asset", order = 320)]
public class VXGIRenderPipelineAsset : RenderPipelineAsset {
  public bool SRPBatching;
  public PerObjectData perObjectData;

  public override Material defaultMaterial => (Material)Resources.Load("VXGI/Material/Default");
  public override Material defaultParticleMaterial => (Material)Resources.Load("VXGI/Material/Default-Particle");
  public override Shader defaultShader => Shader.Find("VXGI/Standard");

  protected override RenderPipeline CreatePipeline() => new VXGIRenderPipeline(this);
}
