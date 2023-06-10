// Make sure this file is not included twice
#ifndef VOXEL_COMPUTE_HELPERS_INCLUDED
#define VOXEL_COMPUTE_HELPERS_INCLUDED

int XWidth = 10;
int YWidth = 10;
int ZWidth = 10;

float Voxel_Size = 1.0f;

float UseTextureIndices = 0.0f;

bool visited[256];

//Struct that holds all voxel face info
struct VoxelFace
{
	float3 vertex0;
	float3 vertex1;
	float3 vertex2;
	float3 vertex3;

	float4 uv0;
	float4 uv1;
	float4 uv2;
	float4 uv3;

	float3 normal;
};

struct SliceStartEnd
{
	int sliceIndex; //Which slice is this
	int sideIndex; //Which side of the voxel is this face within
	
	int2 start;
	int2 end;
};

//An array of enabled voxels
StructuredBuffer<float> Enabled_Voxels_Buffer;

//An array of outer enabled voxels
StructuredBuffer<float> Outer_Enabled_Voxels_Buffer;

//An array of enabled voxels
StructuredBuffer<int> Texture_Index_Buffer;

//Animation Info
StructuredBuffer<int> Animation_Info_Buffer;

//Is outer voxel enabled
bool GetOuter(int3 relativeCoordinate, uint sideIndex)
{
	sideIndex %= 6;
	
	int startingIndex = 0;

	switch (sideIndex)
	{
		default:
		case 0: //Top x & z
			startingIndex = 0;
			break;
		case 1: //Bottom
			startingIndex = XWidth * ZWidth;
			break;
		case 2: //Left y & z
			startingIndex = XWidth * ZWidth * 2;
			break;
		case 3: //Right
			startingIndex = XWidth * ZWidth * 2 + YWidth * ZWidth;
			break;
		case 4: //Forward x & y
			startingIndex = XWidth * ZWidth * 2 + YWidth * ZWidth * 2;
			break;
		case 5: //Back
			startingIndex = XWidth * ZWidth * 2 + YWidth * ZWidth * 2 + XWidth * YWidth;
			break;
	}
	
	int index = startingIndex;
	
	//Top or bottom
	if (sideIndex == 0 || sideIndex == 1)
	{
		index = startingIndex + relativeCoordinate.z * XWidth + relativeCoordinate.x;
	}
	//Left or right
	else if (sideIndex == 2 || sideIndex == 3)
	{
		index = startingIndex + relativeCoordinate.z * YWidth + relativeCoordinate.y;
	}
	//Forward or back
	else if (sideIndex == 4 || sideIndex == 5)
	{
		index = startingIndex + relativeCoordinate.y * XWidth + relativeCoordinate.x;
	}
	
	return Outer_Enabled_Voxels_Buffer[index] == 1.0f;
}

//Returns if the voxel at the coordinate is enabled
bool IsEnabled(int3 coordinate)
{
	bool withinBounds =
	coordinate.x >= 0 && coordinate.x < (int) XWidth &&
	coordinate.y >= 0 && coordinate.y < (int) YWidth &&
	coordinate.z >= 0 && coordinate.z < (int) ZWidth;
	
	if (withinBounds)
	{
		int index = coordinate.x + coordinate.y * XWidth + coordinate.z * XWidth * YWidth;
		return Enabled_Voxels_Buffer[index] == 1.0f;
	}
	
	//Top
	if (coordinate.y == (int) YWidth)
	{
		return GetOuter(coordinate, 0);
	}
	//Bottom
	else if (coordinate.y == -1)
	{
		return GetOuter(coordinate, 1);
	}
	//Left
	else if (coordinate.x == -1)
	{
		return GetOuter(coordinate, 2);
	}
	//Right
	else if (coordinate.x == (int) XWidth)
	{
		return GetOuter(coordinate, 3);
	}
	//Forward
	else if (coordinate.z == (int) ZWidth)
	{
		return GetOuter(coordinate, 4);
	}
	//Back
	else if (coordinate.z == -1)
	{
		return GetOuter(coordinate, 5);
	}
	
	return false;

}

