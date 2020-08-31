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
  const string sampleComputeRender = "Compute.Render";
  const string sampleSetup = "Setup";

  int _kernelRender;
  CommandBuffer _command;
  ComputeBuffer _arguments;
  ComputeBuffer _voxelFragmentsCountBuffer;
  ComputeShader _compute;
  RenderTextureDescriptor _descriptor;
  VXGI _vxgi;

  public VoxelShader(VXGI vxgi) {
    _vxgi = vxgi;

    _command = new CommandBuffer { name = "VXGI.VoxelShader" };

    _arguments = new ComputeBuffer(3, sizeof(int), ComputeBufferType.IndirectArguments);
    _arguments.SetData(new int[] { 1, 1, 1 });

    _voxelFragmentsCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);

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
    _voxelFragmentsCountBuffer.Dispose();
    _command.Dispose();
  }

  public void Render(ScriptableRenderContext renderContext) {
    Setup();
    ComputeRender();
    Cleanup();

    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();
  }

  void Cleanup() {
    _command.BeginSample(sampleCleanup);

    _command.EndSample(sampleCleanup);
  }

  void ComputeRender() {
    _command.BeginSample(sampleComputeRender);

    _command.CopyCounterValue(_vxgi.voxelBuffer, _voxelFragmentsCountBuffer, 0);
    _command.SetComputeBufferParam(compute, _kernelRender, ShaderIDs.VXGI_VoxelFragmentsCountBuffer, _voxelFragmentsCountBuffer);
    _command.SetComputeFloatParam(compute, ShaderIDs.VXGI_VolumeExtent, .5f * _vxgi.bound);
    _command.SetComputeFloatParam(compute, ShaderIDs.VXGI_VolumeSize, _vxgi.bound);
    _command.SetComputeIntParam(compute, ShaderIDs.Resolution, (int)_vxgi.resolution);
    _command.SetComputeIntParam(compute, ShaderIDs.LightCount, _vxgi.Voxelizer.LightsourcesCount);
    _command.SetComputeIntParam(compute, ShaderIDs.VXGI_CascadesCount, _vxgi.CascadesCount);
    _command.SetComputeBufferParam(compute, _kernelRender, ShaderIDs.LightSources, _vxgi.Voxelizer.LightSources);
    _command.SetComputeBufferParam(compute, _kernelRender, ShaderIDs.VoxelBuffer, _vxgi.voxelBuffer);
    _command.SetComputeTextureParam(compute, _kernelRender, ShaderIDs.FragmentPointers, _vxgi.voxelPointerBuffer);
    _command.SetComputeMatrixParam(compute, ShaderIDs.VoxelToWorld, _vxgi.voxelToWorld);
    _command.SetComputeMatrixParam(compute, ShaderIDs.WorldToVoxel, _vxgi.worldToVoxel);
    if (_vxgi.radiances!=null && _vxgi.radiances.Length > 0)
    _command.SetComputeTextureParam(compute, _kernelRender, ShaderIDs.Target, _vxgi.radiances[0]);
    _command.SetComputeVectorParam(compute, ShaderIDs.VXGI_VolumeCenter, _vxgi.voxelSpaceCenter);

    if (_vxgi.CascadesEnabled) {
      _command.SetComputeTextureParam(compute, _kernelRender, ShaderIDs.Target, _vxgi.radiances[0]);
      _command.SetComputeTextureParam(compute, _kernelRender, ShaderIDs.Radiance[0], _vxgi.radiances[0]);
    } else {
      for (var i = 0; i < 9; i++) {
        _command.SetComputeTextureParam(compute, _kernelRender, ShaderIDs.Radiance[i], _vxgi.radiances[Mathf.Min(i, _vxgi.radiances.Length - 1)]);
      }
    }

    _command.DispatchCompute(compute, _kernelRender,
      Mathf.CeilToInt((float)_vxgi.resolution / 4),
      Mathf.CeilToInt((float)_vxgi.resolution / 4),
      Mathf.CeilToInt((float)_vxgi.resolution / 4) * (_vxgi.CascadesEnabled ? _vxgi.CascadesCount : 1)
    );

    _command.EndSample(sampleComputeRender);
  }

  void Setup() {
    _command.BeginSample(sampleSetup);

    UpdateKernels();

    _descriptor.height = _descriptor.width = _descriptor.volumeDepth = (int)_vxgi.resolution;

    if (_vxgi.CascadesEnabled) _descriptor.volumeDepth *= _vxgi.cascadesCount;

    _command.EndSample(sampleSetup);
  }

  void UpdateKernels() {
    _kernelRender = 0;
    if (_vxgi.AnisotropicVoxel) {
      _kernelRender += 2;
    }
    if (_vxgi.CascadesEnabled) {
      _kernelRender += 1;
    }
  }
}
