using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

public class StepMapper {

  public ComputeShader compute {
    get {
      if (_compute == null) _compute = (ComputeShader)Resources.Load("VXGI/Compute/GenerateAcceleration");

      return _compute;
    }
  }

  public Vector3Int StepMapStorageResolution
  {
    get
    {
      return _vxgi.BinaryStorageResolution / 2;
    }
  }

  const string _sampleFilter = "Filter";

  int _kernelFilter;
  int _kernelShift;
  CommandBuffer _command;
  ComputeShader _compute;
  NumThreads _threadsFilter;
  NumThreads _threadsShift;
  Voxelizer _vxgi;

  public StepMapper(Voxelizer vxgi) {
    _vxgi = vxgi;

    _command = new CommandBuffer { name = "VXGI.Mipmapper" };

    InitializeKernel();
  }

  public void Dispose() {
    _command.Dispose();
  }

  RenderTextureDescriptor _binaryDescriptor;
  public RenderTexture stepmap;
  public RenderTexture StepMapFine2x2x2Encode;
  public void UpdateStorage(bool existenceIsRequired)
  {
    _binaryDescriptor = new RenderTextureDescriptor()
    {
      graphicsFormat = GraphicsFormat.R8_UNorm,
      dimension = TextureDimension.Tex3D,
      enableRandomWrite = true,
      msaaSamples = 1,
    };
    stepmap = TextureUtil.UpdateTexture(stepmap, StepMapStorageResolution, _binaryDescriptor, existenceIsRequired);
    StepMapFine2x2x2Encode = TextureUtil.UpdateTexture(StepMapFine2x2x2Encode, StepMapStorageResolution, _binaryDescriptor, existenceIsRequired);
  }
  public void Filter(ScriptableRenderContext renderContext)
  {
    UpdateKernel();

    var binary = _vxgi.binary;

    _command.BeginSample(_sampleFilter);
    _command.SetComputeTextureParam(compute, _kernelFilter, ShaderIDs.Source, binary);
    _command.SetComputeTextureParam(compute, _kernelFilter, ShaderIDs.Target, stepmap);
    _command.SetComputeTextureParam(compute, _kernelFilter, ShaderIDs.TargetDownscale, StepMapFine2x2x2Encode);
    _command.DispatchCompute(compute, _kernelFilter,
        Mathf.CeilToInt((float)stepmap.width / _threadsFilter.x),
        Mathf.CeilToInt((float)stepmap.height / _threadsFilter.y),
        Mathf.CeilToInt((float)stepmap.volumeDepth / _threadsFilter.z / 32)
    );
    _command.EndSample(_sampleFilter);
    
    renderContext.ExecuteCommandBuffer(_command);
    _command.Clear();
  }

  void InitializeKernel() {
    _kernelFilter = 0;
    _threadsFilter = new NumThreads(compute, _kernelFilter);
  }

  [System.Diagnostics.Conditional("UNITY_EDITOR")]
  void UpdateKernel() {
    InitializeKernel();
  }
}
