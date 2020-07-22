using System.Collections.ObjectModel;
using UnityEngine;

internal static class ShaderIDs {
  internal static readonly Collection<int> Radiance = new Collection<int>(new [] {
    Shader.PropertyToID("Radiance0"),
    Shader.PropertyToID("Radiance1"),
    Shader.PropertyToID("Radiance2"),
    Shader.PropertyToID("Radiance3"),
    Shader.PropertyToID("Radiance4"),
    Shader.PropertyToID("Radiance5"),
    Shader.PropertyToID("Radiance6"),
    Shader.PropertyToID("Radiance7"),
    Shader.PropertyToID("Radiance8"),
    Shader.PropertyToID("Radiance9"),
  });
  internal static readonly int _CameraDepthNormalsTexture = Shader.PropertyToID("_CameraDepthNormalsTexture");
  internal static readonly int _CameraDepthTexture = Shader.PropertyToID("_CameraDepthTexture");
  internal static readonly int _CameraGBufferTexture0 = Shader.PropertyToID("_CameraGBufferTexture0");
  internal static readonly int _CameraGBufferTexture1 = Shader.PropertyToID("_CameraGBufferTexture1");
  internal static readonly int _CameraGBufferTexture2 = Shader.PropertyToID("_CameraGBufferTexture2");
  internal static readonly int _CameraGBufferTexture3 = Shader.PropertyToID("_CameraGBufferTexture3");
  internal static readonly int Arguments = Shader.PropertyToID("Arguments");
  internal static readonly int BlitViewport = Shader.PropertyToID("BlitViewport");
  internal static readonly int ClipToVoxel = Shader.PropertyToID("ClipToVoxel");
  internal static readonly int ClipToWorld = Shader.PropertyToID("ClipToWorld");
  internal static readonly int Displacement = Shader.PropertyToID("Displacement");
  internal static readonly int Dummy = Shader.PropertyToID("Dummy");
  internal static readonly int FrameBuffer = Shader.PropertyToID("FrameBuffer");
  internal static readonly int IndirectDiffuseModifier = Shader.PropertyToID("IndirectDiffuseModifier");
  internal static readonly int IndirectSpecularModifier = Shader.PropertyToID("IndirectSpecular`Modifier");
  internal static readonly int LightCount = Shader.PropertyToID("LightCount");
  internal static readonly int LightSources = Shader.PropertyToID("LightSources");
  internal static readonly int LowResColor = Shader.PropertyToID("LowResColor");
  internal static readonly int LowResDepth = Shader.PropertyToID("LowResDepth");
  internal static readonly int MipmapLevel = Shader.PropertyToID("MipmapLevel");
  internal static readonly int NumThreads = Shader.PropertyToID("NumThreads");
  internal static readonly int RadianceBA = Shader.PropertyToID("RadianceBA");
  internal static readonly int RadianceCount = Shader.PropertyToID("RadianceCount");
  internal static readonly int RadianceRG = Shader.PropertyToID("RadianceRG");
  internal static readonly int RayTracingStep = Shader.PropertyToID("RayTracingStep");
  internal static readonly int Resolution = Shader.PropertyToID("Resolution");
  internal static readonly int Source = Shader.PropertyToID("Source");
  internal static readonly int Target = Shader.PropertyToID("Target");
  internal static readonly int VoxelBuffer = Shader.PropertyToID("VoxelBuffer");
  internal static readonly int VoxelToWorld = Shader.PropertyToID("VoxelToWorld");
  internal static readonly int WorldToVoxel = Shader.PropertyToID("WorldToVoxel");
}
