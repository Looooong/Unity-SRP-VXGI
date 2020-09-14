#ifndef TRANSFORMATIONS
#define TRANSFORMATIONS


float3 WorldSpaceToNormalizedVoxelSpace(float3 worldPos)
{
	return (worldPos - VXGI_VolumeMin) / VXGI_VolumeSize;
}

float3 NormalizedVoxelSpaceToWorldSpace(float3 normVoxelPos)
{
	return mad(normVoxelPos, VXGI_VolumeSize, VXGI_VolumeMin);
}


#endif