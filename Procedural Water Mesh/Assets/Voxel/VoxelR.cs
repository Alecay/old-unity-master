using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VoxelR : MonoBehaviour
{
    public Vector2Int position;

    public float voxelScale = 0.25f;

    public int chunkSize = 16;
    public int chunkHeight = 16;
    public int regionSize = 16;

    public VoxelP pillarPrefab;

    public VoxelMeshGenerator meshGenerator;
    public VoxelDensityGenerator densityGenerator;

    private void Start()
    {
        UpdatePosition();
        StartCoroutine(CreatePillarsCo());
    }

    IEnumerator CreatePillarsCo()
    {
        var timer = new System.Diagnostics.Stopwatch();
        timer.Start();

        //pillars = new VoxelChunkPillar[regionSize * regionSize];        

        for (int x = 0; x < regionSize; x++)
        {
            for (int y = 0; y < regionSize; y++)
            {
                CreatePillar(new(x, y));
                yield return null;
            }
        }

        pillarPrefab.gameObject.SetActive(false);

        print("Finished creating pillars in " + timer.ElapsedMilliseconds + " mms");

        densityGenerator.StartProcessingDensityRequests();

        while(false && densityGenerator.TotalRequests > 0)
        {
            yield return null;
        }

        meshGenerator.StartProcessingMeshRequests();

        //StartCoroutine(UpdatePillarsCo());
    }

    private void CreatePillar(Vector2Int position)
    {        
        var pillar = Instantiate(pillarPrefab);
        pillar.ClearMesh();
        //pillar.Hide();

        pillar.width = chunkSize;
        pillar.height = chunkHeight;

        pillar.levelOfDetail = 3;

        if(position.x < regionSize / 2 || position.x > regionSize - regionSize / 2)
        {
            pillar.levelOfDetail = 2;
        }

        if (position.x < regionSize / 4 || position.x > regionSize - regionSize / 4)
        {
            pillar.levelOfDetail = 1;
        }

        if (position.x >= regionSize / 2 - 4 && position.x <= regionSize / 2 + 4)
        {
            pillar.levelOfDetail = 3;
        }

        //if(position.x < regionSize / 4)
        //{
        //    pillar.levelOfDetail = 1;
        //}
        //else if (position.x < regionSize / 2)
        //{
        //    pillar.levelOfDetail = 2;
        //}
        //else if (position.x > regionSize - regionSize / 4)
        //{
        //    pillar.levelOfDetail = 2;
        //}
        //else if (position.x > regionSize - regionSize / 2)
        //{
        //    pillar.levelOfDetail = 1;
        //}

        pillar.position = position + this.position * regionSize;
        pillar.UpdatePosition();
        pillar.transform.parent = transform;        

        pillar.meshGenerator = meshGenerator;
        pillar.densityGenerator = densityGenerator;

        densityGenerator.RequestDensityValues(new VoxelDensityGenerator.DensityRequest(pillar));

        //pillars[position.x + position.y * regionSize] = pillar;
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
