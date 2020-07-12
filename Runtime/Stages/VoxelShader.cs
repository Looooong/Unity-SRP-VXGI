using UnityEngine;
using UnityEngine.Rendering;

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

    ReloadKernels();

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

  void Cleanup() {
    _command.BeginSample(sampleCleanup);

    _command.ReleaseTemporaryRT(ShaderIDs.RadianceBA);
    _command.ReleaseTemporaryRT(ShaderIDs.RadianceRG);
    _command.ReleaseTemporaryRT(ShaderIDs.RadianceCount);

    _command.EndSample(sampleCleanup);
  }

  void ComputeAggregate() {
    _command.BeginSample(sampleComputeAggregate);

    _command.SetComputeTextureParam(compute, _kernelAggregate, ShaderIDs.RadianceBA, ShaderIDs.RadianceBA);
    _command.SetComputeTextureParam(compute, _kernelAggregate, ShaderIDs.RadianceRG, ShaderIDs.RadianceRG);
    _command.SetComputeTextureParam(compute, _kernelAggregate, ShaderIDs.RadianceCount, ShaderIDs.RadianceCount);
    _command.SetComputeTextureParam(compute, _kernelAggregate, ShaderIDs.Target, _vxgi.radiances[0]);
    _command.DispatchCompute(compute, _kernelAggregate,
      Mathf.CeilToInt((float)_vxgi.resolution / _threadsAggregate.x),
      Mathf.CeilToInt((float)_vxgi.resolution / _threadsAggregate.y),
      Mathf.CeilToInt((float)_vxgi.resolution / _threadsAggregate.z) * (_vxgi.CascadesEnabled ? _vxgi.CascadesCount : 1)
    );

    _command.EndSample(sampleComputeAggregate);
  }

  void ComputeClear() {
    _command.BeginSample(sampleComputeClear);

    if (_vxgi.CascadesEnabled) _command.SetComputeTextureParam(compute, _kernelClear, ShaderIDs.Target, _vxgi.radiances[0]);

    _command.SetComputeTextureParam(compute, _kernelClear, ShaderIDs.RadianceBA, ShaderIDs.RadianceBA);
    _command.SetComputeTextureParam(compute, _kernelClear, ShaderIDs.RadianceRG, ShaderIDs.RadianceRG);
    _command.SetComputeTextureParam(compute, _kernelClear, ShaderIDs.RadianceCount, ShaderIDs.RadianceCount);
    _command.DispatchCompute(compute, _kernelClear,
      Mathf.CeilToInt((float)_vxgi.resolution / _threadsClear.x),
      Mathf.CeilToInt((float)_vxgi.resolution / _threadsClear.y),
      Mathf.CeilToInt((float)_vxgi.resolution / _threadsClear.z) * (_vxgi.CascadesEnabled ? _vxgi.CascadesCount : 1)
    );

    _command.EndSample(sampleComputeClear);
  }

  void ComputeRender() {
    _command.BeginSample(sampleComputeRender);

    _lightSources.SetData(_vxgi.lights);

    _command.SetComputeFloatParam(compute, ShaderIDs.VXGI_VolumeExtent, .5f * _vxgi.bound);
    _command.SetComputeFloatParam(compute, ShaderIDs.VXGI_VolumeSize, _vxgi.bound);
    _command.SetComputeIntParam(compute, ShaderIDs.Resolution, (int)_vxgi.resolution);
    _command.SetComputeIntParam(compute, ShaderIDs.LightCount, _vxgi.lights.Count);
    _command.SetComputeIntParam(compute, ShaderIDs.VXGI_CascadesCount, _vxgi.CascadesCount);
    _command.SetComputeBufferParam(compute, _kernelRender, ShaderIDs.LightSources, _lightSources);
    _command.SetComputeBufferParam(compute, _kernelRender, ShaderIDs.VoxelBuffer, _vxgi.voxelBuffer);
    _command.SetComputeMatrixParam(compute, ShaderIDs.VoxelToWorld, _vxgi.voxelToWorld);
    _command.SetComputeMatrixParam(compute, ShaderIDs.WorldToVoxel, _vxgi.worldToVoxel);
    _command.SetComputeTextureParam(compute, _kernelRender, ShaderIDs.RadianceBA, ShaderIDs.RadianceBA);
    _command.SetComputeTextureParam(compute, _kernelRender, ShaderIDs.RadianceRG, ShaderIDs.RadianceRG);
    _command.SetComputeTextureParam(compute, _kernelRender, ShaderIDs.RadianceCount, ShaderIDs.RadianceCount);
    _command.SetComputeVectorParam(compute, ShaderIDs.VXGI_VolumeCenter, _vxgi.voxelSpaceCenter);

    if (_vxgi.CascadesEnabled) {
      _command.SetComputeTextureParam(compute, _kernelRender, ShaderIDs.Target, _vxgi.radiances[0]);
      _command.SetComputeTextureParam(compute, _kernelRender, ShaderIDs.Radiance[0], _vxgi.radiances[0]);
    } else {
      for (var i = 0; i < 9; i++) {
        _command.SetComputeTextureParam(compute, _kernelRender, ShaderIDs.Radiance[i], _vxgi.radiances[Mathf.Min(i, _vxgi.radiances.Length - 1)]);
      }
    }

    _command.CopyCounterValue(_vxgi.voxelBuffer, _arguments, 0);
    _vxgi.parameterizer.Parameterize(_command, _arguments, _threadsTrace);
    _command.DispatchCompute(compute, _kernelRender, _arguments, 0);

    _command.EndSample(sampleComputeRender);
  }

  void Setup() {
    _command.BeginSample(sampleSetup);

#if UNITY_EDITOR
    ReloadKernels();
#endif

    _descriptor.height = _descriptor.width = _descriptor.volumeDepth = (int)_vxgi.resolution;

    if (_vxgi.CascadesEnabled) _descriptor.volumeDepth *= _vxgi.cascadesCount;

    _command.GetTemporaryRT(ShaderIDs.RadianceCount, _descriptor);
    _command.GetTemporaryRT(ShaderIDs.RadianceBA, _descriptor);
    _command.GetTemporaryRT(ShaderIDs.RadianceRG, _descriptor);

    _command.EndSample(sampleSetup);
  }

  void ReloadKernels() {
    if ( _vxgi.CascadesEnabled) {
      _kernelAggregate = VXGIRenderPipeline.isD3D11Supported ? 3 : 2;
      _kernelClear = 5;
      _kernelRender = 7;
    } else {
      _kernelAggregate = VXGIRenderPipeline.isD3D11Supported ? 1 : 0;
      _kernelClear = 4;
      _kernelRender = 6;
    }

    _threadsAggregate = new NumThreads(compute, _kernelAggregate);
    _threadsClear = new NumThreads(compute, _kernelClear);
    _threadsTrace = new NumThreads(compute, _kernelRender);
  }
}
