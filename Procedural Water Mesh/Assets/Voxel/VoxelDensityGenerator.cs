using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;

public class VoxelDensityGenerator : ComputeLoader
{
    [System.Serializable]
    public class NoiseData
    {
        public int seed = 42;

        [Range(1, 8)]
        public int octaves = 5;

        public float scale = 100.0f;

        public float persistance = 0.5f;
        public float lacunarity = 2.0f;

        public Vector3 offset = new Vector3(0, 0, 0);

        public int width = 16;

        public int worldHeight = 256;

        public float heightMultiplier = 16.0f;

        public int heightOffset = 1;
    }

    [SerializeField]
    public NoiseData noiseData;

    [Tooltip("Set the offset of the noise using the render's position")]
    public bool usePositionAsOffset = false;

    public ComputeBuffer densityValuesBuffer;
    public const int DENSITY_STRIDE = sizeof(float);

    [HideInInspector]
    public NativeArray<float> densityValues;
    
    public struct DensityRequest
    {
        public VoxelP pillar;

        public DensityRequest(VoxelP pillar)
        {
            this.pillar = pillar;
        }
    }

    //Mesh request information
    private bool allowProcessingDensityRequests = true;
    private bool processingDensityRequest = false;//is a request currently being processed?
    public bool ProcessingDensityRequest
    {
        get
        {
            return processingDensityRequest;
        }
    }

    private bool waitingForRequestedData = false;
    private Queue<DensityRequest> densityRequests = new Queue<DensityRequest>();
    public int TotalRequests
    {
        get
        {
            return densityRequests.Count;
        }
    }

    //Debug Info
    public RequestTimeInfo requestTimeInfo;

    [System.Serializable]
    public class RequestTimeInfo
    {
        public float requestTime = 0;

        private int processedRequests = 0;

        public float averageTimePerRequest;

        public void AddTime(float mms)
        {
            requestTime += mms;
            processedRequests++;

            averageTimePerRequest = (requestTime / processedRequests);
        }
    }

    protected override void Start()
    {
        requestTimeInfo = new RequestTimeInfo();
        base.Start();        
    }

    public void StartProcessingDensityRequests()
    {
        allowProcessingDensityRequests = true;
        StartCoroutine(ProcessDensityRequestsCo());
    }

    private IEnumerator ProcessDensityRequestsCo()
    {
        processingDensityRequest = false;

        while (allowProcessingDensityRequests)
        {
            if (!processingDensityRequest && densityRequests.Count > 0)
            {
                StartCoroutine(ProcessNextDensityRequest());
            }

            yield return null;
        }

        yield return null;
    }

    private IEnumerator ProcessNextDensityRequest()
    {
        float startTime = Time.realtimeSinceStartup;
        float mms;

        //print("Started next render request");

        processingDensityRequest = true;
        var request = densityRequests.Dequeue();

        noiseData.width = request.pillar.width + 2;
        noiseData.worldHeight = request.pillar.height;        
        noiseData.offset = new Vector3(request.pillar.position.x, 0, request.pillar.position.y) * request.pillar.width;

        UpdateData();
        RequestData();

        while (waitingForRequestedData)
        {
            yield return null;
        }

        request.pillar.OnDensityReady();

        mms = (Time.realtimeSinceStartup - startTime) * 1000;

        requestTimeInfo.AddTime(mms);

        bool printMessages = false;

        if (printMessages)
        {
            print("Processed density request in " + mms.ToString("0.00") + " mms");
        }

        processingDensityRequest = false;      

        yield return null;
    }

    public void RequestDensityValues(DensityRequest request)
    {
        request.pillar.waitingForDensityValues = true;
        densityRequests.Enqueue(request);
    }

    protected override void CreateBuffers()
    {
        base.CreateBuffers();

        densityValuesBuffer = new ComputeBuffer(noiseData.width * noiseData.width * noiseData.worldHeight, DENSITY_STRIDE);
        computeShader.SetBuffer(idKernel, "Density_Values_Buffer", densityValuesBuffer);

        densityValues = new NativeArray<float>(noiseData.width * noiseData.width * noiseData.worldHeight, Allocator.Persistent);
    }

    protected override void DisposeBuffers()
    {
        base.DisposeBuffers();

        densityValuesBuffer.Release();
        densityValues.Dispose();
    }

    protected override void SetComputeVariables()
    {
        computeShader.SetInt("Seed", noiseData.seed);
        computeShader.SetInt("Octaves", noiseData.octaves);

        computeShader.SetFloat("Scale", noiseData.scale);
        computeShader.SetFloat("Persistance", noiseData.persistance);
        computeShader.SetFloat("Lacunarity", noiseData.lacunarity);

        computeShader.SetVector("Offset", usePositionAsOffset ? transform.position : noiseData.offset - new Vector3(-1, 0, -1));

        computeShader.SetInt("ChunkSize", noiseData.width);
        computeShader.SetVector("ChunkOffset", new Vector4());

        computeShader.SetFloat("HeightMultiplier", noiseData.heightMultiplier);
        computeShader.SetInt("HeightOffset", noiseData.heightOffset);
        computeShader.SetInt("WorldHeight", (int)noiseData.worldHeight);

        computeShader.SetFloat("Radius", 100);
    }

    protected override void UpdateDispatchTimes()
    {
        dispatchTimes = new Vector3Int(noiseData.width, noiseData.worldHeight, noiseData.width);
    }

