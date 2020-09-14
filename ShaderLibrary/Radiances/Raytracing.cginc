#ifndef VXGI_SHADERLIBRARY_RADIANCES_RAYTRACING
#define VXGI_SHADERLIBRARY_RADIANCES_RAYTRACING

#include "UnityCG.cginc"
#include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Utilities.cginc"
#include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Radiances/Sampler.cginc"
#include "Packages/com.looooong.srp.vxgi/ShaderLibrary/BitManip.cginc"
#include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Noise.cginc"
#include "Packages/com.looooong.srp.vxgi/ShaderLibrary/Radiances/Transformations.cginc"

Texture3D<float> StepMap;
Texture3D<float> StepMapFine2x2x2Encode;
Texture3D<float> Binary;

float2 DiskSampleUniform(float2 u) {
  float r = sqrt(u.x);
  float phi = 6.28 * u.y;

  float sinPhi = sin(phi);
  float cosPhi = cos(phi);
  return r * float2(cosPhi, sinPhi);
}

float3 HemisphereCosineSample(float2 u) {
  float3 localL;

  localL.xy = DiskSampleUniform(u);
  localL.z = sqrt(1.0 - u.x);

  return localL;
}
float3x3 GenerateNormalBasis(float3 normal)
{
  float3 binor = cross(normal, float3(0.577350269, 0.577350269, 0.577350269));
  float3 tang = cross(normal, binor);
  return (float3x3(binor, tang, normal));
}

float3 SphereSample(float3 lightPos, float radius, float3 randomSample) {
  float x = radius * sqrt(randomSample.x) * cos(2 * 3.1415 * randomSample.y);
  float y = radius * sqrt(randomSample.x) * sin(2 * 3.1415 * randomSample.y);
  float z = sqrt(radius * radius - x * x - y * y) * sin(3.1415 * (randomSample.z - 0.5));

  return lightPos + float3(x, y, z);
}

SamplerState my_point_clamp_sampler;

struct raycastResult
{
  float4 color;
  float3 normalizedPosition;
  float3 position;
  float distance;
  int steps;
  bool sky;
  bool distlimit;
  //For profiling
  float buckets[8];

  float4 BucketToColor()
  {
    float maxval = max(max(max(max(max(max(max(buckets[0], buckets[1]), buckets[2]), buckets[3]), buckets[4]), buckets[5]), buckets[6]), buckets[7]);
    if (maxval = buckets[0])return float4(0, 0, 0, 1);
    if (maxval = buckets[1])return float4(1, 0, 0, 1);
    if (maxval = buckets[2])return float4(1, 0, 0, 1);
    if (maxval = buckets[3])return float4(0, 1, 0, 1);
    if (maxval = buckets[4])return float4(0, 1, 0, 1);
    if (maxval = buckets[5])return float4(0, 0, 1, 1);
    if (maxval = buckets[6])return float4(0, 0, 1, 1);
    if (maxval = buckets[7])return float4(0, 0, 1, 1);
  }
  void UpdatePositionInfo(float3 startWS)
  {
    position = NormalizedVoxelSpaceToWorldSpace(normalizedPosition);
    distance = distance(startWS, position);
  }
};

