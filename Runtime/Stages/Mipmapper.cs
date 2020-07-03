using UnityEngine;
using UnityEngine.Rendering;

public class Mipmapper {
  public enum Mode { Box = 0, Gaussian3x3x3 = 1, Gaussian4x4x4 = 2 }

  public ComputeShader compute {
    get {
      if (_compute == null) _compute = (ComputeShader)Resources.Load("VXGI/Compute/Mipmapper");

      return _compute;
    }
  }

  const string _sampleFilter = "Filter.";
  const string _sampleShift = "Shift";

  int _kernelFilter;
  int _kernelShift;
  CommandBuffer _command;
  ComputeShader _compute;
  NumThreads _threadsFilter;
  NumThreads _threadsShift;
  VXGI _vxgi;

  public Mipmapper(VXGI vxgi) {
    _vxgi = vxgi;

    _command = new CommandBuffer { name = "VXGI.Mipmapper" };

    InitializeKernel();
  }

  public void Dispose() {
    _command.Dispose();
  }

  public void Filter(ScriptableRenderContext renderContext) {
    UpdateKernel();

    var radiances = _vxgi.radiances;

    for (var i = 1; i < radiances.Length; i++) {
      int resolution = radiances[i].volumeDepth;

      _command.BeginSample(_sampleFilter + _vxgi.mipmapFilterMode.ToString() + '.' + resolution.ToString("D3"));
      _command.SetComputeIntParam(compute, ShaderIDs.Resolution, resolution);
      _command.SetComputeTextureParam(compute, _kernelFilter, ShaderIDs.Source, radiances[i - 1]);
      _command.SetComputeTextureParam(compute, _kernelFilter, ShaderIDs.Target, radiances[i]);
      _command.DispatchCompute(compute, _kernelFilter,
         Mathf.CeilToInt((float)resolution /_threadsFilter.x),
         Mathf.CeilToInt((float)resolution /_threadsFilter.y),
         Mathf.CeilToInt((float)resolution /_threadsFilter.z)
      );
      _command.EndSample(_sampleFilter + _vxgi.mipmapFilterMode.ToString() + '.' + resolution.ToString("D3"));
    }

    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();
  }

  public void Shift(ScriptableRenderContext renderContext, Vector3Int displacement) {
    UpdateKernel();

    _command.BeginSample(_sampleShift);
    _command.SetComputeIntParam(compute, ShaderIDs.Resolution, (int)_vxgi.resolution);
    _command.SetComputeIntParams(compute, ShaderIDs.Displacement, new[] { displacement.x, displacement.y, displacement.z });
    _command.SetComputeTextureParam(compute, _kernelShift, ShaderIDs.Target, _vxgi.radiances[0]);
    _command.DispatchCompute(compute, _kernelShift,
      Mathf.CeilToInt((float)_vxgi.resolution / _threadsShift.x),
      Mathf.CeilToInt((float)_vxgi.resolution / _threadsShift.y),
      Mathf.CeilToInt((float)_vxgi.resolution / _threadsShift.z)
    );
    _command.EndSample(_sampleShift);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();

    Filter(renderContext);
  }

  void InitializeKernel() {
    _kernelFilter = 2 * (int)_vxgi.mipmapFilterMode;

    if (!VXGIRenderPipeline.isD3D11Supported) _kernelFilter += 1;

    _kernelShift = compute.FindKernel("CSShift");
    _threadsFilter = new NumThreads(compute, _kernelFilter);
    _threadsShift = new NumThreads(compute, _kernelShift);
  }

  [System.Diagnostics.Conditional("UNITY_EDITOR")]
  void UpdateKernel() {
    InitializeKernel();
  }
}
