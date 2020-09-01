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
  Voxelizer _vxgi;

  public VoxelShader(Voxelizer vxgi) {
    _vxgi = vxgi;

    _command = new CommandBuffer { name = "VXGI.VoxelShader" };

    _arguments = new ComputeBuffer(3, sizeof(int), ComputeBufferType.IndirectArguments);
    _arguments.SetData(new int[] { 1, 1, 1 });

    _voxelFragmentsCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
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
    _command.SetComputeFloatParam(compute, ShaderIDs.VXGI_VolumeExtent, .5f * _vxgi.Bound);
    _command.SetComputeFloatParam(compute, ShaderIDs.VXGI_VolumeSize, _vxgi.Bound);
    _command.SetComputeIntParam(compute, ShaderIDs.Resolution, (int)_vxgi.Resolution);
    _command.SetComputeIntParam(compute, ShaderIDs.LightCount, _vxgi.LightsourcesCount);
    _command.SetComputeIntParam(compute, ShaderIDs.VXGI_CascadesCount, _vxgi.Cascades);
    _command.SetComputeBufferParam(compute, _kernelRender, ShaderIDs.LightSources, _vxgi.LightSources);
    _command.SetComputeBufferParam(compute, _kernelRender, ShaderIDs.VoxelBuffer, _vxgi.voxelBuffer);
    _command.SetComputeTextureParam(compute, _kernelRender, ShaderIDs.FragmentPointers, _vxgi.voxelPointerBuffer);
    _command.SetComputeMatrixParam(compute, ShaderIDs.VoxelToWorld, _vxgi.voxelToWorld);
    _command.SetComputeMatrixParam(compute, ShaderIDs.WorldToVoxel, _vxgi.worldToVoxel);

    _command.SetComputeVectorParam(compute, ShaderIDs.VXGI_VolumeCenter, _vxgi.voxelSpaceCenter);

    _command.SetComputeTextureParam(compute, _kernelRender, ShaderIDs.Target, _vxgi.radiance);
    _command.SetComputeTextureParam(compute, _kernelRender, ShaderIDs.Radiance[0], _vxgi.radiance);

    _command.DispatchCompute(compute, _kernelRender,
      Mathf.CeilToInt((float)_vxgi.PointerStorageResolution.x / 4),
      Mathf.CeilToInt((float)_vxgi.PointerStorageResolution.y / 4),
      Mathf.CeilToInt((float)_vxgi.PointerStorageResolution.z / 4)
    );

    _command.EndSample(sampleComputeRender);
  }

  void Setup() {
    _command.BeginSample(sampleSetup);

    UpdateKernels();

    _command.EndSample(sampleSetup);
  }

  void UpdateKernels() {
    _kernelRender = 0;
    if (_vxgi.AnisotropicColors) {
      _kernelRender += 2;
    }
    _kernelRender += 1;
  }
}