//Returns true if the face of a given voxel is visible (0-up, 1-down, 2-left, 3-right, 4-forward, 5-back)
bool FaceVisible(int3 coordinate, uint sideIndex)
{
	sideIndex %= 6;
	
	if (!IsEnabled(coordinate))
	{
		return false;
	}

	//Up
	if (sideIndex == 0)
	{
		return !IsEnabled(coordinate + int3(0, 1, 0));
	}
	
	//Down
	else if (sideIndex == 1)
	{
		return !IsEnabled(coordinate + int3(0, -1, 0));
	}

	//Left
	else if (sideIndex == 2)
	{
		return !IsEnabled(coordinate + int3(-1, 0, 0));
	}

	//Right
	else if (sideIndex == 3)
	{
		return !IsEnabled(coordinate + int3(1, 0, 0));
	}

	//Forward
	else if (sideIndex == 4)
	{
		return !IsEnabled(coordinate + int3(0, 0, 1));
	}

	//Back
	else if (sideIndex == 5)
	{
		return !IsEnabled(coordinate + int3(0, 0, -1));
	}

	return false;
}

//Returns the texture index of a given face of the voxel at the coordinates
uint TextureIndex(int3 coordinate, uint sideIndex)
{
	sideIndex = sideIndex % 6;
	uint index = sideIndex + (coordinate.x + coordinate.y * XWidth + coordinate.z * XWidth * YWidth) * 6;
	return (uint) Texture_Index_Buffer[index];
}

//Returns the texture index of a given face of the voxel at the coordinates
uint AnimationData(int3 coordinate, uint sideIndex)
{
	sideIndex = sideIndex % 6;
	uint index = sideIndex + (coordinate.x + coordinate.y * XWidth + coordinate.z * XWidth * YWidth) * 6;
	return (uint) Animation_Info_Buffer[index];
}

//Gives a vector3/ float3 of the origin point of a given voxel
float3 Anchor(int3 coordinate)
{
	float3 anchor = float3(coordinate.x * Voxel_Size, coordinate.y * Voxel_Size, coordinate.z * Voxel_Size);

	return anchor;
}

//Gets all the respective corners of a voxel
void Corners(int3 coordinate, out float3 corners[8])
{
	//Bottom going clockwise facing down
	corners[0] = Anchor(coordinate);
	corners[1] = Anchor(coordinate + float3(0, 0, 1));
	corners[2] = Anchor(coordinate + float3(1, 0, 1));
	corners[3] = Anchor(coordinate + float3(1, 0, 0));

	//Top going clockwise facing down
	corners[4] = Anchor(coordinate + float3(0, 1, 0));
	corners[5] = Anchor(coordinate + float3(0, 1, 1));
	corners[6] = Anchor(coordinate + float3(1, 1, 1));
	corners[7] = Anchor(coordinate + float3(1, 1, 0));
}

bool Visited(uint index)
{
	return visited[index];
}

