using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(VoxelMesh))]
public class VoxelChunk : MonoBehaviour
{
    public int chunkSize;
    public Vector3Int chunkPosition;

    public VoxelMesh mesh;

    public float LastLoadTime
    {
        get
        {
            return mesh.lastLoadTime;
        }
    }

    public int VertexCount
    {
        get
        {
            return mesh.vertexCount;
        }
    }

    private void Start()
    {
        VoxelDataCollection.UpdateCollectionsDict();
        TextureDataCollection.UpdateCollectionsDict();

        //UpdatePosition();

        //UpdateMesh();
        
    }

    public void UpdatePosition()
    {
        transform.position = chunkPosition * chunkSize;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 position = chunkPosition * chunkSize;
        Vector3 scale = Vector3.one * chunkSize;

        Gizmos.DrawWireCube(position + scale / 2f, scale);
    }

    private void SetSampleVoxels()
    {
        mesh.states.BoxFill(0, 0, 0, chunkSize - 1, chunkSize - 1, chunkSize - 1, "base:air");

        int realX, realY, realZ;

        List<Vector3Int> positions = new List<Vector3Int>();

        for (int x = 0; x < chunkSize; x++)
        {
            realX = x + chunkPosition.x * chunkSize;
            for (int y = 0; y < chunkSize; y++)
            {
                realY = y + chunkSize * chunkPosition.y;
                for (int z = 0; z < chunkSize; z++)
                {
                    realZ = z + chunkSize * chunkPosition.z;

                    float sx = Mathf.Sin(realX / 10f);
                    float sz = Mathf.Sin(realZ / 10f);

                    int sxint = Mathf.RoundToInt(sx * 10 + 12f);
                    int szint = Mathf.RoundToInt(sz * 10 + 12f);

                    int min = Mathf.Min(sxint, szint);

                    if (realY < min)
                    {
                        //positions.Add(new(x, y, z));
                    }

                    float value = Random.value;

                    if(value < 0.8f)
                    {
                        positions.Add(new(x, y, z));
                    }
                }
            }
        }

        mesh.states.SetVoxels("minecraft:grass", positions);
    }

    public void UpdateMesh()
    {
        if(mesh == null)
        {
            mesh = GetComponent<VoxelMesh>();
        }

        mesh.UpdateMesh();
    }

    #region Editing

    public void SetVoxel(string idString, Vector3Int position, bool addNew = false)
    {
        mesh.states.SetVoxel(idString, position, addNew);
    }

    public void SetVoxel(string idString, Vector3Int position, out bool changed, bool addNew = false)
    {
        mesh.states.SetVoxel(idString, position, out changed, addNew);
    }

    #endregion
}
