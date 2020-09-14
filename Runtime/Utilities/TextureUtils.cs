using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

public static class TextureUtil
{
  public static void DisposeTexture(RenderTexture tex)
  {
    //I will never understand the appeal of managed languages that need things like this.
    if (tex != null)
    {
      if (tex.IsCreated()) tex.Release();
      MonoBehaviour.DestroyImmediate(tex);
    }
  }
  public static void DisposeBuffer(ComputeBuffer buf)
  {
    if (buf != null)
    {
      buf.Dispose();
    }
  }
  public static ComputeBuffer UpdateBuffer(ComputeBuffer buf, int count, int stride, ComputeBufferType type, bool existenceIsRequired)
  {
    if (!existenceIsRequired)
    {
      DisposeBuffer(buf);
      return null;
    }

    if (buf == null || buf.count != count || buf.stride != stride /*|| buf.type != type - can't check the type...*/)
    {
      //Debug.Log("Update buffer" + count.ToString() + ", " + stride.ToString());
      DisposeBuffer(buf);
      return new ComputeBuffer(count, stride, type);
    }

    return buf;
  }
  public static RenderTexture UpdateTexture(RenderTexture tex, Vector3Int res, RenderTextureDescriptor desc, bool existenceIsRequired)
  {
    if (!existenceIsRequired)
    {
      DisposeTexture(tex);
      return null;
    }

    desc.width = res.x;
    desc.height = res.y;
    desc.volumeDepth = res.z;
    //Please tell me there's an in-built way to do this.
    bool identical = tex != null
                     && tex.width == desc.width
                     && tex.height == desc.height
                     && tex.volumeDepth == desc.volumeDepth
                     && tex.graphicsFormat == desc.graphicsFormat
                     && tex.dimension == desc.dimension
                     && tex.enableRandomWrite == desc.enableRandomWrite
                     //&& tex.msaaSamples == desc.msaaSamples - can't check the samples...
                     && tex.sRGB == desc.sRGB
                    ;
    if (!identical)
    {
      //Debug.Log("Update texture" + desc.width.ToString()+ ", " + desc.height.ToString() + ", " + desc.volumeDepth.ToString() + ", ");
      DisposeTexture(tex);
      tex = new RenderTexture(desc);
      tex.filterMode = FilterMode.Point;
      tex.Create();
    }

    return tex;
  }
}