    public override void UpdateData()
    {
        if(densityValuesBuffer.count != noiseData.width * noiseData.width * noiseData.worldHeight)
        {
            Deinitialize();
        }

        base.UpdateData();
    }

    public override void RequestData()
    {
        //UpdateData();        

        waitingForRequestedData = true;
        AsyncGPUReadback.Request(densityValuesBuffer, r1 => OnDataAvalible(r1));
    }

    protected override void OnDataAvalible(AsyncGPUReadbackRequest request)
    {
        if (request.hasError || !Application.isPlaying)
        {
            Debug.LogError("Error loading data");
            return;
        }               

        if (densityValues.IsCreated)
            densityValues.Dispose();

        //var data = request.GetData<float>();
        //densityValues = new float[data.Length];
        //data.CopyTo(densityValues);
        densityValues = request.GetData<float>();

        waitingForRequestedData = false;

        onDataAvalible?.Invoke();
    }

    public void PopulateCellIDs(int width, int height, NativeArray<int> cellIDs)
    {
        var job = new CellIDJob()
        {
            width = width,
            height = height,
            densityValues = densityValues,
            cellIDs = cellIDs
        };

        job.Schedule((width + 2) * (width + 2) * height, (width + 2) * (width + 2)).Complete();
    }

    [BurstCompile]
    private struct CellIDJob : IJobParallelFor
    {
        public int width;
        public int height;

        [NativeDisableParallelForRestriction] [ReadOnly]
        public NativeArray<float> densityValues;

        public NativeArray<int> cellIDs;

        private bool InBounds(int x, int y, int z)
        {
            return x >= 0 && x < width && y >= 0 && y < height && z >= 0 && z < width;
        }

        private bool InOuterBounds(int x, int y, int z)
        {
            return x >= -1 && x <= width && y >= 0 && y < height && z >= -1 && z <= width;
        }

        public static int3 LinearIndexToXYZInt3(int index, int width)
        {
            int sizeSqd = width * width;

            return new int3(index % sizeSqd % width, index / sizeSqd, index % sizeSqd / width);
        }

        public static int LinearIndex(int x, int y, int z, int width)
        {
            return x + z * width + y * width * width;
        }

        private float GetDensity(int x, int y, int z)
        {
            if (!InOuterBounds(x, y, z))
            {
                return 0;
            }

            return densityValues[LinearIndex(x + 1, y, z + 1, width + 2)];
        }

        private bool IsSoild(int x, int y, int z)
        {
            return GetDensity(x, y, z) > 0;
        }

        private bool IsFloating(int x, int y, int z)
        {
            //If positon is not solid
            if(!IsSoild(x, y, z))
            {
                return false;
            }

            int nCount = 0;

            if (IsSoild(x, y + 1, z))
                nCount++;

            if (IsSoild(x, y - 1, z))
                nCount++;

            if (IsSoild(x + 1, y, z))
                nCount++;

            if (IsSoild(x - 1, y, z))
                nCount++;

            if (IsSoild(x, y, z + 1))
                nCount++;

            if (IsSoild(x, y, z - 1))
                nCount++;

            if(nCount == 0)
            {
                return true;
            }

            return false;
        }

        public void Execute(int index)
        {
            if(index >= (width + 2) * (width + 2) * height)
            {
                return;
            }

            int3 pos = LinearIndexToXYZInt3(index, width + 2) - new int3(1, 0, 1);
            float density = densityValues[index];

            cellIDs[index] = 0;

            if(IsSoild(pos.x, pos.y, pos.z) && !IsFloating(pos.x, pos.y, pos.z))
            {
                cellIDs[index] = 1;

                if(!IsSoild(pos.x, pos.y + 1, pos.z))
                {
                    cellIDs[index] = 3;
                }
            }
            else
            {
                if(pos.y <= 180)
                {
                    cellIDs[index] = 4;//Water
                }
            }

            //if (density > 0f)
            //{
            //    //Stone
            //    cellIDs[index] = 1;

            //    int emptyDist = 10;

            //    for (int i = 1; i <= 5; i++)
            //    {
            //        if (pos.y + i >= height || densityValues[LinearIndex(pos.x + 1, pos.y + i, pos.z + 1, width + 2)] <= 0f)
            //        {
            //            emptyDist = i;
            //            break;
            //        }
            //    }

            //    if (emptyDist <= 5 && pos.y < height * 0.8f && pos.y > 150) //Dirt
            //    {
            //        cellIDs[index] = 2;
            //    }

            //    if (emptyDist == 1 && pos.y < height * 0.7f && pos.y > 150) //Grass
            //    {
            //        cellIDs[index] = 3;
            //    }
                
            //}

            //if (cellIDs[index] == 0 && pos.x == 5 && pos.z == 8)
            //{
            //    int solidDist = 10;
            //    for (int i = 1; i <= 5; i++)
            //    {
            //        if (pos.y - i < 0 || densityValues[LinearIndex(pos.x + 1, pos.y - i, pos.z + 1, width + 2)] > 0f)
            //        {
            //            solidDist = i;
            //            break;
            //        }
            //    }

            //    //cellIDs[index] = 4;

            //    if (solidDist < 5)
            //    {
            //        cellIDs[index] = 4;
            //    }
            //}


        }
    }
}