void GetVoxelFace(uint3 coordinate, uint faceIndex, float3 corners[8], bool useTextureIndices, out VoxelFace face)
{
	faceIndex %= 6;
	
	int tIndex = TextureIndex(coordinate, faceIndex);
	uint animationData = AnimationData(coordinate, faceIndex);

	if (useTextureIndices)
	{
		face.uv0 = float4(0, 0, tIndex, animationData);
		face.uv1 = float4(0, 1, tIndex, animationData);
		face.uv2 = float4(1, 1, tIndex, animationData);
		face.uv3 = float4(1, 0, tIndex, animationData);
	}
	else
	{
		face.uv0 = float4(0, 0, 0, 0);
		face.uv1 = float4(0, 1, 0, 0);
		face.uv2 = float4(1, 1, 0, 0);
		face.uv3 = float4(1, 0, 0, 0);
	}
	
	int rot = 0;
		
	if (rot > 0)
	{
		float4 uv0 = face.uv0;
		float4 uv1 = face.uv1;
		float4 uv2 = face.uv2;
		float4 uv3 = face.uv3;
			
		switch (rot)
		{
			default:
			case 1:
				face.uv0 = uv3;
				face.uv1 = uv0;
				face.uv2 = uv1;
				face.uv3 = uv2;
				break;
			case 2:
				face.uv0 = uv2;
				face.uv1 = uv3;
				face.uv2 = uv0;
				face.uv3 = uv1;
				break;
			case 3:
				face.uv0 = uv1;
				face.uv1 = uv2;
				face.uv2 = uv3;
				face.uv3 = uv0;
				break;
		}
	}
	
	//Up
	if (faceIndex == 0)
	{
		face.vertex0 = corners[4];
		face.vertex1 = corners[5];
		face.vertex2 = corners[6];
		face.vertex3 = corners[7];

		face.normal = float3(0, 1, 0);
	}
	//Down
	else if (faceIndex == 1)
	{
		face.vertex0 = corners[3];
		face.vertex1 = corners[2];
		face.vertex2 = corners[1];
		face.vertex3 = corners[0];

		face.normal = float3(0, -1, 0);
	}
	//Left
	else if (faceIndex == 2)
	{
		face.vertex0 = corners[1];
		face.vertex1 = corners[5];
		face.vertex2 = corners[4];
		face.vertex3 = corners[0];

		face.normal = float3(-1, 0, 0);
	}
	//Right
	else if (faceIndex == 3)
	{
		face.vertex0 = corners[3];
		face.vertex1 = corners[7];
		face.vertex2 = corners[6];
		face.vertex3 = corners[2];

		face.normal = float3(1, 0, 0);
	}
	//Forward
	else if (faceIndex == 4)
	{
		face.vertex0 = corners[2];
		face.vertex1 = corners[6];
		face.vertex2 = corners[5];
		face.vertex3 = corners[1];

		face.normal = float3(0, 0, 1);
	}
	//Back
	else
	{
		face.vertex0 = corners[0];
		face.vertex1 = corners[4];
		face.vertex2 = corners[7];
		face.vertex3 = corners[3];

		face.normal = float3(0, 0, -1);
	}
}

