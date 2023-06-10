// This describes a vertex in local terms
struct LocalVertex
{
	float3 positionOS; // position in object space
	float2 uv; // UV
	float4 color;// Vertex Color
};

// This describes a vertex on the generated mesh
struct GlobalVertex
{
	float3 positionWS; // position in world space
	float2 uv; // UV
	float4 color;//Vertex color
};

// We have to insert three draw vertices at once so the triangle stays connected
// in the graphics shader. This structure does that
struct DrawTriangle
{
	float3 normalWS; // normal in world space. All points share this normal
	//GlobalVertex vertices[3];
	GlobalVertex vertex0;
	GlobalVertex vertex1;
	GlobalVertex vertex2;
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
AppendStructuredBuffer<DrawTriangle> _DrawTriangles;

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

// This Creates a new local vertex struct using the inputs
LocalVertex NewLocalVertex(float3 position, float2 uv, float4 color)
{
	LocalVertex l;
	l.positionOS = position;
	l.uv = uv;
	l.color = color;
	return l;
}

// This converts a Local vertex into a Global vertex by converting from OS to WS
float3 TransformToWorldSpaceF3(float3 localPosition)
{
	float3 globalPosition;
	globalPosition = mul(_LocalToWorld, float4(localPosition, 1)).xyz;
	return globalPosition * _Scale;
}

// This converts a Local vertex into a Global vertex by converting from OS to WS
GlobalVertex TransformToWorldSpace(LocalVertex v)
{
	GlobalVertex o;
	o.positionWS = TransformToWorldSpaceF3(v.positionOS);
	o.uv = v.uv;
	o.color = v.color;
	return o;	
}

// This Creates a new local vertex struct using the inputs
GlobalVertex NewGlobalVertex(float3 positionLS, float2 uv, float4 color)
{
	GlobalVertex o;
	o.positionWS = positionLS;
	o.uv = uv;
	o.color = color;
	return o;
}

// This Creates a new local vertex struct using the inputs
GlobalVertex NewGlobalVertexFromLocal(float3 positionLS, float2 uv, float4 color)
{
	return TransformToWorldSpace(NewLocalVertex(positionLS, uv, color));
}

DrawTriangle GetDrawTriangle(GlobalVertex a, GlobalVertex b, GlobalVertex c)
{
	// Since we extrude the center face, the normal must be recalculated
	float3 normalWS = GetNormalFromTriangle(a.positionWS, b.positionWS, c.positionWS);

    // Create a draw triangle from three points
	DrawTriangle tri;
	tri.normalWS = normalWS;
	tri.vertex0 = a;
	tri.vertex1 = b;
	tri.vertex2 = c;
	
	return tri;
}

void AddTriangle(DrawTriangle tri)
{
	_DrawTriangles.Append(tri);
}

void AddTriangle(GlobalVertex a, GlobalVertex b, GlobalVertex c)
{
	AddTriangle(GetDrawTriangle(a, b, c));

}

void AddCubeToDrawBuffer(float3 center, float3 size, float4 color)
{
	float3 centeredOffset = float3(-0.5f * size.x, -0.5f * size.y, -0.5f * size.z) + center;
	
	float3 corners[8];
	corners[0] = float3(0, 0, 0) + centeredOffset;
	corners[1] = float3(0, 0, size.z) + centeredOffset;
	corners[2] = float3(size.x, 0, size.z) + centeredOffset;
	corners[3] = float3(size.x, 0, 0) + centeredOffset;

	corners[4] = float3(0, size.y, 0) + centeredOffset;
	corners[5] = float3(0, size.y, size.z) + centeredOffset;
	corners[6] = float3(size.x, size.y, size.z) + centeredOffset;
	corners[7] = float3(size.x, size.y, 0) + centeredOffset;
	
	GlobalVertex inputs[24]; //4 corners and 6 sides
	
	//Top
	inputs[0] = NewGlobalVertex(corners[4], float2(0, 0), color);
	inputs[1] = NewGlobalVertex(corners[5], float2(0, 1), color);
	inputs[2] = NewGlobalVertex(corners[6], float2(1, 1), color);
	inputs[3] = NewGlobalVertex(corners[7], float2(1, 0), color);
	
	//Bottom
	inputs[4] = NewGlobalVertex(corners[3], float2(0, 0), color);
	inputs[5] = NewGlobalVertex(corners[2], float2(0, 1), color);
	inputs[6] = NewGlobalVertex(corners[1], float2(1, 1), color);
	inputs[7] = NewGlobalVertex(corners[0], float2(1, 0), color);
	
	//Left
	inputs[8] = NewGlobalVertex(corners[1], float2(0, 0), color);
	inputs[9] = NewGlobalVertex(corners[5], float2(0, 1), color);
	inputs[10] = NewGlobalVertex(corners[4], float2(1, 1), color);
	inputs[11] = NewGlobalVertex(corners[0], float2(1, 0), color);
	
	//Right
	inputs[12] = NewGlobalVertex(corners[3], float2(0, 0), color);
	inputs[13] = NewGlobalVertex(corners[7], float2(0, 1), color);
	inputs[14] = NewGlobalVertex(corners[6], float2(1, 1), color);
	inputs[15] = NewGlobalVertex(corners[2], float2(1, 0), color);
	
	//Forawrd
	inputs[16] = NewGlobalVertex(corners[2], float2(0, 0), color);
	inputs[17] = NewGlobalVertex(corners[6], float2(0, 1), color);
	inputs[18] = NewGlobalVertex(corners[5], float2(1, 1), color);
	inputs[19] = NewGlobalVertex(corners[1], float2(1, 0), color);
	
	//Back
	inputs[20] = NewGlobalVertex(corners[0], float2(0, 0), color);
	inputs[21] = NewGlobalVertex(corners[4], float2(0, 1), color);
	inputs[22] = NewGlobalVertex(corners[7], float2(1, 1), color);
	inputs[23] = NewGlobalVertex(corners[3], float2(1, 0), color);
	

	for (int i = 0; i < 6; i++)
	{
		AddTriangle(inputs[i * 4], inputs[i * 4 + 1], inputs[i * 4 + 2]);
		AddTriangle(inputs[i * 4], inputs[i * 4 + 2], inputs[i * 4 + 3]);
	}
}

// Multiply the number of vertices by three to convert from triangles
[numthreads(1, 1, 1)]
void TriToVertCount(uint3 id : SV_DispatchThreadID)
{
	_IndirectArgsBuffer[0].numVerticesPerInstance *= 3;
}