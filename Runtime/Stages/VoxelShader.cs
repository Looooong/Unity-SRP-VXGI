using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

public class VoxelShader : System.IDisposable {
  public ComputeShader compute {
    get {
      if (_compute == null) _compute = (ComputeShader)Resources.Load("VXGI/Compute/VoxelShader");

      return _compute;
    }
  }

  int _kernelAverage;
  int _kernelRender;
  int _propLightCount;
  int _propLightSources;
  int _propRadianceBuffer;
  int _propResolution;
  int _propTarget;
  int _propVoxelBuffer;
  VXGI _vxgi;
  CommandBuffer _commandAverage;
  CommandBuffer _commandRender;
  ComputeBuffer _arguments;
  ComputeBuffer _lightSources;
  ComputeShader _compute;
  NumThreads _threadsAverage;
  NumThreads _threadsTrace;

  public VoxelShader(VXGI vxgi) {
    _vxgi = vxgi;

    _commandAverage = new CommandBuffer { name = "VoxelShader.Average" };
    _commandRender = new CommandBuffer { name = "VoxelShader.Render" };

    _arguments = new ComputeBuffer(3, sizeof(int), ComputeBufferType.IndirectArguments);
    _arguments.SetData(new int[] { 1, 1, 1 });
    _lightSources = new ComputeBuffer(64, LightSource.size);

    if (Application.platform == RuntimePlatform.LinuxEditor || Application.platform == RuntimePlatform.LinuxPlayer) {
      _kernelAverage = 1;
    } else {
      _kernelAverage = 0;
    }

    _kernelRender = compute.FindKernel("CSRender");

    _propLightCount = Shader.PropertyToID("LightCount");
    _propLightSources = Shader.PropertyToID("LightSources");
    _propRadianceBuffer = Shader.PropertyToID("RadianceBuffer");
    _propResolution = Shader.PropertyToID("Resolution");
    _propTarget = Shader.PropertyToID("Target");
    _propVoxelBuffer = Shader.PropertyToID("VoxelBuffer");

    _threadsAverage = new NumThreads(compute, _kernelAverage);
    _threadsTrace = new NumThreads(compute, _kernelRender);
  }

  public void Dispose() {
    _arguments.Dispose();
    _commandRender.Dispose();
    _commandAverage.Dispose();
    _lightSources.Dispose();
  }

  public void Render(ScriptableRenderContext renderContext) {
#if UNITY_EDITOR
    _threadsAverage = new NumThreads(compute, _kernelAverage);
    _threadsTrace = new NumThreads(compute, _kernelRender);
#endif

    var radiances = _vxgi.radiances;

    _commandRender.BeginSample(_commandRender.name);

    _commandRender.SetComputeIntParam(compute, _propResolution, (int)_vxgi.resolution);

    _commandRender.CopyCounterValue(_vxgi.voxelBuffer, _arguments, 0);
    _vxgi.parameterizer.Parameterize(_commandRender, _arguments, _threadsTrace);

    _lightSources.SetData(_vxgi.lights);

    _commandRender.SetGlobalInt(_propLightCount, _vxgi.lights.Count);
    _commandRender.SetComputeBufferParam(compute, _kernelRender, _propLightSources, _lightSources);

    for (var i = 0; i < 9; i++) {
      _commandRender.SetComputeTextureParam(compute, _kernelRender, "Radiance" + i, radiances[Mathf.Min(i, radiances.Length - 1)]);
    }

    _commandRender.SetComputeMatrixParam(compute, "VoxelToWorld", _vxgi.voxelToWorld);
    _commandRender.SetComputeMatrixParam(compute, "WorldToVoxel", _vxgi.worldToVoxel);
    _commandRender.SetComputeBufferParam(compute, _kernelRender, _propVoxelBuffer, _vxgi.voxelBuffer);
    _commandRender.SetComputeBufferParam(compute, _kernelRender, _propRadianceBuffer, _vxgi.radianceBuffer);
    _commandRender.DispatchCompute(compute, _kernelRender, _arguments, 0);

    _commandRender.EndSample(_commandRender.name);

    renderContext.ExecuteCommandBuffer(_commandRender);

    _commandRender.Clear();

    _commandAverage.BeginSample(_commandAverage.name);

    _commandAverage.SetComputeBufferParam(compute, _kernelAverage, _propRadianceBuffer, _vxgi.radianceBuffer);
    _commandAverage.SetComputeTextureParam(compute, _kernelAverage, _propTarget, radiances[0]);
    _commandAverage.DispatchCompute(compute, _kernelAverage,
      Mathf.CeilToInt((float)_vxgi.resolution / _threadsAverage.x),
      Mathf.CeilToInt((float)_vxgi.resolution / _threadsAverage.y),
      Mathf.CeilToInt((float)_vxgi.resolution / _threadsAverage.z)
    );

    _commandAverage.EndSample(_commandAverage.name);

    renderContext.ExecuteCommandBuffer(_commandAverage);

    _commandAverage.Clear();
  }
}
