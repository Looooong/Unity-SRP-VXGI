#ifndef NOISEUTILS
#define NOISEUTILS
//from https://www.shadertoy.com/view/4djSRW
// Hash without Sine
// MIT License...
/* Copyright (c)2014 David Hoskins.
Small modifications (GLSL to HLSL, renaming) by Sean Boettger

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.*/

//----------------------------------------------------------------------------------------
//  1 out, 1 in...
float hash(float p)
{
  p = frac(p * .1031);
  p *= p + 33.33;
  p *= p + p;
  return frac(p);
}

//----------------------------------------------------------------------------------------
//  1 out, 2 in...
float hash(float2 p)
{
  float3 p3 = frac(float3(p.xyx) * .1031);
  p3 += dot(p3, p3.yzx + 33.33);
  return frac((p3.x + p3.y) * p3.z);
}

//----------------------------------------------------------------------------------------
//  1 out, 3 in...
float hash(float3 p3)
{
  p3 = frac(p3 * .1031);
  p3 += dot(p3, p3.yzx + 33.33);
  return frac((p3.x + p3.y) * p3.z);
}

//----------------------------------------------------------------------------------------
//  2 out, 1 in...
float2 hash2(float p)
{
  float3 p3 = frac(float3(p, p, p) * float3(.1031, .1030, .0973));
  p3 += dot(p3, p3.yzx + 33.33);
  return frac((p3.xx + p3.yz) * p3.zy);

}

//----------------------------------------------------------------------------------------
///  2 out, 2 in...
float2 hash2(float2 p)
{
  float3 p3 = frac(float3(p.xyx) * float3(.1031, .1030, .0973));
  p3 += dot(p3, p3.yzx + 33.33);
  return frac((p3.xx + p3.yz) * p3.zy);

}

//----------------------------------------------------------------------------------------
///  2 out, 3 in...
float2 hash2(float3 p3)
{
  p3 = frac(p3 * float3(.1031, .1030, .0973));
  p3 += dot(p3, p3.yzx + 33.33);
  return frac((p3.xx + p3.yz) * p3.zy);
}

//----------------------------------------------------------------------------------------
//  3 out, 1 in...
float3 hash3(float p)
{
  float3 p3 = frac(float3(p, p, p) * float3(.1031, .1030, .0973));
  p3 += dot(p3, p3.yzx + 33.33);
  return frac((p3.xxy + p3.yzz) * p3.zyx);
}


//----------------------------------------------------------------------------------------
///  3 out, 2 in...
float3 hash3(float2 p)
{
  float3 p3 = frac(float3(p.xyx) * float3(.1031, .1030, .0973));
  p3 += dot(p3, p3.yxz + 33.33);
  return frac((p3.xxy + p3.yzz) * p3.zyx);
}

//----------------------------------------------------------------------------------------
///  3 out, 3 in...
float3 hash3(float3 p3)
{
  p3 = frac(p3 * float3(.1031, .1030, .0973));
  p3 += dot(p3, p3.yxz + 33.33);
  return frac((p3.xxy + p3.yxx) * p3.zyx);

}

//----------------------------------------------------------------------------------------
// 4 out, 1 in...
float4 hash4(float p)
{
  float4 p4 = frac(float4(p, p, p, p) * float4(.1031, .1030, .0973, .1099));
  p4 += dot(p4, p4.wzxy + 33.33);
  return frac((p4.xxyz + p4.yzzw) * p4.zywx);

}

//----------------------------------------------------------------------------------------
// 4 out, 2 in...
float4 hash4(float2 p)
{
  float4 p4 = frac(float4(p.xyxy) * float4(.1031, .1030, .0973, .1099));
  p4 += dot(p4, p4.wzxy + 33.33);
  return frac((p4.xxyz + p4.yzzw) * p4.zywx);

}

//----------------------------------------------------------------------------------------
// 4 out, 3 in...
float4 hash4(float3 p)
{
  float4 p4 = frac(float4(p.xyzx) * float4(.1031, .1030, .0973, .1099));
  p4 += dot(p4, p4.wzxy + 33.33);
  return frac((p4.xxyz + p4.yzzw) * p4.zywx);
}

//----------------------------------------------------------------------------------------
// 4 out, 4 in...
float4 hash4(float4 p4)
{
  p4 = frac(p4 * float4(.1031, .1030, .0973, .1099));
  p4 += dot(p4, p4.wzxy + 33.33);
  return frac((p4.xxyz + p4.yzzw) * p4.zywx);
}

//-------------------------------------------------------------------




float stratify(float val, int count, int index)
{
  return 1.0/count * index + val / count;
}
#endif