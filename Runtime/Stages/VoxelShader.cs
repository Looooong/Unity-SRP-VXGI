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

  const string sampleCleanup = "Cleanup";
  const string sampleComputeAggregate = "Compute.Aggregate";
  const string sampleComputeClear = "Compute.Clear";
  const string sampleComputeRender = "Compute.Render";
  const string sampleSetup = "Setup";

  int _kernelAggregate;
  int _kernelClear;
  int _kernelRender;
  int _propLightCount;
  int _propLightSources;
  int _propRadianceBA;
  int _propRadianceRG;
  int _propRadianceCount;
  int _propResolution;
  int _propTarget;
  int _propVoxelBuffer;
  CommandBuffer _command;
  ComputeBuffer _arguments;
  ComputeBuffer _lightSources;
  ComputeShader _compute;
  NumThreads _threadsAggregate;
  NumThreads _threadsClear;
  NumThreads _threadsTrace;
  RenderTextureDescriptor _descriptor;
  VXGI _vxgi;

  public VoxelShader(VXGI vxgi) {
    _vxgi = vxgi;

    _command = new CommandBuffer { name = "VXGI.VoxelShader" };

    _arguments = new ComputeBuffer(3, sizeof(int), ComputeBufferType.IndirectArguments);
    _arguments.SetData(new int[] { 1, 1, 1 });
    _lightSources = new ComputeBuffer(64, LightSource.size);

    _kernelAggregate = VXGIRenderPipeline.isD3D11Supported ? 0 : 1;
    _kernelClear = compute.FindKernel("CSClear");
    _kernelRender = compute.FindKernel("CSRender");

    _propLightCount = Shader.PropertyToID("LightCount");
    _propLightSources = Shader.PropertyToID("LightSources");
    _propRadianceBA = Shader.PropertyToID("RadianceBA");
    _propRadianceRG = Shader.PropertyToID("RadianceRG");
    _propRadianceCount = Shader.PropertyToID("RadianceCount");
    _propResolution = Shader.PropertyToID("Resolution");
    _propTarget = Shader.PropertyToID("Target");
    _propVoxelBuffer = Shader.PropertyToID("VoxelBuffer");

    _threadsAggregate = new NumThreads(compute, _kernelAggregate);
    _threadsClear = new NumThreads(compute, _kernelClear);
    _threadsTrace = new NumThreads(compute, _kernelRender);

    _descriptor = new RenderTextureDescriptor() {
      colorFormat = RenderTextureFormat.RInt,
      dimension = TextureDimension.Tex3D,
      enableRandomWrite = true,
      msaaSamples = 1,
      sRGB = false
    };
  }

  public void Dispose() {
    _arguments.Dispose();
    _command.Dispose();
    _lightSources.Dispose();
  }

  public void Render(ScriptableRenderContext renderContext) {
    Setup();
    ComputeClear();
    ComputeRender();
    ComputeAggregate();
    Cleanup();

    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();
  }

  void Cleanup()
  {
    _command.BeginSample(sampleCleanup);

    _command.ReleaseTemporaryRT(_propRadianceBA);
    _command.ReleaseTemporaryRT(_propRadianceRG);
    _command.ReleaseTemporaryRT(_propRadianceCount);

    _command.EndSample(sampleCleanup);
  }

  void ComputeAggregate() {
    _command.BeginSample(sampleComputeAggregate);

    _command.SetComputeTextureParam(compute, _kernelAggregate, _propRadianceBA, _propRadianceBA);
    _command.SetComputeTextureParam(compute, _kernelAggregate, _propRadianceRG, _propRadianceRG);
    _command.SetComputeTextureParam(compute, _kernelAggregate, _propRadianceCount, _propRadianceCount);
    _command.SetComputeTextureParam(compute, _kernelAggregate, _propTarget, _vxgi.radiances[0]);
    _command.DispatchCompute(compute, _kernelAggregate,
      Mathf.CeilToInt((float)_vxgi.resolution / _threadsAggregate.x),
      Mathf.CeilToInt((float)_vxgi.resolution / _threadsAggregate.y),
      Mathf.CeilToInt((float)_vxgi.resolution / _threadsAggregate.z)
    );

    _command.EndSample(sampleComputeAggregate);
  }

  void ComputeClear() {
    _command.BeginSample(sampleComputeClear);

    _command.SetComputeTextureParam(compute, _kernelClear, _propRadianceBA, _propRadianceBA);
    _command.SetComputeTextureParam(compute, _kernelClear, _propRadianceRG, _propRadianceRG);
    _command.SetComputeTextureParam(compute, _kernelClear, _propRadianceCount, _propRadianceCount);
    _command.DispatchCompute(compute, _kernelClear,
      Mathf.CeilToInt((float)_vxgi.resolution / _threadsClear.x),
      Mathf.CeilToInt((float)_vxgi.resolution / _threadsClear.y),
      Mathf.CeilToInt((float)_vxgi.resolution / _threadsClear.z)
    );

    _command.EndSample(sampleComputeClear);
  }

  void ComputeRender() {
    _command.BeginSample(sampleComputeRender);

    _lightSources.SetData(_vxgi.lights);

    _command.SetComputeIntParam(compute, _propResolution, (int)_vxgi.resolution);
    _command.SetComputeIntParam(compute, _propLightCount, _vxgi.lights.Count);
    _command.SetComputeBufferParam(compute, _kernelRender, _propLightSources, _lightSources);
    _command.SetComputeBufferParam(compute, _kernelRender, _propVoxelBuffer, _vxgi.voxelBuffer);
    _command.SetComputeMatrixParam(compute, "VoxelToWorld", _vxgi.voxelToWorld);
    _command.SetComputeMatrixParam(compute, "WorldToVoxel", _vxgi.worldToVoxel);
    _command.SetComputeTextureParam(compute, _kernelRender, _propRadianceBA, _propRadianceBA);
    _command.SetComputeTextureParam(compute, _kernelRender, _propRadianceRG, _propRadianceRG);
    _command.SetComputeTextureParam(compute, _kernelRender, _propRadianceCount, _propRadianceCount);

    for (var i = 0; i < 9; i++) {
      _command.SetComputeTextureParam(compute, _kernelRender, "Radiance" + i, _vxgi.radiances[Mathf.Min(i, _vxgi.radiances.Length - 1)]);
    }

    _command.CopyCounterValue(_vxgi.voxelBuffer, _arguments, 0);
    _vxgi.parameterizer.Parameterize(_command, _arguments, _threadsTrace);
    _command.DispatchCompute(compute, _kernelRender, _arguments, 0);

    _command.EndSample(sampleComputeRender);
  }

  void Setup()
  {
    _command.BeginSample(sampleSetup);

    UpdateNumThreads();
    _descriptor.height = _descriptor.width = _descriptor.volumeDepth = (int)_vxgi.resolution;
    _command.GetTemporaryRT(_propRadianceCount, _descriptor);
    _command.GetTemporaryRT(_propRadianceBA, _descriptor);
    _command.GetTemporaryRT(_propRadianceRG, _descriptor);

    _command.EndSample(sampleSetup);
  }

  [System.Diagnostics.Conditional("UNITY_EDITOR")]
  void UpdateNumThreads()
  {
    _threadsAggregate = new NumThreads(compute, _kernelAggregate);
    _threadsClear = new NumThreads(compute, _kernelClear);
    _threadsTrace = new NumThreads(compute, _kernelRender);
  }
}
