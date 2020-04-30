using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

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
  int _propDisplacement;
  int _propDst;
  int _propDstRes;
  int _propSrc;
  CommandBuffer _command;
  ComputeShader _compute;
  NumThreads _threadsFilter;
  NumThreads _threadsShift;
  VXGI _vxgi;

  public Mipmapper(VXGI vxgi) {
    _vxgi = vxgi;

    _command = new CommandBuffer { name = "VXGI.Mipmapper" };

    InitializeKernel();

    _propDisplacement = Shader.PropertyToID("Displacement");
    _propDst = Shader.PropertyToID("Dst");
    _propDstRes = Shader.PropertyToID("DstRes");
    _propSrc = Shader.PropertyToID("Src");
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
      _command.SetComputeIntParam(compute, _propDstRes, resolution);
      _command.SetComputeTextureParam(compute, _kernelFilter, _propDst, radiances[i]);
      _command.SetComputeTextureParam(compute, _kernelFilter, _propSrc, radiances[i - 1]);
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
    _command.SetComputeIntParam(compute, _propDstRes, (int)_vxgi.resolution);
    _command.SetComputeIntParams(compute, _propDisplacement, new[] { displacement.x, displacement.y, displacement.z });
    _command.SetComputeTextureParam(compute, _kernelShift, _propDst, _vxgi.radiances[0]);
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
