// Morder Order codec functions. 
// Derived from https://github.com/Forceflow/libmorton/
// Licence: MIT

/// <summary>
/// Morton 3D Magicbits Encode
/// Usage: uint = morton3D_Magicbits_Encode(uint X, uint Y, uint Z)
/// </summary>
inline uint morton3D_SplitBy3bits(uint a) 
{
	uint x = (a) & 0x000003ff;
	x = (x | x << 16) & 0x30000ff;
	x = (x | x << 8)  & 0x0300f00f;
	x = (x | x << 4)  & 0x30c30c3;
	x = (x | x << 2)  & 0x9249249;
	return x;
}
inline uint morton3D_Magicbits_Encode(uint x, uint y, uint z) {
	return morton3D_SplitBy3bits(x) | (morton3D_SplitBy3bits(y) << 1) | (morton3D_SplitBy3bits(z) << 2);
}

/// <summary>
/// Morton 3D Magicbits Decode
/// Usage: uint3 = morton3D_Magicbits_Decode(uint MortonIndex)
/// </summary>
inline uint morton3D_GetThirdBits(uint m)
{
	uint x = m & 0x9249249;
	x = (x ^ (x >> 2)) & 0x30c30c3;
	x = (x ^ (x >> 4)) & 0x0300f00f;
	x = (x ^ (x >> 8)) & 0x30000ff;
	x = (x ^ (x >> 16)) & 0x000003ff;
	return x;
}
inline uint3 morton3D_Magicbits_Decode(uint morton)
{
	return uint3(morton3D_GetThirdBits(morton), morton3D_GetThirdBits(morton >> 1), morton3D_GetThirdBits(morton >> 2));
}