#ifndef BitManip
#define BitManip

uint Get2x2x2MaskRel(uint3 p)
{
	return 1 << (p.z * 4 + p.y * 2 + p.x);
	//return (p.z * 15 + 1) * (p.y * 3 + 1) * (p.x + 1);
}
uint Get2x2x2MaskAbs(uint3 p)
{
	return Get2x2x2MaskRel(p % 2);
}

#endif