void GetGreedyVoxelFace(uint3 coordinate, uint faceIndex, bool useTextureIndices, out VoxelFace face)
{
	uint3 starting = coordinate;
	uint3 ending = coordinate;
	uint3 current = coordinate;
	
	uint startingTextureIndex = TextureIndex(coordinate, faceIndex);
	uint currentTextureIndex = TextureIndex(coordinate, faceIndex);
	
	uint startingAnimationData = AnimationData(coordinate, faceIndex);
	
	//Top or Bottom
	if (faceIndex == 0 || faceIndex == 1)
	{
		for (int x = starting.x + 1; x < XWidth; x++)
		{
			current = uint3(x, starting.y, starting.z);
			currentTextureIndex = TextureIndex(current, faceIndex);
			if (currentTextureIndex != startingTextureIndex || !IsEnabled(current) || !FaceVisible(current, faceIndex))
			{
				break;
			}
			ending = current;
		}
	}
	//Left or Right
	else if (faceIndex == 2 || faceIndex == 3)
	{
		for (int z = starting.z + 1; z < ZWidth; z++)
		{
			current = uint3(starting.x, starting.y, z);
			currentTextureIndex = TextureIndex(current, faceIndex);
			if (currentTextureIndex != startingTextureIndex || !IsEnabled(current) || !FaceVisible(current, faceIndex))
			{
				break;
			}
			ending = current;
		}
	}
	//Forward or Back
	else
	{
		for (int x = starting.x - 1; x >= 0; x--)
		{
			current = uint3(x, starting.y, starting.z);
			currentTextureIndex = TextureIndex(current, faceIndex);
			if (currentTextureIndex != startingTextureIndex || !IsEnabled(current) || !FaceVisible(current, faceIndex))
			{
				break;
			}
			ending = current;
		}
	}
	
	bool singleFace =
		starting.x == ending.x &&
		starting.y == ending.y &&
		starting.z == ending.z;
	
	if (singleFace)
	{
		float3 corners[8];
		Corners(coordinate, corners);
		GetVoxelFace(coordinate, faceIndex, corners, useTextureIndices, face);
		return;
	}
	
	int tIndex = TextureIndex(coordinate, faceIndex);
	
	float3 startingCorners[8];
	float3 endingCorners[8];
	
	Corners(starting, startingCorners);
	Corners(ending, endingCorners);
	
	face.vertex0 = startingCorners[4];
	face.vertex1 = startingCorners[5];
	
	face.vertex2 = endingCorners[6];
	face.vertex3 = endingCorners[7];
	
	face.uv0 = float4(0, 0, startingTextureIndex, startingAnimationData);
	face.uv1 = float4(0, 1, startingTextureIndex, startingAnimationData);
	face.uv2 = float4(ending.x - starting.x + 1, 1, startingTextureIndex, startingAnimationData);
	face.uv3 = float4(ending.x - starting.x + 1, 0, startingTextureIndex, startingAnimationData);
	
	face.normal = float3(0, 1, 0);
	
	//Top
	if (faceIndex == 0)
	{
		face.vertex0 = startingCorners[4];
		face.vertex1 = startingCorners[5];
	
		face.vertex2 = endingCorners[6];
		face.vertex3 = endingCorners[7];
	
		face.uv0 = float4(0, 0, startingTextureIndex, startingAnimationData);
		face.uv1 = float4(0, 1, startingTextureIndex, startingAnimationData);
		face.uv2 = float4(ending.x - starting.x + 1, 1, startingTextureIndex, startingAnimationData);
		face.uv3 = float4(ending.x - starting.x + 1, 0, startingTextureIndex, startingAnimationData);
	
		face.normal = float3(0, 1, 0);
	}
	//Bottom
	else if (faceIndex == 1)
	{
		face.vertex0 = endingCorners[3];
		face.vertex1 = endingCorners[2];
	
		face.vertex2 = startingCorners[1];
		face.vertex3 = startingCorners[0];
	
		face.uv0 = float4(0, 0, startingTextureIndex, startingAnimationData);
		face.uv1 = float4(0, 1, startingTextureIndex, startingAnimationData);
		face.uv2 = float4(ending.x - starting.x + 1, 1, startingTextureIndex, startingAnimationData);
		face.uv3 = float4(ending.x - starting.x + 1, 0, startingTextureIndex, startingAnimationData);
	
		face.normal = float3(0, -1, 0);
	}
	
	//Left
	else if (faceIndex == 2)
	{
		face.vertex0 = endingCorners[1];
		face.vertex1 = endingCorners[5];
	
		face.vertex2 = startingCorners[4];
		face.vertex3 = startingCorners[0];
	
		face.uv0 = float4(0, 0, startingTextureIndex, startingAnimationData);
		face.uv1 = float4(0, 1, startingTextureIndex, startingAnimationData);
		face.uv2 = float4(ending.z - starting.z + 1, 1, startingTextureIndex, startingAnimationData);
		face.uv3 = float4(ending.z - starting.z + 1, 0, startingTextureIndex, startingAnimationData);
	
		face.normal = float3(-1, 0, 0);
	}
	//Right
	else if (faceIndex == 3)
	{
		face.vertex0 = startingCorners[3];
		face.vertex1 = startingCorners[7];
	
		face.vertex2 = endingCorners[6];
		face.vertex3 = endingCorners[2];
	
		face.uv0 = float4(0, 0, startingTextureIndex, startingAnimationData);
		face.uv1 = float4(0, 1, startingTextureIndex, startingAnimationData);
		face.uv2 = float4(ending.z - starting.z + 1, 1, startingTextureIndex, startingAnimationData);
		face.uv3 = float4(ending.z - starting.z + 1, 0, startingTextureIndex, startingAnimationData);
	
		face.normal = float3(1, 0, 0);
	}
	//Forward
	else if (faceIndex == 4)
	{
		face.vertex0 = startingCorners[2];
		face.vertex1 = startingCorners[6];
	
		face.vertex2 = endingCorners[5];
		face.vertex3 = endingCorners[1];
	
		face.uv0 = float4(0, 0, startingTextureIndex, startingAnimationData);
		face.uv1 = float4(0, 1, startingTextureIndex, startingAnimationData);
		face.uv2 = float4(starting.x - ending.x + 1, 1, startingTextureIndex, startingAnimationData);
		face.uv3 = float4(starting.x - ending.x + 1, 0, startingTextureIndex, startingAnimationData);
	
		face.normal = float3(0, 0, 1);
	}
	//Back
	else
	{
		face.vertex0 = endingCorners[0];
		face.vertex1 = endingCorners[4];
	
		face.vertex2 = startingCorners[7];
		face.vertex3 = startingCorners[3];
	
		face.uv0 = float4(0, 0, startingTextureIndex, startingAnimationData);
		face.uv1 = float4(0, 1, startingTextureIndex, startingAnimationData);
		face.uv2 = float4(starting.x - ending.x + 1, 1, startingTextureIndex, startingAnimationData);
		face.uv3 = float4(starting.x - ending.x + 1, 0, startingTextureIndex, startingAnimationData);
	
		face.normal = float3(0, 0, -1);
	}
}

