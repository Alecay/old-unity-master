using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class VoxelStateGenerator : ComputeLoader
{
    [Header("Debug Info")]
    public int processedRequestsCount = 0;
    public int nonEmptyChunks = 0;
    public int emptyChunks = 0;
    public int fullChunks = 0;

    public bool loadingData = false;

    public delegate void OnStatesLoaded(VoxelMesh mesh);
    public OnStatesLoaded OnChunkStatesAvalible;

    //public AnimationCurve densityCurve;

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

        public int chunkSize = 16;

        public Vector3 chunkOffset = new Vector3(0, 0, 0);

        public float heightMultiplier = 16.0f;

        public int heightOffset = 1;

        public uint worldHeight = 256;
    }

    [SerializeField]
    public NoiseData noiseData;
    public float radius = 100f;

    [Tooltip("Set the offset of the noise using the render's position")]
    public bool usePositionAsOffset = false;

    public ComputeBuffer densityValuesBuffer;
    public const int DENSITY_STRIDE = sizeof(float);

    [HideInInspector]
    public float[] densityValues;

    private Dictionary<string, List<Vector3Int>> voxelLocations = new Dictionary<string, List<Vector3Int>>();

    public class ChunkStatesRequest
    {
        public Vector3Int worldChunkPos;
        public VoxelChunkPillar pillar;

        public ChunkStatesRequest(Vector3Int worldChunkPos, VoxelChunkPillar pillar)
        {
            this.worldChunkPos = worldChunkPos;
            this.pillar = pillar;
        }
    }

    public Queue<ChunkStatesRequest> chunkStatesRequests = new Queue<ChunkStatesRequest>();

    private bool processingRequest = false;

    protected override void CreateBuffers()
    {
        base.CreateBuffers();

        int densityArrLength = (int)Mathf.Pow(noiseData.chunkSize + 2, 3);

        densityValuesBuffer = new ComputeBuffer(densityArrLength, DENSITY_STRIDE);
        computeShader.SetBuffer(idKernel, "Density_Values_Buffer", densityValuesBuffer);

        densityValues = new float[densityArrLength];
    }

    protected override void DisposeBuffers()
    {
        base.DisposeBuffers();

        densityValuesBuffer.Release();
    }

    protected override void SetComputeVariables()
    {
        computeShader.SetInt("Seed", noiseData.seed);
        computeShader.SetInt("Octaves", noiseData.octaves);

        computeShader.SetFloat("Scale", noiseData.scale);
        computeShader.SetFloat("Persistance", noiseData.persistance);
        computeShader.SetFloat("Lacunarity", noiseData.lacunarity);

        computeShader.SetVector("Offset", usePositionAsOffset ? transform.position : noiseData.offset - new Vector3(-1,-1,-1));

        computeShader.SetInt("ChunkSize", noiseData.chunkSize + 2);
        computeShader.SetVector("ChunkOffset", noiseData.chunkOffset);

        computeShader.SetFloat("HeightMultiplier", noiseData.heightMultiplier);
        computeShader.SetInt("HeightOffset", noiseData.heightOffset);
        computeShader.SetInt("WorldHeight", (int)noiseData.worldHeight);

        computeShader.SetFloat("Radius", radius);
    }

    protected override void UpdateDispatchTimes()
    {
        dispatchTimes = new Vector3Int(noiseData.chunkSize + 2, noiseData.chunkSize + 2, noiseData.chunkSize + 2);
    }

    public override void RequestData()
    {        
        loadingData = true;
        AsyncGPUReadback.Request(densityValuesBuffer, r1 => OnDataAvalible(r1));
    }

    protected override void OnDataAvalible(AsyncGPUReadbackRequest request)
    {
        if (request.hasError || !Application.isPlaying)
        {
            return;
        }

        var data = request.GetData<float>();
        //densityValues = new float[data.Length];
        data.CopyTo(densityValues);

        onDataAvalible?.Invoke();

        loadingData = false;
    }

    public void RequestChunkStates(Vector3Int worldChunkPos, VoxelChunkPillar pillar)
    {
        chunkStatesRequests.Enqueue(new(worldChunkPos, pillar));
    }

    IEnumerator ProcessNextChunkStateRequest()
    {        
        processingRequest = true;

        if(chunkStatesRequests.Count <= 0)
        {
            processingRequest = false;
            yield break;
        }

        var request = chunkStatesRequests.Dequeue();

        //Debug.Log("Processing request for chunk at " + request.worldChunkPos);

        noiseData.offset = request.worldChunkPos * noiseData.chunkSize;
        UpdateData();
        RequestData();

        while (loadingData)
        {
            yield return null;
        }

        int type = CalculateVoxelStates(request);

        if(type == 2)
        {
            //yield return new WaitForSeconds(0.01f);
        }

        processingRequest = false;

        //Debug.Log("Finished loading states for chunk at " + request.worldChunkPos);

        yield return null;       
    }

    IEnumerator LoadChunkStates()
    {
        while (true)
        {
            processingRequest = true;
            StartCoroutine(ProcessNextChunkStateRequest());

            while (processingRequest)
            {
                yield return null;
            }

            yield return null;
        }
    }

    private int CalculateVoxelStates(ChunkStatesRequest request)
    {
        // returns 0 if empty, 1 if full, 2 if standard

        bool skipEmpty = false;
        bool skipFull = false;

        foreach (var key in voxelLocations.Keys)
        {
            voxelLocations[key].Clear();
        }

        string[] toAdd = new string[] { "minecraft:stone", "minecraft:dirt", "minecraft:grass", "minecraft:quartz_pillar", "minecraft:farmland", "minecraft:water" };

        for (int i = 0; i < toAdd.Length; i++)
        {
            if (!voxelLocations.ContainsKey(toAdd[i]))
            {
                voxelLocations.Add(toAdd[i], new List<Vector3Int>());
            }
        }

        string id;

        for (int x = 0; x < noiseData.chunkSize; x++)
        {
            for (int y = 0; y < noiseData.chunkSize; y++)
            {
                for (int z = 0; z < noiseData.chunkSize; z++)
                {
                    id = GetVoxelID(x, y, z);

                    if(id != "base:air")
                    {
                        if (!voxelLocations.ContainsKey(id))
                        {
                            voxelLocations.Add(id, new List<Vector3Int>());
                        }

                        voxelLocations[id].Add(new(x, y, z));
                    }

                }
            }
        }

        //Add Top and bottom face boundary states
        for (int x = 0; x < noiseData.chunkSize; x++)
        {
            for (int z = 0; z < noiseData.chunkSize; z++)
            {
                int y = noiseData.chunkSize;

                id = GetVoxelID(x, y, z);

                if (!voxelLocations.ContainsKey(id))
                {
                    voxelLocations.Add(id, new List<Vector3Int>());
                }

                voxelLocations[id].Add(new(x, y, z));

                y = -1;

                id = GetVoxelID(x, y, z);

                if (!voxelLocations.ContainsKey(id))
                {
                    voxelLocations.Add(id, new List<Vector3Int>());
                }

                voxelLocations[id].Add(new(x, y, z));
            }
        }

        //Add Left and right face boundary states
        for (int z = 0; z < noiseData.chunkSize; z++)
        {
            for (int y = 0; y < noiseData.chunkSize; y++)
            {
                int x = -1;

                id = GetVoxelID(x, y, z);

                if (!voxelLocations.ContainsKey(id))
                {
                    voxelLocations.Add(id, new List<Vector3Int>());
                }

                voxelLocations[id].Add(new(x, y, z));

                x = noiseData.chunkSize;

                id = GetVoxelID(x, y, z);

                if (!voxelLocations.ContainsKey(id))
                {
                    voxelLocations.Add(id, new List<Vector3Int>());
                }

                voxelLocations[id].Add(new(x, y, z));
            }
        }

        //Add forward and back
        for (int x = 0; x < noiseData.chunkSize; x++)
        {
            for (int y = 0; y < noiseData.chunkSize; y++)
            {
                int z = -1;

                id = GetVoxelID(x, y, z);

                if (!voxelLocations.ContainsKey(id))
                {
                    voxelLocations.Add(id, new List<Vector3Int>());
                }

                voxelLocations[id].Add(new(x, y, z));

                z = noiseData.chunkSize;

                id = GetVoxelID(x, y, z);

                if (!voxelLocations.ContainsKey(id))
                {
                    voxelLocations.Add(id, new List<Vector3Int>());
                }

                voxelLocations[id].Add(new(x, y, z));
            }
        }


        processedRequestsCount++;


        int sum = 0;
        foreach (var key in voxelLocations.Keys)
        {
            if(key != "base:air")
                sum += voxelLocations[key].Count;
        }

        if(sum <= 0)
        {
            emptyChunks++;
            if(skipEmpty)
                return 0;
        }

        //Full on the inside and outside
        if(sum == 4096 + 1536)
        {
            fullChunks++;
            if (skipFull)
                return 1;
        }

        nonEmptyChunks++;

        var mesh = request.pillar.CreateChunk(request.worldChunkPos.y);

        //mesh.InitializeStatesArray();

        //mesh.states.palette.Clear();
        //mesh.states.BoxFill(0, 0, 0, noiseData.chunkSize - 1, noiseData.chunkSize - 1, noiseData.chunkSize - 1, "base:air");

        //foreach (var key in voxelLocations.Keys)
        //{
        //    print(key + " " + voxelLocations[key].Count);

        //    mesh.states.palette.AddVoxel(key);

        //    if(voxelLocations[key].Count > 0)
        //        mesh.states.SetVoxels(key, voxelLocations[key]);
        //}

        mesh.states.SetVoxels("minecraft:stone", voxelLocations["minecraft:stone"]);
        mesh.states.SetVoxels("minecraft:dirt", voxelLocations["minecraft:dirt"]);
        mesh.states.SetVoxels("minecraft:grass", voxelLocations["minecraft:grass"]);
        mesh.states.SetVoxels("minecraft:quartz_pillar", voxelLocations["minecraft:quartz_pillar"]);
        mesh.states.SetVoxels("minecraft:farmland", voxelLocations["minecraft:farmland"]);
        mesh.states.SetVoxels("minecraft:water", voxelLocations["minecraft:water"]);
        mesh.UpdateMesh();

        //mesh.states.palette.PrintVoxelIDs();

        //print("Added " + voxelLocations["minecraft:stone"].Count);

        return 2;
    }

    private string GetVoxelID(int x, int y, int z)
    {
        int index = GreedyMeshing.XYZToLinearIndex(x + 1, y + 1, z + 1, noiseData.chunkSize + 2);

        float dValue = densityValues[index];        

        int actualY = y + (int)noiseData.offset.y;

        string id = "base:air";
        float nValue;

        if (dValue < 0)
        {
            id = "minecraft:stone";
        }

        bool shouldBeDirt = false;

        for (int i = 1; i < 5; i++)
        {
            if (y + i < noiseData.chunkSize + 1)
            {
                nValue = densityValues[GreedyMeshing.XYZToLinearIndex(x + 1, y + i + 1, z + 1, noiseData.chunkSize + 2)];
                if (nValue >= 0)
                {
                    shouldBeDirt = true;
                    break;
                }
            }
        }

        if (dValue < 0 && shouldBeDirt)
        {
            id = "minecraft:dirt";
        }

        if (y < noiseData.chunkSize)
        {
            nValue = densityValues[GreedyMeshing.XYZToLinearIndex(x + 1, y + 2, z + 1, noiseData.chunkSize + 2)];

            if (dValue < 0 && nValue >= 0)
            {
                if (actualY < 60)
                {
                    id = "minecraft:grass";
                }
                else
                {
                    id = "minecraft:quartz_pillar";
                }

            }

        }

        if(actualY < noiseData.heightOffset)//id == "minecraft:grass" &&
        {
            id = "minecraft:water";
        }

        return id;
    }

    protected override void Start()
    {
        base.Start();

        StartCoroutine(LoadChunkStates());
    }

    protected override void Update()
    {
        base.Update();
    }
}