raycastResult VoxelRaycast(float3 rayPos, float3 rayDir, int maxSteps, float maxVoxelDist)
{
  raycastResult result;
  [unroll]
  for(int iiiiii = 0; iiiiii < 8 ; iiiiii++)
    result.buckets[iiiiii] = 0;

  //Transform the ray into voxel space
  float3 normPos = WorldSpaceToNormalizedVoxelSpace(rayPos);

  //Intersection precalcs
  float3 invdir = float3(1.0, 1.0, 1.0) / rayDir;
  float3 raystep = step(0, rayDir) + 1.0 / 640.0 * rayDir;
  float3 rayDirScaled =  rayDir / BinaryResolution;
  raystep = raystep * invdir;
  invdir = -invdir;
  float buckets[8] = {0,0,0,0,0,0,0,0};

  float rayDist = 0;
  for(int i = 0; i < maxSteps; i ++)
  {
    if (dot(normPos - saturate(normPos), float3(1, 1, 1)) != 0)
    {
      //Sample sky-light
      result.color = TempSkyColor;//float4(0.6, 0.86, 1, 1) * 0.3;
      result.sky = true;
      result.distlimit = false;
      result.normalizedPosition = normPos;
      result.steps = i;
      result.UpdatePositionInfo(rayPos);
      return result;
      //The following doesn't seem to work...
      //float4 skyData = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, rayDir, 0);
      //return float4(DecodeHDR(skyData, unity_SpecCube0_HDR), 1.0)*100;
    }

    if (maxVoxelDist > 0 && rayDist >= maxVoxelDist)
    {
      result.color = 0;
      result.distlimit = true;
      result.sky = false;
      result.normalizedPosition = normPos;
      result.steps = i;
      result.UpdatePositionInfo(rayPos);
      return result;
    }

    float intdist = 0;
    //Sample stepping size from half-resolution flat-octree thing
    intdist = StepMap.SampleLevel(my_point_clamp_sampler, normPos, 0).r * 255.0;//uint(StepMap.Load(uint4(normPos * StepMapResolution, 0)).r * 255.0);

    if (intdist == 1)result.buckets[0] += 1;
    if (intdist == 2)result.buckets[1] += 1;
    if (intdist == 4)result.buckets[2] += 1;
    if (intdist == 8)result.buckets[3] += 1;
    if (intdist == 16)result.buckets[4] += 1;
    if (intdist == 32)result.buckets[5] += 1;
    if (intdist == 64)result.buckets[6] += 1;
    if (intdist == 128)result.buckets[7] += 1;

    //If small enough check against the finest voxel resolution for intersection
    if (intdist == 1)
    {
      //if (Binary.Load(uint4(normPos * BinaryResolution, 0)).r != 0)
      //if (Binary.SampleLevel(my_point_clamp_sampler, normPos, 0).r != 0)
      if ((uint(StepMapFine2x2x2Encode.SampleLevel(my_point_clamp_sampler, normPos, 0).r * 255) & Get2x2x2MaskAbs(uint3(normPos * BinaryResolution))) != 0)
        break;
    }

    float dist = float(intdist);
    //vec3 deltas = (raystep - fract(normPos*distinv))*invdir;
    float3 madtest = frac(normPos * BinaryResolution / dist);
    float3 deltas = madtest * invdir + raystep;

    //Step by the smallest one
    float tinc = min(deltas.x, min(deltas.y, deltas.z));
    normPos += rayDirScaled * tinc * dist;
    rayDist += tinc * dist;
  }

  //Better results when resolution's don't match, but surprisingly slower
  //result.color = Radiance0.SampleLevel(RADIANCE_SAMPLER, normPos, 0.0);
  //result.color.rgb /= max(0.001, result.color.a);

  result.color = Radiance0.Load(uint4(normPos * Resolution, 0));

  result.sky = false;
  result.distlimit = false;
  result.normalizedPosition = normPos;
  result.steps = i;
  result.UpdatePositionInfo(rayPos);

  return result;
}

float3 CalcBiasPosition(float3 worldPos, float3 rayDir, float3 worldNor)
{
  //Ad-hoc ray bias to remove self intersection due to the voxels being kinda big...
  return worldPos + (rayDir / max(0.3, dot(worldNor, rayDir)) * BinaryVoxelSize * 0.75 + worldNor * BinaryVoxelSize * 0.75);

  //Something along these lines should be best but currently it has artifacts and leaking problems
  /*float3 voxelPos = (worldPos - VXGI_VolumeMin) / VXGI_VolumeSize * BinaryResolution;
  float3 voxelRel = step(0, worldNor) - frac(voxelPos);

  float3 corner = voxelPos + voxelRel;
  float3 cornerPos = corner / BinaryResolution * VXGI_VolumeSize + VXGI_VolumeMin + worldNor * BinaryVoxelSize;

  float denom = dot(rayDir, worldNor);
  float3 p0l0 = cornerPos-worldPos;
  float t = dot(p0l0, worldNor) / denom;

  worldPos += rayDir * t;

  return worldPos;*/
}
//Raycast after biasing ray outwards to avoid intersection
raycastResult VoxelRaycastBias(float3 worldPos, float3 rayDir, float3 worldNor, float maxSteps)
{
  return VoxelRaycast(CalcBiasPosition(worldPos, rayDir, worldNor), rayDir, maxSteps, 0);
}
raycastResult VoxelRaycastBias(float3 worldPos, float3 rayDir, float3 worldNor, float maxSteps, float maxVoxelDist)
{
  return VoxelRaycast(CalcBiasPosition(worldPos, rayDir, worldNor), rayDir, maxSteps, maxVoxelDist);
}

float3 StratifiedHemisphereSample(float3 worldPos, float3 worldNor, int maxSteps, uint qual, float rand)
{
  if (qual <= 0)return float3(0,0,0);
  int samples = qual * qual;
  //Create rotation basis for hemisphere 
  //(so I can generate random directions on a hemisphere then rotate them to follow the normal)
  float3x3 normalBasis = GenerateNormalBasis(worldNor);

  float3 radiance = 0;
  [loop]
  for (int s = 0; s < samples; s++)
  {
    rand += 0.0267;
    //Rotate a random hemisphere sample into the normal basis
    float3 castdir = mul(HemisphereCosineSample(float2(stratify(hash(rand), qual, s % qual), stratify(hash(rand * 723.389), qual, s / qual))), normalBasis);
    float3 col = VoxelRaycastBias(worldPos, castdir, worldNor, maxSteps).color.rgb;
    radiance += col;
  }
  radiance /= samples;

  return radiance;
}

#endif