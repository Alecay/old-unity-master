using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InfiniteMarchingCubes : MonoBehaviour
{
    [Header("Sizing")]
    public int chunkWidth;
    public int chunkHeight;
    public int size = 16;

    [Header("References")]
    public ComputeShader marchComputeShader;
    public ComputeShader densityComputeShader;
    public MarchingCubesGPUMesh gpuMeshPrefab;
    public Transform playerTransform;

    [Header("Noise")]
    public DensityGenerator.NoiseData noiseData;

    //Mesh Builders
    private MarchingCubesMeshGenerator collisionMeshGenerator;

    public Dictionary<Vector2Int, MarchingCubesGPUMesh> usedChunks = new Dictionary<Vector2Int, MarchingCubesGPUMesh>();
    public Queue<MarchingCubesGPUMesh> unusedChunks = new Queue<MarchingCubesGPUMesh>();

    public Queue<MarchingCubesGPUMesh> chunksToUpdate = new Queue<MarchingCubesGPUMesh>();
    public Queue<MarchingCubesGPUMesh> chunksToCreateCollisionMesh = new Queue<MarchingCubesGPUMesh>();
    private bool waitingForCollisionMesh = false;

    private Vector2Int[] requestOffsets;
    private int[] requestOffsetsMeshSimplificationLevels;

    private Vector2Int lastPlayerChunkPos = new Vector2Int(-1, -1);
    private bool newPlayerChunkPosThisFrame = false;

    private Coroutine meshBuildCo;

    private void Start()
    {
        CreateOffsets();

        collisionMeshGenerator = new MarchingCubesMeshGenerator(chunkWidth, chunkHeight, 0, true);
        collisionMeshGenerator.meshSimplificationLevel = 1;

        //StartCoroutine(SampleRegionBuild());
    }

    private void OnDisable()
    {
        collisionMeshGenerator.Dispose();
    }

    private void Update()
    {
        UpdatePlayerChunkPos();

        if (newPlayerChunkPosThisFrame)
        {            
            if(meshBuildCo != null)
            {
                chunksToUpdate.Clear();
                chunksToCreateCollisionMesh.Clear();
                StopCoroutine(meshBuildCo);                
            }

            meshBuildCo = StartCoroutine(SampleRegionBuild());
        }

        BuildNextCollisionMesh();
    }

    private void CreateOffsets()
    {
        List<Vector2Int> offsets = new List<Vector2Int>();
        Vector2Int offset;
        for (int y = -size; y <= size; y++)
        {
            for (int x = -size; x <= size; x++)
            {
                offset = new Vector2Int(x, y);
                if (offset.magnitude < size)
                {
                    offsets.Add(offset);
                }
            }
        }

        offsets.Sort((o1, o2) => o1.magnitude.CompareTo(o2.magnitude));

        requestOffsetsMeshSimplificationLevels = new int[offsets.Count];
        int mLevel = 0;
        float mag = 0;
        for (int i = 0; i < offsets.Count; i++)
        {
            mLevel = 2;
            mag = offsets[i].magnitude;

            if (mag < size / 3f)
            {
                mLevel = 0;
            }
            else if (mag < size / 3f * 2f)
            {
                mLevel = 1;
            }

            requestOffsetsMeshSimplificationLevels[i] = mLevel;
        }

        requestOffsets = offsets.ToArray();
    }

    private void UpdatePlayerChunkPos()
    {
        Vector2Int chunkPos = new Vector2Int(Mathf.FloorToInt(playerTransform.position.x / chunkWidth), Mathf.FloorToInt(playerTransform.position.z / chunkWidth));
        newPlayerChunkPosThisFrame = false;
        if (chunkPos != lastPlayerChunkPos)
        {
            lastPlayerChunkPos = chunkPos;
            newPlayerChunkPosThisFrame = true;
        }
    }

    private IEnumerator SampleRegionBuild()
    {
        Vector2Int offset;
        bool needed;

        List<Vector2Int> toRemove = new List<Vector2Int>();

        foreach (var pos in usedChunks.Keys)
        {
            offset = pos - lastPlayerChunkPos;

            needed = false;
            for (int i = 0; i < requestOffsets.Length; i++)
            {        
                if(offset == requestOffsets[i])
                {
                    needed = true;
                    break;
                }
            }

            if (!needed)
            {
                toRemove.Add(pos);
            }

            //yield return null;
        }

        for (int i = 0; i < toRemove.Count; i++)
        {            
            unusedChunks.Enqueue(usedChunks[toRemove[i]]);
            usedChunks.Remove(toRemove[i]);
        }

        for (int i = 0; i < requestOffsets.Length; i++)
        {
            RequestChunk(requestOffsets[i] + lastPlayerChunkPos, requestOffsetsMeshSimplificationLevels[i]);
            yield return null;

            UpdateNextChunk();
            yield return null;
        }

        //while(chunksToUpdate.Count > 0)
        //{
        //    UpdateNextChunk();
        //    yield return null;
        //}
    }

    private void RequestChunk(Vector2Int chunkPos, int meshSimplificationLevel)
    {
        MarchingCubesGPUMesh chunk;
        bool reused = false;
        
        if(usedChunks.TryGetValue(chunkPos, out chunk))
        {
            reused = true;
        }
        else if(unusedChunks.Count > 0)
        {
            chunk = unusedChunks.Dequeue();
        }
        else
        {
            chunk = CreateNewChunk(chunkPos);
        }

        chunk.noiseData.SetValues(noiseData);
        chunk.position = chunkPos;
        chunk.UpdatePosition();
        chunk.meshSimplificationLevel = meshSimplificationLevel;

        if(!reused)
            usedChunks.Add(chunkPos, chunk);

        RequestChunkUpdate(chunk);
    }

    private void RequestChunkUpdate(MarchingCubesGPUMesh chunk)
    {
        chunksToUpdate.Enqueue(chunk);
    }

    private void UpdateNextChunk()
    {
        if(chunksToUpdate.Count > 0)
        {
            var chunk = chunksToUpdate.Dequeue();//.UpdateMesh();

            chunk.UpdateMesh();

            if(chunk.meshSimplificationLevel == 0)
            {
                RequestCollisonMesh(chunk);
            }
            else
            {
                chunk.SetCollisonMesh(null);
            }

        }
    }

    private void RequestCollisonMesh(MarchingCubesGPUMesh chunk)
    {
        chunksToCreateCollisionMesh.Enqueue(chunk);
    }

    private void BuildNextCollisionMesh()
    {
        if(!waitingForCollisionMesh && chunksToCreateCollisionMesh.Count > 0)
        {
            var chunk = chunksToCreateCollisionMesh.Dequeue();            

            StartCoroutine(BuildCollisionMesh(chunk));
        }
    }

    private IEnumerator BuildCollisionMesh(MarchingCubesGPUMesh chunk)
    {
        waitingForCollisionMesh = true;

        chunk.densityGenerator.RequestData();

        while (chunk.densityGenerator.WaitingForData)
        {
            yield return null;
        }

        collisionMeshGenerator.densityValues = chunk.densityGenerator.values;
        chunk.mCollider ??= chunk.GetComponent<MeshCollider>();
        StartCoroutine(collisionMeshGenerator.UpdateMeshAndAssign(chunk.collisionMesh, chunk.mCollider));

        while (collisionMeshGenerator.creatingMesh)
        {
            yield return null;
        }   

        waitingForCollisionMesh = false;
    }

    private MarchingCubesGPUMesh CreateNewChunk(Vector2Int position)
    {
        var chunk = Instantiate(gpuMeshPrefab);

        chunk.width = chunkWidth;
        chunk.height = chunkHeight;

        chunk.meshSimplificationLevel = 0;

        //if (setMeshSimplificationLevel)
        //{
        //    if (position.x < regionSize / 2 - 3 || position.x > regionSize - regionSize / 2 + 3)
        //    {
        //        chunk.meshSimplificationLevel = 1;
        //    }

        //    if (position.x < regionSize / 8 || position.x > regionSize - regionSize / 8 - 1)
        //    {
        //        chunk.meshSimplificationLevel = 2;
        //    }
        //}

        chunk.position = position;
        chunk.UpdatePosition();
        chunk.transform.parent = transform;

        return chunk;
    }
}
