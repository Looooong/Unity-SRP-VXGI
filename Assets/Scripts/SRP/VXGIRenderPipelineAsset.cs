using UnityEngine;
using UnityEngine.Experimental.Rendering;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
#endif

[CreateAssetMenu(fileName = "VXGIRenderPipeline.asset", menuName = "Rendering/VXGI Render Pipeline Asset", order = 320)]
[ExecuteInEditMode]
public class VXGIRenderPipelineAsset : RenderPipelineAsset {
  protected override IRenderPipeline InternalCreatePipeline() {
    return new VXGIRenderPipeline();
  }
}