bool HasSameTextureInfo(int3 coordinate0, int3 coordinate1, uint sideIndex)
{
	return TextureIndex(coordinate0, sideIndex) == TextureIndex(coordinate1, sideIndex) &&
			AnimationData(coordinate0, sideIndex) == AnimationData(coordinate1, sideIndex);

}

bool LCheck(int3 anchor, uint size, uint sideIndex)
{
	sideIndex %= 6;
	
	int3 bottomRight = int3(anchor.x + size, anchor.y, anchor.z);
	int3 topLeft = int3(anchor.x, anchor.y, anchor.z + size);
	
	int visitedIndex = 0;
	
	if (sideIndex < 2)
	{
		bottomRight = int3(anchor.x + size, anchor.y, anchor.z);
		topLeft = int3(anchor.x, anchor.y, anchor.z + size);
		
		if (bottomRight.x >= XWidth || topLeft.z >= ZWidth)
		{
			return false;
		}
	}
	else if (sideIndex < 4)
	{
		bottomRight = int3(anchor.x, anchor.y, anchor.z + size);
		topLeft = int3(anchor.x, anchor.y + size, anchor.z);
		
		if (bottomRight.z >= ZWidth || topLeft.y >= YWidth)
		{
			return false;
		}
	}
	else
	{
		bottomRight = int3(anchor.x + size, anchor.y, anchor.z);
		topLeft = int3(anchor.x, anchor.y + size, anchor.z);
		
		if (bottomRight.x >= XWidth || topLeft.y >= YWidth)
		{
			return false;
		}
	}
	
	if (sideIndex < 2)
	{
		visitedIndex = bottomRight.x + bottomRight.z * XWidth;
	}
	else if (sideIndex < 4)
	{
		visitedIndex = bottomRight.z + bottomRight.y * ZWidth;
	}
	else
	{
		visitedIndex = bottomRight.x + bottomRight.y * XWidth;
	}

	
	if (Visited(visitedIndex) || !FaceVisible(bottomRight, sideIndex) || !HasSameTextureInfo(anchor, bottomRight, sideIndex))
	{
		return false;
	}
	
	if (sideIndex < 2)
	{
		visitedIndex = topLeft.x + topLeft.z * XWidth;
	}
	else if (sideIndex < 4)
	{
		visitedIndex = topLeft.z + topLeft.y * ZWidth;
	}
	else
	{
		visitedIndex = topLeft.x + topLeft.y * XWidth;
	}
	
	if (Visited(visitedIndex) || !FaceVisible(topLeft, sideIndex) || !HasSameTextureInfo(anchor, topLeft, sideIndex))
	{
		return false;
	}
	
	int xOffset, yOffset, zOffset;
	
	int3 currentCheck = bottomRight;
	
	if (sideIndex < 2)
	{
		for (zOffset = 1; zOffset <= size; zOffset++)
		{
			currentCheck = int3(bottomRight.x, bottomRight.y, bottomRight.z + zOffset);
			visitedIndex = currentCheck.x + currentCheck.z * XWidth;
			if (Visited(visitedIndex) || !FaceVisible(currentCheck, sideIndex) || !HasSameTextureInfo(anchor, currentCheck, sideIndex))
			{
				return false;
			}
		}
		
		for (xOffset = 1; xOffset < size; xOffset++)
		{
			currentCheck = int3(bottomRight.x - xOffset, bottomRight.y, bottomRight.z + size);
			visitedIndex = currentCheck.x + currentCheck.z * XWidth;
			if (Visited(visitedIndex) || !FaceVisible(currentCheck, sideIndex) || !HasSameTextureInfo(anchor, currentCheck, sideIndex))
			{
				return false;
			}
		}
	}
	else if (sideIndex < 4)
	{
		for (yOffset = 1; yOffset <= size; yOffset++)
		{
			currentCheck = int3(bottomRight.x, bottomRight.y + yOffset, bottomRight.z);
			visitedIndex = currentCheck.z + currentCheck.y * ZWidth;
			if (Visited(visitedIndex) || !FaceVisible(currentCheck, sideIndex) || !HasSameTextureInfo(anchor, currentCheck, sideIndex))
			{
				return false;
			}
		}
		
		for (zOffset = 1; zOffset < size; zOffset++)
		{
			currentCheck = int3(bottomRight.x, bottomRight.y + size, bottomRight.z - zOffset);
			visitedIndex = currentCheck.z + currentCheck.y * ZWidth;
			if (Visited(visitedIndex) || !FaceVisible(currentCheck, sideIndex) || !HasSameTextureInfo(anchor, currentCheck, sideIndex))
			{
				return false;
			}
		}
	}
	else
	{
		for (yOffset = 1; yOffset <= size; yOffset++)
		{
			currentCheck = int3(bottomRight.x, bottomRight.y + yOffset, bottomRight.z);
			visitedIndex = currentCheck.x + currentCheck.y * XWidth;
			if (Visited(visitedIndex) || !FaceVisible(currentCheck, sideIndex) || !HasSameTextureInfo(anchor, currentCheck, sideIndex))
			{
				return false;
			}
		}
		
		for (xOffset = 1; xOffset < size; xOffset++)
		{
			currentCheck = int3(bottomRight.x - xOffset, bottomRight.y + size, bottomRight.z);
			visitedIndex = currentCheck.x + currentCheck.y * XWidth;
			if (Visited(visitedIndex) || !FaceVisible(currentCheck, sideIndex) || !HasSameTextureInfo(anchor, currentCheck, sideIndex))
			{
				return false;
			}
		}
	}
	
	return true;
}

