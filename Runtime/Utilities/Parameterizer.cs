using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

public class Parameterizer : System.IDisposable {
  int _kernelParameterize;
  int _propNumThreads = Shader.PropertyToID("NumThreads");
  int _propArguments = Shader.PropertyToID("Arguments");
  ComputeBuffer _arguments = new ComputeBuffer(3, sizeof(int), ComputeBufferType.IndirectArguments);
  ComputeShader _compute = (ComputeShader)Resources.Load("Compute/Parameterizer");

  public Parameterizer() {
    _kernelParameterize = _compute.FindKernel("CSParameterize");
    _arguments.SetData(new int[] { 1, 1, 1 });
  }

  public void Dispose() {
    _arguments.Dispose();
  }

  public void Parameterize(CommandBuffer command, ComputeBuffer arguments, NumThreads numThreads) {
    command.SetComputeIntParams(_compute, _propNumThreads, numThreads);
    command.SetComputeBufferParam(_compute, _kernelParameterize, _propArguments, arguments);
    command.DispatchCompute(_compute, _kernelParameterize, _arguments, 0);
  }
}
