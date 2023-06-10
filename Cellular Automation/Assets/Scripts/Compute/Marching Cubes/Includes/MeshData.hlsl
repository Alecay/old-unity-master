
struct Triangle
{
	float3 vertex0;
	float3 vertex1;
	float3 vertex2;
	
	float3 normal;
	
	float2 uv0;
	float2 uv1;
	float2 uv2;
	
	float4 color0;
	float4 color1;
	float4 color2;	
};

// Returns the normal of a plane containing the triangle defined by the three arguments
float3 GetNormalFromTriangle(float3 a, float3 b, float3 c)
{
	return normalize(cross(b - a, c - a));
}

// Returns the center point of a triangle defined by the three arguments
float3 GetTriangleCenter(float3 a, float3 b, float3 c)
{
	return (a + b + c) / 3.0;
}
float2 GetTriangleCenter(float2 a, float2 b, float2 c)
{
	return (a + b + c) / 3.0;
}

// Compute buffers
AppendStructuredBuffer<Triangle> _Triangles;

float3 _Scale;
float4x4 _LocalToWorld;

RWStructuredBuffer<float3> _BoundsBuffer;

struct IndirectArgs
{
	uint numVerticesPerInstance;
	uint numInstances;
	uint startVertexIndex;
	uint startInstanceIndex;
};
RWStructuredBuffer<IndirectArgs> _IndirectArgsBuffer;

void GetTriangle(float3 vertex0, float3 vertex1, float3 vertex2,
				float2 uv0, float2 uv1, float2 uv2,
				float4 color0, float4 color1, float4 color2)
{
	Triangle tri;
	
	tri.vertex0 = vertex0;
	tri.vertex1 = vertex1;
	tri.vertex1 = vertex1;
	
	tri.uv0 = uv0;
	tri.uv1 = uv1;
	tri.uv2 = uv2;
	
	tri.color0 = color0;
	tri.color1 = color1;
	tri.color2 = color2;
	
	tri.normal = GetNormalFromTriangle(vertex0, vertex1, vertex2);
}

void AddTriangleToBuffer(Triangle tri)
{
	_Triangles.Append(tri);
}

// Multiply the number of vertices by three to convert from triangles
[numthreads(1, 1, 1)]
void TriToVertCount(uint3 id : SV_DispatchThreadID)
{
	_IndirectArgsBuffer[0].numVerticesPerInstance *= 3;
}