bool UpCheck(int3 anchor, uint offset, uint length, uint sideIndex)
{
	int3 upCoord = int3(anchor.x, anchor.y, anchor.z + offset);
	
	if (sideIndex < 2)
	{
		upCoord = int3(anchor.x, anchor.y, anchor.z + offset);
	}
	else if (sideIndex < 4)
	{
		upCoord = int3(anchor.x, anchor.y + offset, anchor.z);
	}
	else
	{
		upCoord = int3(anchor.x, anchor.y + offset, anchor.z);
	}
	
	
	int xOffset = 0;
	
	if (sideIndex < 2)
	{
		for (xOffset = 0; xOffset <= length; xOffset++)
		{
			upCoord = int3(anchor.x - xOffset, anchor.y, anchor.z + offset);
		
			if (upCoord.z >= ZWidth || upCoord.x < 0 || Visited(upCoord.x + upCoord.z * XWidth) || !FaceVisible(upCoord, sideIndex) || !HasSameTextureInfo(anchor, upCoord, sideIndex))
			{
				return false;
			}
		}
	}
	else if (sideIndex < 4)
	{
		for (int zOffset = 0; zOffset <= length; zOffset++)
		{
			upCoord = int3(anchor.x, anchor.y + offset, anchor.z - zOffset);
		
			if (upCoord.y >= YWidth || upCoord.z < 0 || Visited(upCoord.z + upCoord.y * ZWidth) || !FaceVisible(upCoord, sideIndex) || !HasSameTextureInfo(anchor, upCoord, sideIndex))
			{
				return false;
			}
		}
	}
	else
	{
		for (xOffset = 0; xOffset <= length; xOffset++)
		{
			upCoord = int3(anchor.x - xOffset, anchor.y + offset, anchor.z);
		
			if (upCoord.y >= YWidth || upCoord.x < 0 || Visited(upCoord.x + upCoord.y * XWidth) || !FaceVisible(upCoord, sideIndex) || !HasSameTextureInfo(anchor, upCoord, sideIndex))
			{
				return false;
			}
		}
	}
	
	return true;
}

