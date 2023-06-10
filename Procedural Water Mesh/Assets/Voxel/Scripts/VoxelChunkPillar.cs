using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VoxelChunkPillar : MonoBehaviour
{
    [Header("Debug Info")]
    public int totalVertexCount;
    public float totalLoadTime = 0;

    [Header("Sizing")]
    /// <summary>
    /// How big each chunk is
    /// </summary>
    public int chunkSize;
    /// <summary>
    /// How many chunks tall this pillar is
    /// </summary>
    public int height;

    public Vector2Int position;

    public VoxelChunk[] chunks;

    public VoxelChunk chunkPrefab;

    private void Start()
    {
        VoxelDataCollection.UpdateCollectionsDict();
        TextureDataCollection.UpdateCollectionsDict();
    }

    public void UpdatePosition()
    {
        transform.position =  new(position.x * chunkSize, 0, position.y * chunkSize);
    }

    public void CreatePillar()
    {
        chunks = new VoxelChunk[height];
    }

    public VoxelMesh CreateChunk(int index)
    {
        var voxelChunk = Instantiate(chunkPrefab);

        voxelChunk.chunkSize = chunkSize;
        voxelChunk.chunkPosition = new Vector3Int(position.x, index, position.y);
        voxelChunk.UpdatePosition();
        //voxelChunk.UpdateMesh();
        voxelChunk.transform.parent = transform;

        voxelChunk.mesh.width = chunkSize;
        voxelChunk.mesh.InitializeStatesArray();
        voxelChunk.mesh.states.palette.AddVoxel("base:air");
        
        voxelChunk.mesh.states.BoxFill(0, 0, 0, chunkSize - 1, chunkSize - 1, chunkSize - 1, "base:air");

        chunks[index] = voxelChunk;

        totalVertexCount += voxelChunk.VertexCount;
        totalLoadTime += voxelChunk.LastLoadTime;

        return voxelChunk.mesh;
    }

}
