using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MarchingChunkDataManager : MonoBehaviour
{
    public DensityGenerator densityGenerator;

    [Tooltip("How many voxels long are each of the chunks")]
    public int chunkSize;

    public Queue<Vector3Int> requestedChunks = new Queue<Vector3Int>();
    //[SerializeField]
    [HideInInspector]
    public List<ChunkData> chunkData = new List<ChunkData>();

    [HideInInspector]
    public int processedRequests = 0;

    public TriangleCountLoader triCountLoader;

    [HideInInspector]
    public bool shouldProcessNextTick = false;
    private int cUpdates = 0;

    public Vector3Int size;
    public int cUpdateLimit = 10;

    private void Start()
    {
        Initialize();        

        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                for (int z = 0; z < size.z; z++)
                {
                    RequestChunkData(new Vector3Int(x, y, z));                    
                }
            }
        }

        shouldProcessNextTick = true;            
    }

    private void Update()
    {
        if (shouldProcessNextTick)
        {
            shouldProcessNextTick = false;
            ProcessNextChunkDataRequest();
        }
    }

    private void Initialize()
    {
        densityGenerator.noiseData.chunkSize = chunkSize + 1;
        densityGenerator.noiseData.chunkOffset = new Vector3();
        densityGenerator.noiseData.offset = new Vector3();
        densityGenerator.onDataAvalible = OnDensityValuesAvalible;

        densityGenerator.Initialize();        

        requestedChunks = new Queue<Vector3Int>();
        chunkData = new List<ChunkData>();

        processedRequests = 0;

        triCountLoader.voxelsPerAxis = chunkSize;
        triCountLoader.onDataAvalible = OnTriangleCountAvalible;
    }

    private void OnDensityValuesAvalible()
    {
        Vector3Int chunkPos = new Vector3Int(
            (int)densityGenerator.noiseData.offset.x / chunkSize, 
            (int)densityGenerator.noiseData.offset.y / chunkSize, 
            (int)densityGenerator.noiseData.offset.z / chunkSize);

        float[] values = new float[densityGenerator.densityValues.Length];
        densityGenerator.densityValues.CopyTo(values, 0);

        //if (processedRequests < 500)
        //{
        //    triCountLoader.densityValues = values;
        //    triCountLoader.UpdateData();
        //}

        int triCount = triCountLoader.triangleCount;

        //if(triCount > 0)
            chunkData.Add(new ChunkData(chunkSize, chunkPos, triCount, values));

        float percent = (triCount * 3) / 65535f;

        //Debug.Log("Recieved data for chunk at " + chunkPos + " which has " + triCount + " triangles and " + percent.ToString("0.00% of unity max verts"));        


        processedRequests++;

        shouldProcessNextTick = true;
        float percentLoaded = (processedRequests / (float)(size.x * size.y * size.z));

        //if (percentLoaded * 100 >= 90f)
        //{
        //    Debug.Log("Loading 90%");
        //}
        //else if (percent * 100 >= 80f)
        //{
        //    Debug.Log("Loading 80%");
        //}

        //Debug.Log("Loading data " + percentLoaded.ToString("0.0%"));        

    }

    private void RequestChunkData(Vector3Int globalChunkPos)
    {
        requestedChunks.Enqueue(globalChunkPos);
    }

    private void ProcessNextChunkDataRequest()
    {
        if(requestedChunks.Count <= 0)
        {
            Debug.Log("Reached the end of requested data in " + 
                Time.realtimeSinceStartup.ToString("0.00 seconds") +
                " with an average of " + (Time.realtimeSinceStartup / (float)processedRequests * 1000f).ToString("0.0 milliseconds") + " per request");
            return;
        }        

        var chunkPos = requestedChunks.Dequeue();

        ProcessChunkDataRequest(chunkPos);
    }

    private void ProcessChunkDataRequest(Vector3Int globalChunkPos)
    {
        densityGenerator.noiseData.offset = globalChunkPos * chunkSize;
        densityGenerator.UpdateData();

        triCountLoader.UpdateData();
        triCountLoader.RequestData();
    }

    private void OnTriangleCountAvalible()
    {
        if (triCountLoader.triangleCount > 0)
        {            
            densityGenerator.RequestData();
            cUpdates = 0;
        }
        else
        {
            if(cUpdates < cUpdateLimit)
            {
                ProcessNextChunkDataRequest();
                cUpdates++;
            }
            else
            {
                //Debug.Log("Reached end on c updates");
                shouldProcessNextTick = true;
                cUpdates = 0;
            }

            processedRequests++;
        }
    }

    public struct MeshRequest
    {
        public Vector3Int chunkPos;
        public int meshSimplificationLevel;
    }

    [System.Serializable]
    public class ChunkData
    {        
        public int chunkSize;
        public Vector3Int chunkPos;
        public int triangleCount;

        [HideInInspector]
        public float[] denistyValues;

        public ChunkData(int chunkSize, Vector3Int chunkPos, int triangleCount, float[] denistyValues)
        {
            this.chunkSize = chunkSize;
            this.chunkPos = chunkPos;
            this.triangleCount = triangleCount;
            this.denistyValues = denistyValues;
        }
    }

    /// <summary>
    /// Contains a group of chunk data
    /// </summary>
    public class Region
    {
        public int size; //How many chunks^3
        public int chunkSize;
        public Vector3Int regionPos;
        public ChunkData[] chunks;

        private Vector3Int offset;
        private void Initialize()
        {
            chunks = new ChunkData[size * size * size];

            offset = new Vector3Int(
                regionPos.x * size,
                regionPos.x * size,
                regionPos.x * size);
        }

        public void AddChunkData(ChunkData chunkData)
        {
            Vector3Int localPos = chunkData.chunkPos - offset;

            int index = localPos.x + localPos.y * size + localPos.z * size * size;

            chunks[index] = chunkData;
        }
    }
}