bool RightCheck(int3 anchor, uint offset, uint length, uint sideIndex)
{
	int3 rightCoord = int3(anchor.x + offset, anchor.y, anchor.z);
	
	if (sideIndex < 2)
	{
		rightCoord = int3(anchor.x + offset, anchor.y, anchor.z);
	}
	else if (sideIndex < 4)
	{
		rightCoord = int3(anchor.x, anchor.y, anchor.z + offset);
	}
	else
	{
		rightCoord = int3(anchor.x + offset, anchor.y, anchor.z);
	}
	
	int yOffset = 0;
	
	if (sideIndex < 2)
	{
		for (int zOffset = 0; zOffset <= length; zOffset++)
		{
			rightCoord = int3(anchor.x + offset, anchor.y, anchor.z - zOffset);
		
			if (rightCoord.x >= XWidth || rightCoord.z < 0 || Visited(rightCoord.x + rightCoord.z * XWidth) || !FaceVisible(rightCoord, sideIndex) || !HasSameTextureInfo(anchor, rightCoord, sideIndex))
			{
				return false;
			}
		}
	}
	else if (sideIndex < 4)
	{
		for (yOffset = 0; yOffset <= length; yOffset++)
		{
			rightCoord = int3(anchor.x, anchor.y - yOffset, anchor.z + offset);
		
			if (rightCoord.z >= ZWidth || rightCoord.y < 0 || Visited(rightCoord.z + rightCoord.y * ZWidth) || !FaceVisible(rightCoord, sideIndex) || !HasSameTextureInfo(anchor, rightCoord, sideIndex))
			{
				return false;
			}
		}
	}
	else
	{
		for (yOffset = 0; yOffset <= length; yOffset++)
		{
			rightCoord = int3(anchor.x + offset, anchor.y - yOffset, anchor.z);
		
			if (rightCoord.x >= XWidth || rightCoord.y < 0 || Visited(rightCoord.x + rightCoord.y * XWidth) || !FaceVisible(rightCoord, sideIndex) || !HasSameTextureInfo(anchor, rightCoord, sideIndex))
			{
				return false;
			}
		}
	}
	
	return true;
}

