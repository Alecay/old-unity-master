using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MarchRegion : MonoBehaviour
{
    [Space(10)]
    [Header("Region Variables")]
    [Space(5)]

    [Range(1, 32)]
    public int size;
    public Vector3Int offset;

    public int NumChunks
    {
        get
        {
            return size * size * size;
        }
    }

    [Space(10)]
    [Header("Chunk Variables")]
    [Space(5)]

    public float voxelScale = 1f;

    [Range(1, 32)]
    public int chunkSize;

    [Space(10)]
    [Header("References")]
    [Space(5)]
    public MarchingCubeMesh chunkPrefab;
    public DensityGenerator densityGenerator;

    public MarchingCubeMesh[] chunks;

    private void Start()
    {
        chunks = new MarchingCubeMesh[NumChunks];

        StartCoroutine(CreateChunks());
    }

    int indexFromCoord(int x, int y, int z)
    {
        return z * size * size + y * size + x;
    }

    int indexFromCoord(Vector3Int id)
    {
        return indexFromCoord(id.x, id.y, id.z);
    }

    private IEnumerator CreateChunks()
    {
        System.Diagnostics.Stopwatch stopWatch = new System.Diagnostics.Stopwatch();
        stopWatch.Restart();
        for (int x = 0; x < size; x++)
        {
            for (int z = 0; z < size; z++)
            {
                for (int y = 0; y < size; y++)
                {
                    CreateChunk(new Vector3Int(x, y, z));
                    yield return null;
                }
            }
        }

        float totalTime = stopWatch.ElapsedMilliseconds / 1000f;
        float average = stopWatch.ElapsedMilliseconds / NumChunks;

        print("Finished creating chunks in " + totalTime.ToString("0.00 sec") + " with an average of " + average.ToString("0.00 mms") + " per chunk");

        StartCoroutine(UpdateChunks());
    }

    private IEnumerator UpdateChunks()
    {
        System.Diagnostics.Stopwatch stopWatch = new System.Diagnostics.Stopwatch();
        stopWatch.Restart();
        int index = 0;
        for (int x = 0; x < size; x++)
        {
            for (int z = 0; z < size; z++)
            {
                for (int y = 0; y < size; y++)
                {
                    index = indexFromCoord(x, y, z);
                    chunks[index].UpdateMesh();

                    if(chunks[index].triangleCount > 0)
                    {
                        yield return null;// new WaitForSeconds(0.1f);
                    }
                    else
                    {
                        yield return null;
                    }
                }
            }
        }
        float totalTimeSec = (stopWatch.ElapsedMilliseconds / 1000f);
        float average = (stopWatch.ElapsedMilliseconds) / NumChunks;
        print("Finished updating chunks in " + totalTimeSec.ToString("0.00 sec") + " with an average of " + average.ToString("0.00 mms") + " per chunk");
    }

    private void CreateChunk(Vector3Int chunkPos)
    {
        var chunk = Instantiate(chunkPrefab);

        chunk.transform.position = transform.position + (Vector3)chunkPos * voxelScale * chunkSize + (Vector3)offset * voxelScale * chunkSize;
        chunk.transform.parent = transform;

        chunk.densityGenerator.noiseData = densityGenerator.noiseData;

        chunk.voxelScale = voxelScale;
        chunk.numVoxelsPerAxis = chunkSize;

        chunks[indexFromCoord(chunkPos)] = chunk;

    }


    private void OnValidate()
    {
        int maxSize = 32;
        size = Mathf.Clamp(size, 1, maxSize);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;

        Vector3 center = (Vector3)offset * voxelScale * chunkSize;
        Vector3 drawSize = new Vector3(size, size, size) * voxelScale * chunkSize * 1.01f;

        center += drawSize / 2f;

        Gizmos.DrawWireCube(center, drawSize);
    }

}
