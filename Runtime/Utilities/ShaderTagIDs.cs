using UnityEngine.Rendering;

internal static class ShaderTagIDs
{
  internal static readonly ShaderTagId Always = new ShaderTagId("Always");
  internal static readonly ShaderTagId Deferred = new ShaderTagId("Deferred");
  internal static readonly ShaderTagId ForwardBase = new ShaderTagId("ForwardBase");
  internal static readonly ShaderTagId PrepassBase = new ShaderTagId("PrepassBase");
  internal static readonly ShaderTagId Vertex = new ShaderTagId("Vertex");
  internal static readonly ShaderTagId VertexLM = new ShaderTagId("VertexLM");
  internal static readonly ShaderTagId VertexLMRGBM = new ShaderTagId("VertexLMRGBM");
  internal static readonly ShaderTagId Voxelization = new ShaderTagId("Voxelization");
}
