using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

public class Mipmapper {
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
  VXGI _vxgi;

  public Mipmapper(VXGI vxgi) {
    _vxgi = vxgi;

    _command = new CommandBuffer { name = "VXGI.Mipmapper" };

    if (Application.platform == RuntimePlatform.LinuxEditor || Application.platform == RuntimePlatform.LinuxPlayer) {
      _kernelFilter = 1;
    } else {
      _kernelFilter = 0;
    }

    _kernelShift = compute.FindKernel("CSShift");

    _propDisplacement = Shader.PropertyToID("Displacement");
    _propDst = Shader.PropertyToID("Dst");
    _propDstRes = Shader.PropertyToID("DstRes");
    _propSrc = Shader.PropertyToID("Src");
  }

  public void Dispose() {
    _command.Dispose();
  }

  public void Filter(ScriptableRenderContext renderContext) {
    var radiances = _vxgi.radiances;

    for (var i = 1; i < radiances.Length; i++) {
      int resolution = radiances[i].volumeDepth;
      int groups = Mathf.CeilToInt((float)resolution / 4f);

      _command.BeginSample(_sampleFilter + resolution.ToString());
      _command.SetComputeIntParam(compute, _propDstRes, resolution);
      _command.SetComputeTextureParam(compute, _kernelFilter, _propDst, radiances[i]);
      _command.SetComputeTextureParam(compute, _kernelFilter, _propSrc, radiances[i - 1]);
      _command.DispatchCompute(compute, _kernelFilter, groups, groups, groups);
      _command.EndSample(_sampleFilter + resolution.ToString());
    }

    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();
  }

  public void Shift(ScriptableRenderContext renderContext, Vector3Int displacement) {
    int groups = Mathf.CeilToInt((float)_vxgi.resolution / 4f);

    _command.BeginSample(_sampleShift);
    _command.SetComputeIntParam(compute, _propDstRes, (int)_vxgi.resolution);
    _command.SetComputeIntParams(compute, _propDisplacement, new[] { displacement.x, displacement.y, displacement.z });
    _command.SetComputeTextureParam(compute, _kernelShift, _propDst, _vxgi.radiances[0]);
    _command.DispatchCompute(compute, _kernelShift, groups, groups, groups);
    _command.EndSample(_sampleShift);
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();

    Filter(renderContext);
  }
}
