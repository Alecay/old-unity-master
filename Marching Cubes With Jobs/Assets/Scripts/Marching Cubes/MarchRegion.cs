using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MarchRegion : MonoBehaviour
{
    public Vector2Int position;

    public float voxelScale = 0.25f;

    public int chunkSize = 16;
    public int chunkHeight = 16;
    public int regionSize = 16;

    public MarchingCubesGPUMesh chunkPrefab;

    public bool setMeshSimplificationLevel = false;

    //public VoxelMeshGenerator meshGenerator;
    //public VoxelDensityGenerator densityGenerator;

    private void Start()
    {
        UpdatePosition();
        StartCoroutine(CreateChunksCo());
    }

    IEnumerator CreateChunksCo()
    {
        var timer = new System.Diagnostics.Stopwatch();
        timer.Start();

        //pillars = new VoxelChunkPillar[regionSize * regionSize];        

        for (int x = 0; x < regionSize; x++)
        {
            for (int y = 0; y < regionSize; y++)
            {
                CreateChunk(new(x, y));
                yield return null;
            }
        }

        //chunkPrefab.gameObject.SetActive(false);

        print("Finished creating pillars in " + timer.ElapsedMilliseconds + " mms");
    }

    private void CreateChunk(Vector2Int position)
    {
        var chunk = Instantiate(chunkPrefab);

        chunk.width = chunkSize;
        chunk.height = chunkHeight;

        chunk.meshSimplificationLevel = 0;

        if (setMeshSimplificationLevel)
        {
            if (position.x < regionSize / 2 - 3 || position.x > regionSize - regionSize / 2 + 3)
            {
                chunk.meshSimplificationLevel = 1;
            }

            if (position.x < regionSize / 8 || position.x > regionSize - regionSize / 8 - 1)
            {
                chunk.meshSimplificationLevel = 2;
            }
        }

        chunk.position = position;
        chunk.UpdatePosition();        
        chunk.transform.parent = transform;

        chunk.UpdateMesh();
    }

    public void UpdatePosition()
    {
        transform.position = new Vector3(position.x, 0, position.y) * chunkSize * regionSize * voxelScale;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;

        Vector3 size = new Vector3(regionSize, 0, regionSize) * chunkSize * voxelScale;
        Vector3 center = new Vector3(position.x, 0, position.y) * chunkSize * regionSize * voxelScale + size / 2f;

        Gizmos.DrawWireCube(center, size);
    }
}
