// Morder Order codec functions. 
// Derived from https://github.com/Forceflow/libmorton/

/// Morton 2D Magicbit Encode
// Usage: uint = morton2D_MagicBits_Encode(uint X, uint Y)
inline uint morton2D_SplitBy2Bits(uint x) 
{
	x = (x | x << 16) & 0x0000FFFF;
	x = (x | x << 8) & 0x00FF00FF;
	x = (x | x << 4) & 0x0F0F0F0F;
	x = (x | x << 2) & 0x33333333;
	x = (x | x << 1) & 0x55555555;
	return x;
}
inline uint morton2D_MagicBits_Encode(uint x, uint y) 
{
	return morton2D_SplitBy2Bits(x) | (morton2D_SplitBy2Bits(y) << 1);
}

/// Morton 2D Magicbit Decode
// Usage: uint2 = morton2D_MagicBits_Decode(uint MortonIndex)
static inline uint morton2D_GetSecondBits(uint morton) {
	uint x = morton & 0x55555555;
	x = (x ^ (x >> 1)) & 0x33333333;
	x = (x ^ (x >> 2)) & 0x0F0F0F0F;
	x = (x ^ (x >> 4)) & 0x00FF00FF;
	x = (x ^ (x >> 8)) & 0x0000FFFF;
	return x;
}
inline uint2 morton2D_MagicBits_Decode(uint morton)
{
	return uint2(morton2D_GetSecondBits(morton), morton2D_GetSecondBits(morton >> 1));
}