void GetVoxelFaceStartEnd(uint3 start, uint3 end, uint faceIndex, out VoxelFace face)
{
	faceIndex %= 6;
	
	bool useTextureIndices = UseTextureIndices == 1.0f;
	int tIndex = TextureIndex(start, faceIndex);
	uint animationData = AnimationData(end, faceIndex);
	
	int xDist = end.x - start.x;
	int yDist = end.y - start.y;
	int zDist = end.z - start.z;
	
	int xDist2D, yDist2D;
	
	xDist2D = xDist;
	yDist2D = zDist;
	
	if (faceIndex < 2)
	{
		xDist2D = xDist;
		yDist2D = zDist;
	}
	else if (faceIndex < 4)
	{
		xDist2D = zDist;
		yDist2D = yDist;
	}
	else
	{
		xDist2D = xDist;
		yDist2D = yDist;
	}
	
	float3 startingCorners[8];
	float3 endingCorners[8];
	
	Corners(start, startingCorners);
	Corners(end, endingCorners);
	
	face.vertex0 = startingCorners[4];
	face.vertex1 = float3(startingCorners[5].x, startingCorners[5].y, endingCorners[5].z);
	face.vertex2 = endingCorners[6];
	face.vertex3 = float3(endingCorners[7].x, startingCorners[7].y, startingCorners[7].z);
	
	face.normal = float3(0, 1, 0);
	
	switch (faceIndex)
	{
		default:
			break;
		case 0:
			face.vertex0 = startingCorners[4];
			face.vertex1 = float3(startingCorners[5].x, startingCorners[5].y, endingCorners[5].z);
			face.vertex2 = endingCorners[6];
			face.vertex3 = float3(endingCorners[7].x, startingCorners[7].y, startingCorners[7].z);
		
			face.normal = float3(0, 1, 0);
			break;
		case 1:
			face.vertex0 = float3(endingCorners[3].x, startingCorners[3].y, startingCorners[3].z);
			face.vertex1 = endingCorners[2];
			face.vertex2 = float3(startingCorners[1].x, startingCorners[1].y, endingCorners[1].z);
			face.vertex3 = startingCorners[0];
		
			face.normal = float3(0, -1, 0);
			break;
		case 2:
			face.vertex0 = float3(startingCorners[1].x, startingCorners[1].y, endingCorners[1].z);
			face.vertex1 = endingCorners[5];
			face.vertex2 = float3(startingCorners[4].x, endingCorners[4].y, startingCorners[4].z);
			face.vertex3 = startingCorners[0];
		
			face.normal = float3(-1, 0, 0);
			break;
		case 3:
			face.vertex0 = startingCorners[3];
			face.vertex1 = float3(startingCorners[7].x, endingCorners[7].y, startingCorners[7].z);
			face.vertex2 = endingCorners[6];
			face.vertex3 = float3(endingCorners[2].x, startingCorners[2].y, endingCorners[2].z);
		
			face.normal = float3(1, 0, 0);
			break;
		case 4: //2651
			face.vertex0 = float3(endingCorners[2].x, startingCorners[2].y, startingCorners[2].z);
			face.vertex1 = endingCorners[6];
			face.vertex2 = float3(startingCorners[5].x, endingCorners[5].y, startingCorners[5].z);
			face.vertex3 = startingCorners[1];
		
			face.normal = float3(0, 0, 1);
			break;
		case 5: //0473
			face.vertex0 = startingCorners[0];
			face.vertex1 = float3(startingCorners[4].x, endingCorners[4].y, startingCorners[4].z);
			face.vertex2 = endingCorners[7];
			face.vertex3 = float3(endingCorners[3].x, startingCorners[3].y, startingCorners[3].z);
		
			face.normal = float3(0, 0, -1);
			break;
	}

	if (useTextureIndices)
	{
		face.uv0 = float4(0, 0, tIndex, animationData);
		face.uv1 = float4(0, yDist2D + 1, tIndex, animationData);
		face.uv2 = float4(xDist2D + 1, yDist2D + 1, tIndex, animationData);
		face.uv3 = float4(xDist2D + 1, 0, tIndex, animationData);
	}
	else
	{
		face.uv0 = float4(0, 0, 0, 0);
		face.uv1 = float4(0, yDist2D + 1, 0, 0);
		face.uv2 = float4(xDist2D + 1, yDist2D + 1, 0, 0);
		face.uv3 = float4(xDist2D + 1, 0, 0, 0);
	}
}

#endif