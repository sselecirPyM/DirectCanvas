//file6
cbuffer DC_LayoutsData0 : register(b0)
{
	float4 DC_LayoutColor;
	int4 Parameters[8];
}
uniform StructuredBuffer<uint2> DC_Tiles : register(t0);
Texture2D<float4> RefTexture :register(t1);
RWTexture2D<float4> DC_Target : register(u0);

#define codehere

[numthreads(8, 8, 16)]
void main(uint3 id : SV_DispatchThreadID)
{
	uint DC_count;
	uint DC_stride;
	DC_Tiles.GetDimensions(DC_count, DC_stride);
	if (id.z < DC_count)
	{
		uint2 position = id.xy + DC_Tiles[id.z];
		DC_Target[position] = blend(DC_Target[position], DC_LayoutColor, position);
	}
}
//example
/*
float4 blend(float4 bufferColor, float4 layoutColor, uint2 position)
{
	float4 color = bufferColor;
	return float4(color.rgb*(1 - layoutColor.a) + layoutColor.rgb*layoutColor.a,
		saturate(1 - (1 - layoutColor.a)*(1 - color.a)));
}
*/