using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Rendering;

public class VoxelMeshGenerator : MonoBehaviour
{
    public bool completeRequestsImmediatley = false;
    public float targetJobCompletion = 25;
    
    private NativeList<SliceJob.StartEnd> startEnds;

    private int numberOfStartEndsLastMeshGen = -1;

    //Mesh data
    private NativeArray<Vector3> vertices;
    private NativeArray<int> indices;
    private NativeArray<Vector3> uvs;
    private NativeArray<Vector3> normals;

    //Mesh request information
    private bool allowProcessingMeshRequests = true;
    private bool processingMeshRequest = false;//is a request currently being processed?
    public bool ProcessingMeshRequest
    {
        get
        {
            return processingMeshRequest;
        }
    }
    private Queue<MeshRequest> meshRequests = new Queue<MeshRequest>();
    private Queue<MeshRequest> priorityMeshRequests = new Queue<MeshRequest>();

    public struct MeshRequest
    {
        public int width;
        public int height;

        public int levelOfDetail;

        public float voxelScale;

        public NativeArray<int> textureIndices; // -> index of zero always means no texture or nonsolid
        public NativeArray<int> cellIDs;

        public List<Texture2D> textures;
        public List<Vector4> animationInfo;

        public MeshFilter mFilter;
        public MeshRenderer mRenderer;
        public MeshCollider mCollider;

        public System.Action onRequestProcessed;

        public enum Type
        {
            RenderMesh,
            CollisionMesh
        }

        public Type type;

        public MeshRequest(int width, int height, float voxelScale, int levelOfDetail, NativeArray<int> textureIndices, 
            NativeArray<int> cellIDs, List<Texture2D> textures, List<Vector4> animationInfo, 
            MeshFilter mFilter, MeshRenderer mRenderer, MeshCollider mCollider, Type type, System.Action onRequestProcessed = null)
        {
            this.width = width;
            this.height = height;
            this.levelOfDetail = levelOfDetail;
            this.voxelScale = voxelScale;
            this.textureIndices = textureIndices;
            this.cellIDs = cellIDs;
            this.textures = textures;
            this.animationInfo = animationInfo;
            this.mFilter = mFilter;
            this.mRenderer = mRenderer;
            this.mCollider = mCollider;
            this.type = type;
            this.onRequestProcessed = onRequestProcessed;
        }
    }

    public struct CollisionMeshRequest
    {
        public int width;
        public int height;

        public NativeArray<int> textureIndices; // -> index of zero always means no texture or nonsolid
        public NativeArray<int> cellIDs;

        public MeshCollider mCollider;

        public CollisionMeshRequest(int width, int height, NativeArray<int> textureIndices, NativeArray<int> cellIDs, MeshCollider mCollider)
        {
            this.width = width;
            this.height = height;
            this.textureIndices = textureIndices;
            this.cellIDs = cellIDs;
            this.mCollider = mCollider;
        }
    }

    //Requested meshes
    private Mesh mesh;

    //Debug Info
    public RequestTimeInfo requestTimeInfo;

    [System.Serializable]
    public class RequestTimeInfo
    {
        public float requestTime = 0;
        private float renderTime = 0;
        private float collisionTime = 0;

        private int processedRequests = 0;
        private int processedRenderRequests = 0;
        private int processedCollisionRequests = 0;

        public int ProcessedRequests
        {
            get
            {
                return processedRequests;
            }
        }

        public float averageTimePerRequest;
        public float averageTimePerRenderRequest;
        public float averageTimePerCollisionRequest;

        public void AddRenderTime(float mms)
        {
            requestTime += mms;
            renderTime += mms;
            processedRequests++;
            processedRenderRequests++;

            averageTimePerRequest = (requestTime / processedRequests);
            averageTimePerRenderRequest = (renderTime / processedRenderRequests);            
        }

        public void AddCollisionTime(float mms)
        {
            requestTime += mms;
            collisionTime += mms;
            processedRequests++;
            processedCollisionRequests++;

            averageTimePerRequest = (requestTime / processedRequests);
            averageTimePerCollisionRequest = (collisionTime / processedCollisionRequests);            
        }
    }

    private void Start()
    {
        Initialize();

        allowProcessingMeshRequests = true;
        //StartCoroutine(ProcessMeshRequestsCo());


    }

    private void Update()
    {

    }

    private void OnDisable()
    {
        startEnds.Dispose();
        DisposeOfMeshArrays();
        //densityValues.Dispose();
    }

    private void Initialize()
    {
        var timer = new System.Diagnostics.Stopwatch();
        timer.Start();
        float startTime = Time.realtimeSinceStartup;
        float mms;        

        mms = (Time.realtimeSinceStartup - startTime) * 1000;//timer.ElapsedMilliseconds

        //print("Initializtion of texture indices took " + mms.ToString("0.00") + " mms");

        int maxStartEndsSize = 32 * 32 * 384 / 2;

        startEnds = new NativeList<SliceJob.StartEnd>(maxStartEndsSize, Allocator.Persistent);        

        mms = (Time.realtimeSinceStartup - startTime) * 1000;//timer.ElapsedMilliseconds

        print("Total Initializtion took " + mms.ToString("0.00") + " mms");
    }    

    private void DisposeOfMeshArrays()
    {
        if(vertices.IsCreated)
            vertices.Dispose();

        if (normals.IsCreated)
            normals.Dispose();

        if (uvs.IsCreated)
            uvs.Dispose();

        if (indices.IsCreated)
            indices.Dispose();
    }

    public void StartProcessingMeshRequests()
    {
        StartCoroutine(ProcessMeshRequestsCo());
    }

    private IEnumerator ProcessMeshRequestsCo()
    {
        processingMeshRequest = false;

        while (allowProcessingMeshRequests)
        {
            if(!processingMeshRequest && (meshRequests.Count > 0 || priorityMeshRequests.Count > 0))
            {
                if (completeRequestsImmediatley)
                {
                    ProcessNextMeshRequestImmediately();
                }
                else
                {
                    //StartCoroutine(ProcessNextMeshRequest());
                    StartCoroutine(ProcessNextMeshRequestLOD());
                }
                
            }

            yield return null;
        }

        yield return null;
    }

    private IEnumerator ProcessNextMeshRequest()
    {
        float startTime = Time.realtimeSinceStartup;
        float mms;

        //print("Started next render request");

        processingMeshRequest = true;
        var request = meshRequests.Dequeue();

        var sliceHandle = StartSliceJob(request.width, request.height, request.textureIndices, request.cellIDs, request.type == MeshRequest.Type.CollisionMesh);

        float sliceStartTime = Time.realtimeSinceStartup;
        //Force completion if the time per job take longer than this time
        float maxTime = targetJobCompletion / 1000f / 2f;


        while (!sliceHandle.IsCompleted && Time.realtimeSinceStartup - sliceStartTime < maxTime)
        {
            yield return null;
        }

        //print("Render Slice Job complete");

        sliceHandle.Complete();

        mesh = new Mesh();
        mesh.indexFormat = IndexFormat.UInt32;        

        var meshHandle = StartMeshJob(request.width, request.height, new float3(request.voxelScale), request.textureIndices, request.cellIDs, request.type == MeshRequest.Type.CollisionMesh);

        sliceStartTime = Time.realtimeSinceStartup;        

        while (!meshHandle.IsCompleted && Time.realtimeSinceStartup - sliceStartTime < maxTime)
        {
            yield return null;
        }

        //print("Render Mesh Job complete");

        meshHandle.Complete();

        int numVertsInRenderMesh = vertices.Length;

        mesh.SetVertices(vertices);
        mesh.SetIndices(indices, MeshTopology.Triangles, 0);
        mesh.SetNormals(normals);
        mesh.RecalculateBounds();

        if (request.type == MeshRequest.Type.RenderMesh)
        {
            mesh.SetUVs(0, uvs);

            request.mFilter.sharedMesh = mesh;
            SetMaterialProperties(request.mRenderer, request.textures, request.animationInfo);
            request.mRenderer.enabled = true;
        }
        else
        {
            request.mCollider.sharedMesh = mesh;
        }


        processingMeshRequest = false;

        mms = (Time.realtimeSinceStartup - startTime) * 1000;

        bool printMessages = false;

        if (printMessages)
        {
            if (request.type == MeshRequest.Type.RenderMesh)
                print("Processed render mesh request with " + numVertsInRenderMesh + " vertices in " + mms.ToString("0.00") + " mms");
            else
                print("Processed collision mesh request with " + numVertsInRenderMesh + " vertices in " + mms.ToString("0.00") + " mms");
        }

        if(request.type == MeshRequest.Type.RenderMesh)
        {
            requestTimeInfo.AddRenderTime(mms);
        }
        else
        {
            requestTimeInfo.AddCollisionTime(mms);
        }

        request.onRequestProcessed?.Invoke();

        yield return null;
    }

    private IEnumerator ProcessNextMeshRequestLOD()
    {
        float startTime = Time.realtimeSinceStartup;
        float mms;

        processingMeshRequest = true;
        MeshRequest request;// = meshRequests.Dequeue();

        if(priorityMeshRequests.Count > 0)
        {
            request = priorityMeshRequests.Dequeue();
        }
        else  
        {
            request = meshRequests.Dequeue();
        }

        //int levelOfDetail = request.levelOfDetail;

        //if(requestTimeInfo.ProcessedRequests < 16 * 2 * 4)
        //{
        //    levelOfDetail = 4;
        //}
        //else if (requestTimeInfo.ProcessedRequests < 16 * 2 * 8)
        //{
        //    levelOfDetail = 2;
        //}
        //else
        //{
        //    levelOfDetail = 1;
        //}

        NativeArray<int> cellIDsLOD;

        int widthLOD = request.width / request.levelOfDetail;
        int heightLOD = request.height;// / levelOfDetail;
        int numOfCellsLOD = (widthLOD + 2) * (widthLOD + 2) * heightLOD;

        if (request.levelOfDetail > 1)
        {
            cellIDsLOD = new NativeArray<int>(numOfCellsLOD, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            for (int y = 0; y < heightLOD; y++)
            {
                for (int z = 0; z < widthLOD; z++)
                {
                    for (int x = 0; x < widthLOD; x++)
                    {
                        cellIDsLOD[LinearIndex(x + 1, y, z + 1, widthLOD + 2)] =
                            request.cellIDs[LinearIndex(x * request.levelOfDetail + 1, y, z * request.levelOfDetail + 1, request.width + 2)];
                    }
                }
            }
        }
        else
        {
            cellIDsLOD = new NativeArray<int>(0, Allocator.Persistent);
        }

        var sliceHandle = StartSliceJob(widthLOD, request.height, request.textureIndices, 
            request.levelOfDetail > 1 ? cellIDsLOD : request.cellIDs, request.type == MeshRequest.Type.CollisionMesh);

        float sliceStartTime = Time.realtimeSinceStartup;
        //Force completion if the time per job take longer than this time
        float maxTime = 25 / 1000f / 2f;


        while (!sliceHandle.IsCompleted && Time.realtimeSinceStartup - sliceStartTime < maxTime)
        {
            yield return null;
        }

        //print("Render Slice Job complete");

        sliceHandle.Complete();

        mesh = new Mesh();
        mesh.indexFormat = IndexFormat.UInt32;

        var meshHandle = StartMeshJob(widthLOD, request.height, 
            new Vector3(request.voxelScale * request.levelOfDetail, request.voxelScale, request.voxelScale * request.levelOfDetail),
            request.textureIndices, request.levelOfDetail > 1 ? cellIDsLOD : request.cellIDs, request.type == MeshRequest.Type.CollisionMesh);

        sliceStartTime = Time.realtimeSinceStartup;

        while (!meshHandle.IsCompleted && Time.realtimeSinceStartup - sliceStartTime < maxTime)
        {
            yield return null;
        }

        //print("Render Mesh Job complete");

        meshHandle.Complete();

        int numVertsInRenderMesh = vertices.Length;

        mesh.SetVertices(vertices);
        mesh.SetIndices(indices, MeshTopology.Triangles, 0);
        mesh.SetNormals(normals);
        mesh.RecalculateBounds();

        if (request.type == MeshRequest.Type.RenderMesh)
        {
            mesh.SetUVs(0, uvs);

            request.mFilter.sharedMesh = mesh;
            SetMaterialProperties(request.mRenderer, request.textures, request.animationInfo);
            request.mRenderer.enabled = true;
        }
        else
        {
            request.mCollider.sharedMesh = mesh;
        }


        processingMeshRequest = false;

        mms = (Time.realtimeSinceStartup - startTime) * 1000;

        bool printMessages = false;

        if (printMessages)
        {
            if (request.type == MeshRequest.Type.RenderMesh)
                print("Processed render mesh request with " + numVertsInRenderMesh + " vertices in " + mms.ToString("0.00") + " mms");
            else
                print("Processed collision mesh request with " + numVertsInRenderMesh + " vertices in " + mms.ToString("0.00") + " mms");
        }

        if (request.type == MeshRequest.Type.RenderMesh)
        {
            requestTimeInfo.AddRenderTime(mms);
        }
        else
        {
            requestTimeInfo.AddCollisionTime(mms);
        }

        if(cellIDsLOD.IsCreated)
            cellIDsLOD.Dispose();                

        request.onRequestProcessed?.Invoke();

        yield return null;
    }

    private void ProcessNextMeshRequestImmediately()
    {
        float startTime = Time.realtimeSinceStartup;
        float mms;

        //print("Started next render request");

        processingMeshRequest = true;
        var request = meshRequests.Dequeue();

        //int levelOfDetail = requestTimeInfo.ProcessedRequests < 16 * 4 ? 2 : 1;

        //NativeArray<int> cellIDsLOD;

        //int widthLOD = request.width / levelOfDetail;
        //int heightLOD = request.height / levelOfDetail;
        //int numOfCellsLOD = (widthLOD + 2) * (widthLOD + 2) * heightLOD;
        //cellIDsLOD = new NativeArray<int>(numOfCellsLOD, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        ////int numSlices = heightLOD * 2 + widthLOD * 4;

        //for (int y = 0; y < heightLOD; y++)
        //{
        //    for (int z = 0; z < widthLOD; z++)
        //    {
        //        for (int x = 0; x < widthLOD; x++)
        //        {
        //            cellIDsLOD[LinearIndex(x + 1, y, z + 1, widthLOD + 2)] = 
        //                request.cellIDs[LinearIndex(x * levelOfDetail + 1, y * levelOfDetail, z * levelOfDetail + 1, request.width + 2)];
        //        }
        //    }
        //}

        ////var sliceHandle = StartSliceJob(widthLOD, heightLOD, request.textureIndices, cellIDsLOD, request.type == MeshRequest.Type.CollisionMesh);
        var sliceHandle = StartSliceJob(request.width, request.height, request.textureIndices, request.cellIDs, request.type == MeshRequest.Type.CollisionMesh);

        //print("Render Slice Job complete");

        sliceHandle.Complete();

        mesh = new Mesh();
        mesh.indexFormat = IndexFormat.UInt32;

        //var meshHandle = StartMeshJob(widthLOD, heightLOD, request.voxelScale * levelOfDetail, 
        //    request.textureIndices, cellIDsLOD, request.type == MeshRequest.Type.CollisionMesh);
        var meshHandle = StartMeshJob(request.width, request.height, new float3(request.voxelScale),
              request.textureIndices, request.cellIDs, request.type == MeshRequest.Type.CollisionMesh);

        //print("Render Mesh Job complete");

        meshHandle.Complete();

        int numVertsInRenderMesh = vertices.Length;

        mesh.SetVertices(vertices);
        mesh.SetIndices(indices, MeshTopology.Triangles, 0);
        mesh.SetNormals(normals);

        if (request.type == MeshRequest.Type.RenderMesh)
        {
            mesh.SetUVs(0, uvs);

            request.mFilter.sharedMesh = mesh;
            SetMaterialProperties(request.mRenderer, request.textures, request.animationInfo);
            request.mRenderer.enabled = true;
        }
        else
        {
            request.mCollider.sharedMesh = mesh;
        }


        processingMeshRequest = false;

        mms = (Time.realtimeSinceStartup - startTime) * 1000;

        bool printMessages = false;

        if (printMessages)
        {
            if (request.type == MeshRequest.Type.RenderMesh)
                print("Processed render mesh request with " + numVertsInRenderMesh + " vertices in " + mms.ToString("0.00") + " mms");
            else
                print("Processed collision mesh request with " + numVertsInRenderMesh + " vertices in " + mms.ToString("0.00") + " mms");
        }

        if (request.type == MeshRequest.Type.RenderMesh)
        {
            requestTimeInfo.AddRenderTime(mms);
        }
        else
        {
            requestTimeInfo.AddCollisionTime(mms);
        }

        request.onRequestProcessed?.Invoke();

        //cellIDsLOD.Dispose();
    }

    private JobHandle StartSliceJob(int width, int height, NativeArray<int> textureIndices, NativeArray<int> cellIDs, bool generateCollisionMesh = false)
    {

        startEnds.Clear();

        int firstNonZero = 0;
        for (int i = cellIDs.Length - 1; i >= 0; i -= 6)
        {
            if (cellIDs[i] > 0)
            {
                firstNonZero = i;
                break;
            }
        }

        int3 pos = LinearIndexToXYZInt3(firstNonZero, width);
        int highestY = pos.y + 2;

        highestY = Mathf.Clamp(highestY, 1, height);

        int numSlices = highestY * 2 + width * 4;

        //int levelOfDetail = 2;

        //NativeArray<int> cellIDsLOD;

        //int widthLOD = width / levelOfDetail;
        //int heightLOD = height / levelOfDetail;
        //int numOfCellsLOD = (widthLOD + 2) * (widthLOD + 2) * heightLOD;
        //cellIDsLOD = new NativeArray<int>(numOfCellsLOD, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        //numSlices = heightLOD * 2 + widthLOD * 4;
        
        //for (int y = 0; y < heightLOD; y++)
        //{
        //    for (int z = 0; z < widthLOD; z++)
        //    {
        //        for (int x = 0; x < widthLOD; x++)
        //        {
        //            cellIDsLOD[LinearIndex(x + 1, y, z + 1, widthLOD + 2)] = cellIDs[LinearIndex(x * 2 + 1, y * 2, z * 2 + 1, width + 2)];                    
        //        }
        //    }
        //}


        var job = new SliceJob()
        {
            width = width,
            height = highestY,            
            MAX_START_END_SIZE = 128,
            cellIDs = cellIDs,
            textureIndices = textureIndices,
            startEnds = startEnds.AsParallelWriter(),
            generateCollisionMesh = generateCollisionMesh
        };
        var handle = job.Schedule(numSlices, width);

        return handle;

        //if(levelOfDetail == 1)
        //{
        //    var job = new SliceJob()
        //    {
        //        width = width,
        //        height = highestY,
        //        MAX_START_END_SIZE = 128,
        //        cellIDs = cellIDs,
        //        textureIndices = textureIndices,
        //        startEnds = startEnds.AsParallelWriter(),
        //        generateCollisionMesh = generateCollisionMesh
        //    };
        //    var handle = job.Schedule(numSlices, width);

        //    return handle;
        //}
        //else
        //{
        //    int widthLOD = width / levelOfDetail;
        //    int heightLOD = highestY / levelOfDetail;
        //    int numOfCellsLOD = widthLOD * widthLOD * heightLOD;
        //    cellIDsLOD = new NativeArray<int>(numOfCellsLOD, Allocator.Persistent);

        //    numSlices = heightLOD * 2 + widthLOD * 4;


        //    int vIndex = 0;
        //    for (int y = 0; y < highestY; y += 2)
        //    {
        //        for (int z = -1; z <= width; z += 2)
        //        {
        //            for (int x = -1; x <= width; x += 2)
        //            {
        //                cellIDsLOD[vIndex] = cellIDs[LinearIndex(x + 1, y, z + 1, width + 2)];


        //                vIndex++;
        //            }
        //        }
        //    }

        //    print("Original size: " + new Vector2Int(width, highestY) + " vs lod size: " + new Vector2Int(widthLOD, heightLOD));

        //    var job = new SliceJob()
        //    {
        //        width = widthLOD,
        //        height = heightLOD,
        //        MAX_START_END_SIZE = 128,
        //        cellIDs = cellIDsLOD,
        //        textureIndices = textureIndices,
        //        startEnds = startEnds.AsParallelWriter(),
        //        generateCollisionMesh = generateCollisionMesh
        //    };
        //    var handle = job.Schedule(numSlices, width);

        //    return handle;
        //}        
    }

    private JobHandle StartMeshJob(int width, int height, Vector3 voxelScale, NativeArray<int> textureIndices, NativeArray<int> cellIDs, bool generateCollisionMesh = false)
    {
        int numVertices = startEnds.Length * 4;
        int numIndices = startEnds.Length * 6;

        if (numberOfStartEndsLastMeshGen != startEnds.Length)
        {
            DisposeOfMeshArrays();

            vertices = new NativeArray<Vector3>(numVertices, Allocator.Persistent);
            normals = new NativeArray<Vector3>(numVertices, Allocator.Persistent);
            uvs = new NativeArray<Vector3>(numVertices, Allocator.Persistent);
            indices = new NativeArray<int>(numIndices, Allocator.Persistent);
        }

        numberOfStartEndsLastMeshGen = startEnds.Length;

        var job = new MeshBuildJob()
        {
            width = width,
            height = height,
            voxelScale = voxelScale,
            numberOfStartEnds = startEnds.Length,
            cellIDs = cellIDs,
            textureIndices = textureIndices,
            startEnds = startEnds,
            vertices = vertices,
            normals = normals,
            uvs = uvs,
            indices = indices,
            generateCollisionMesh = generateCollisionMesh
        };

        var handle = job.Schedule(numIndices, 16 * 6);

        return handle;        
    }

    public void RequestMesh(MeshRequest request)
    {        
        meshRequests.Enqueue(request);
    }

    public void RequestPriorityMesh(MeshRequest request)
    {
        priorityMeshRequests.Enqueue(request);
    }

    public void RequestMeshImmediately(MeshRequest request)
    {
        float startTime = Time.realtimeSinceStartup;
        float mms;

        //print("Started next render request");

        processingMeshRequest = true;

        var sliceHandle = StartSliceJob(request.width, request.height, request.textureIndices, request.cellIDs, request.type == MeshRequest.Type.CollisionMesh);

        //print("Render Slice Job complete");

        sliceHandle.Complete();

        mesh = new Mesh();
        mesh.indexFormat = IndexFormat.UInt32;

        var meshHandle = StartMeshJob(request.width, request.height, new float3(request.voxelScale), 
            request.textureIndices, request.cellIDs, request.type == MeshRequest.Type.CollisionMesh);
        meshHandle.Complete();

        int numVertsInRenderMesh = vertices.Length;

        mesh.SetVertices(vertices);
        mesh.SetIndices(indices, MeshTopology.Triangles, 0);
        mesh.SetNormals(normals);
        mesh.RecalculateBounds();

        if (request.type == MeshRequest.Type.RenderMesh)
        {
            mesh.SetUVs(0, uvs);

            request.mFilter.sharedMesh = mesh;
            SetMaterialProperties(request.mRenderer, request.textures, request.animationInfo);
            request.mRenderer.enabled = true;
        }
        else
        {
            request.mCollider.sharedMesh = mesh;
        }


        processingMeshRequest = false;

        mms = (Time.realtimeSinceStartup - startTime) * 1000;

        bool printMessages = false;

        if (printMessages)
        {
            if (request.type == MeshRequest.Type.RenderMesh)
                print("Processed render mesh request with " + numVertsInRenderMesh + " vertices in " + mms.ToString("0.00") + " mms");
            else
                print("Processed collision mesh request with " + numVertsInRenderMesh + " vertices in " + mms.ToString("0.00") + " mms");
        }

        if (request.type == MeshRequest.Type.RenderMesh)
        {
            requestTimeInfo.AddRenderTime(mms);
        }
        else
        {
            requestTimeInfo.AddCollisionTime(mms);
        }

        request.onRequestProcessed?.Invoke();

    }

    public void SetMaterialProperties(MeshRenderer meshRenderer, List<Texture2D> textures, List<Vector4> animationInfo)
    {
        if (meshRenderer == null)
        {
            return;
        }

        Texture2DArray array = new Texture2DArray(textures[0].width, textures[0].height, textures.Count, textures[0].format, false);

        for (int i = 0; i < textures.Count; i++)
        {
            array.SetPixels(textures[i].GetPixels(), i);
        }

        array.filterMode = FilterMode.Point;
        array.wrapMode = TextureWrapMode.Repeat;
        array.Apply();

        MaterialPropertyBlock props = new MaterialPropertyBlock();
        props.SetTexture("_MainTex", array);
        props.SetFloat("_ArrayLength", textures.Count);
        props.SetVectorArray("_AnimationInfo", animationInfo);

        //Debug.Log("Created Texture array with " + array.depth + " textures");

        meshRenderer.SetPropertyBlock(props);

        //Debug.Log("Setting material props, animation data length: " + animationInfo.Count);

    }

    [BurstCompile]
    struct SliceJob : IJobParallelFor
    {
        [ReadOnly] [NativeDisableParallelForRestriction]
        public int MAX_START_END_SIZE;

        public int width;
        public int height;

        [ReadOnly] [NativeDisableParallelForRestriction] 
        public NativeArray<int> cellIDs;

        [ReadOnly] [NativeDisableParallelForRestriction]
        public NativeArray<int> textureIndices;

        public NativeList<StartEnd>.ParallelWriter startEnds;

        public bool generateCollisionMesh;

        public struct StartEnd
        {
            public int2 start;
            public int2 end;

            public int axisIndex;

            public int faceIndex;            

            public int2 SizeInts
            {
                get
                {
                    return (end + new int2(1, 1)) - start;
                }
            }

            public Vector2 Size
            {
                get
                {
                    return (end + new float2(1, 1)) - start;
                }
            }

            public Vector2 Center
            {
                get
                {
                    return new Vector2(start.x, start.y) + Size / 2f;
                }
            }

            public Vector3 GetCorner(int index)
            {
                Vector2 size = Size;

                switch (faceIndex)
                {
                    default:
                    case 0:
                        switch (index)
                        {
                            default:
                            case 0:
                                return new Vector3(start.x, axisIndex + 1, start.y);
                            case 1:
                                return new Vector3(start.x, axisIndex + 1, start.y + size.y);
                            case 2:
                                return new Vector3(start.x + size.x, axisIndex + 1, start.y + size.y);
                            case 3:
                                return new Vector3(start.x + size.x, axisIndex + 1, start.y);
                        }
                    case 1:
                        switch (index)
                        {
                            default:
                            case 3:
                                return new Vector3(start.x, axisIndex, start.y);                                
                            case 2:
                                return new Vector3(start.x, axisIndex, start.y + size.y);
                            case 1:
                                return new Vector3(start.x + size.x, axisIndex, start.y + size.y);
                            case 0:
                                return new Vector3(start.x + size.x, axisIndex, start.y);
                        }
                        
                    case 2:
                        switch (index)
                        {
                            default:
                            case 3:
                                return new Vector3(axisIndex, start.y, start.x);
                            case 2:
                                return new Vector3(axisIndex, start.y + size.y, start.x);
                            case 1:
                                return new Vector3(axisIndex, start.y + size.y, start.x + size.x);
                            case 0:
                                return new Vector3(axisIndex, start.y, start.x + size.x);
                        }
                    case 3:
                        switch (index)
                        {
                            default:
                            case 0:
                                return new Vector3(axisIndex + 1, start.y, start.x);
                            case 1:
                                return new Vector3(axisIndex + 1, start.y + size.y, start.x);
                            case 2:
                                return new Vector3(axisIndex + 1, start.y + size.y, start.x + size.x);
                            case 3:
                                return new Vector3(axisIndex + 1, start.y, start.x + size.x);
                        }
                    case 4:
                        switch (index)
                        {
                            default:
                            case 3:
                                return new Vector3(start.x, start.y, axisIndex + 1);
                            case 2:
                                return new Vector3(start.x, start.y + size.y, axisIndex + 1);
                            case 1:
                                return new Vector3(start.x + size.x, start.y + size.y, axisIndex + 1);
                            case 0:
                                return new Vector3(start.x + size.x, start.y, axisIndex + 1);
                        }
                    case 5:
                        switch (index)
                        {
                            default:
                            case 0:
                                return new Vector3(start.x, start.y, axisIndex);
                            case 1:
                                return new Vector3(start.x, start.y + size.y, axisIndex);
                            case 2:
                                return new Vector3(start.x + size.x, start.y + size.y, axisIndex);
                            case 3:
                                return new Vector3(start.x + size.x, start.y, axisIndex);
                        }                        
                }                
            }

            public Vector3 GetUV(int index, int textureIndex)
            {
                Vector2 size = Size;

                switch (index)
                {
                    default:
                    case 0:
                        return new Vector3(0, 0, textureIndex);
                    case 1:
                        return new Vector3(0, size.y, textureIndex);
                    case 2:
                        return new Vector3(size.x, size.y, textureIndex);
                    case 3:
                        return new Vector3(size.x, 0, textureIndex);
                }
            }

            public StartEnd(int2 start, int2 end, int axisIndex, int faceIndex)
            {
                this.start = start;
                this.end = end;
                this.axisIndex = axisIndex;
                this.faceIndex = faceIndex;
            }

            public StartEnd(int startX, int startY, int endX, int endY, int axisIndex, int faceIndex)
            {
                this.start = new int2(startX, startY);
                this.end = new int2(endX, endY);
                this.axisIndex = axisIndex;
                this.faceIndex = faceIndex;
            }

            public override string ToString()
            {
                return start + ", " + end + ", FaceIndex: " + faceIndex;
            }
        }

        private bool InBounds(int x, int y, int z)
        {
            return x >= 0 && x < width && y >= 0 && y < height && z >= 0 && z < width;
        }

        private bool InOuterBounds(int x, int y, int z)
        {
            return x >= -1 && x <= width && y >= 0 && y < height && z >= -1 && z <= width;
        }

        private int GetTextureIndex(int x, int y, int z, int faceIndex)
        {
            if (!InOuterBounds(x, y, z))
            {
                return 0;
            }

            int cellID = cellIDs[LinearIndex(x + 1, y, z + 1, width + 2)];

            if(cellID == 0)
            {
                return 0;
            }            

            return textureIndices[(cellID - 1) * 6 + faceIndex % 6];
        }

        private bool IsSolid(int x, int y, int z)
        {
            return GetTextureIndex(x, y, z, 0) > 0;
        }

        private bool FaceIsVisible(int x, int y, int z, int faceIndex)
        {
            if (!IsSolid(x, y, z) || !InBounds(x, y, z))
            {
                return false;
            }

            switch (faceIndex)
            {
                default: //Up
                case 0:
                    if (!IsSolid(x, y + 1, z))
                    {
                        return true;
                    }
                    break;
                case 1: //Down
                    if (!IsSolid(x, y - 1, z))
                    {
                        return true;
                    }
                    break;
                case 2: //Left
                    if (!IsSolid(x - 1, y, z))
                    {
                        return true;
                    }
                    break;
                case 3: //Right
                    if (!IsSolid(x + 1, y, z))
                    {
                        return true;
                    }
                    break;
                case 4: //Forward
                    if (!IsSolid(x, y, z + 1))
                    {
                        return true;
                    }
                    break;
                case 5: //Back
                    if (!IsSolid(x, y, z - 1))
                    {
                        return true;
                    }
                    break;
            }

            return false;
        }

        private bool IsAnchor(int x, int y, int z, int faceIndex)
        {
            int anchorIndex = GetTextureIndex(x, y, z, faceIndex);

            if(anchorIndex == 0 || !FaceIsVisible(x, y, z, faceIndex))
            {
                return false;
            }            

            switch (faceIndex)
            {
                default: 
                case 0: //Up
                case 1: //Down
                    if (GetTextureIndex(x - 1, y, z, faceIndex) != anchorIndex && GetTextureIndex(x, y, z - 1, faceIndex) != anchorIndex)
                    {
                        return true;
                    }
                    break;
                case 2: //Left
                case 3: //Right
                    if (GetTextureIndex(x, y, z - 1, faceIndex) != anchorIndex && GetTextureIndex(x, y - 1, z, faceIndex) != anchorIndex)
                    {
                        return true;
                    }
                    break;
                case 4: //Forward
                case 5: //Back
                    if (GetTextureIndex(x - 1, y, z, faceIndex) != anchorIndex && GetTextureIndex(x, y - 1, z, faceIndex) != anchorIndex)
                    {
                        return true;
                    }
                    break;
            }

            return false;
        }                

        private bool Visited(int x, int y, int z, int faceIndex, NativeArray<bool> visited)
        {
            if(faceIndex < 2)
            {
                return visited[x + z * width];
            }
            else if (faceIndex < 4)
            {
                return visited[z + y * width];
            }
            else if (faceIndex < 6)
            {
                return visited[x + y * width];
            }

            return false;
        }

        private int GetFaceIndex(int index)
        {
            int faceIndex = 0;

            //Top
            if (index < height)
            {
                faceIndex = 0;
            }
            //Bottom
            else if (index < height * 2)
            {
                faceIndex = 1;
            }
            //Left
            else if (index < height * 2 + width)
            {
                faceIndex = 2;
            }
            //Right
            else if (index < height * 2 + width * 2)
            {
                faceIndex = 3;
            }
            //Forward
            else if (index < height * 2 + width * 3)
            {
                faceIndex = 4;
            }
            //Back
            else if (index < height * 2 + width * 4)
            {
                faceIndex = 5;
            }

            return faceIndex;
        }

        private int GetSliceIndex(int index)
        {
            int sliceIndex = 0;

            //Top
            if (index < height)
            {
                sliceIndex = index;
            }
            //Bottom
            else if (index < height * 2)
            {
                sliceIndex = index - height;
            }
            //Left
            else if (index < height * 2 + width)
            {
                sliceIndex = index - height * 2;
            }
            //Right
            else if (index < height * 2 + width * 2)
            {
                sliceIndex = index - height * 2 - width;
            }
            //Forward
            else if (index < height * 2 + width * 3)
            {
                sliceIndex = index - height * 2 - width * 2;
            }
            //Back
            else if (index < height * 2 + width * 4)
            {
                sliceIndex = index - height * 2 - width * 3;
            }
            else
            {
                return -1;
            }

            return sliceIndex;
        }

        private bool LCheck(int anchorX, int anchorY, int anchorZ, int faceIndex, int lSize, NativeArray<bool> visited)
        {
            //This function checks in an L shape to see if the cells match the anchor cell and are solid
            //[A] - Anchor
            //[0-4] Cells being checked
            //size - in this example is 4

            //[0][1][2][3][4]
            // |          [3]
            // |          [2]
            // |   size   [1]
            //[A]---------[0]

            if (!InBounds(anchorX, anchorY, anchorZ) || !FaceIsVisible(anchorX, anchorY, anchorZ, faceIndex) || Visited(anchorX, anchorY, anchorZ, faceIndex, visited))
            {
                return false;
            }

            if (lSize < 1)
            {
                return false;
            }

            lSize = Mathf.Clamp(lSize, 1, MAX_START_END_SIZE);

            if (lSize == 1)
            {
                return true;
            }

            int anchorID = GetTextureIndex(anchorX, anchorY, anchorZ, faceIndex);

            int x;
            int y;
            int z;

            if(faceIndex < 2)
            {
                y = anchorY;
                //Top of L
                z = anchorZ + lSize - 1;
                for (x = anchorX; x < anchorX + lSize; x ++)
                {
                    //Is the current cell solid? & not been visited? and the same ID as the anchor?
                    if (!FaceIsVisible(x, y, z, faceIndex) || Visited(x, y, z, faceIndex, visited)
                        || (!generateCollisionMesh && GetTextureIndex(x, y, z, faceIndex) != anchorID))
                    {
                        return false;
                    }
                }

                //Right of L
                x = anchorX + lSize - 1;
                for (z = anchorZ; z < anchorZ + lSize - 1; z ++)
                {
                    //Is the current cell solid? & not been visited? and the same ID as the anchor?
                    if (!FaceIsVisible(x, y, z, faceIndex) || Visited(x, y, z, faceIndex, visited)
                        || (!generateCollisionMesh && GetTextureIndex(x, y, z, faceIndex) != anchorID))
                    {
                        return false;
                    }
                }
            }
            else if (faceIndex < 4)
            {
                x = anchorX;
                //Top of L
                y = anchorY + lSize - 1;
                for (z = anchorZ; z < anchorZ + lSize; z ++)
                {
                    //Is the current cell solid? & not been visited? and the same ID as the anchor?
                    if (!FaceIsVisible(x, y, z, faceIndex) || Visited(x, y, z, faceIndex, visited)
                        || (!generateCollisionMesh && GetTextureIndex(x, y, z, faceIndex) != anchorID))
                    {
                        return false;
                    }
                }

                //Right of L
                z = anchorZ + lSize - 1;
                for (y = anchorY; y < anchorY + lSize - 1; y ++)
                {
                    //Is the current cell solid? & not been visited? and the same ID as the anchor?
                    if (!FaceIsVisible(x, y, z, faceIndex) || Visited(x, y, z, faceIndex, visited)
                        || (!generateCollisionMesh && GetTextureIndex(x, y, z, faceIndex) != anchorID))
                    {
                        return false;
                    }
                }
            }
            else if (faceIndex < 6)
            {
                z = anchorZ;
                //Top of L
                y = anchorY + lSize - 1;
                for (x = anchorX; x < anchorX + lSize; x ++)
                {
                    //Is the current cell solid? & not been visited? and the same ID as the anchor?
                    if (!FaceIsVisible(x, y, z, faceIndex) || Visited(x, y, z, faceIndex, visited)
                        || (!generateCollisionMesh && GetTextureIndex(x, y, z, faceIndex) != anchorID))
                    {
                        return false;
                    }
                }

                //Right of L
                x = anchorX + lSize - 1;
                for (y = anchorY; y < anchorY + lSize - 1; y ++)
                {
                    //Is the current cell solid? & not been visited? and the same ID as the anchor?
                    if (!FaceIsVisible(x, y, z, faceIndex) || Visited(x, y, z, faceIndex, visited)
                        || (!generateCollisionMesh && GetTextureIndex(x, y, z, faceIndex) != anchorID))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private bool HorizontalCheck(int anchorX, int anchorY, int anchorZ, int faceIndex, int hSize, int xWidth, NativeArray<bool> visited)
        {
            //This function checks in a horizontal line to see if the cells match the anchor cell and are solid
            //[A] - Anchor
            //[0-4] Cells being checked
            //size - in this example is 4

            //[0][1][2][3][4]
            // |           |
            // |           |
            // |   size    |
            //[A]----------|

            int anchorID = GetTextureIndex(anchorX, anchorY, anchorZ, faceIndex);

            if (!InBounds(anchorX, anchorY, anchorZ) || !FaceIsVisible(anchorX, anchorY, anchorZ, faceIndex) || Visited(anchorX, anchorY, anchorZ, faceIndex, visited))
            {
                return false;
            }

            if (hSize < 1)
            {
                return false;
            }

            hSize = Mathf.Clamp(hSize, 1, MAX_START_END_SIZE);

            if (hSize == 1)
            {
                return true;
            }

            int x;
            int y;
            int z;

            if (faceIndex < 2)
            {
                //Top of L
                y = anchorY;
                z = anchorZ + hSize - 1;
                for (x = anchorX; x < anchorX + xWidth; x ++)
                {
                    //Is the current cell solid? & not been visited? and the same ID as the anchor?
                    if (!FaceIsVisible(x, y, z, faceIndex) || Visited(x, y, z, faceIndex, visited)
                        || (!generateCollisionMesh && GetTextureIndex(x, y, z, faceIndex) != anchorID))
                    {
                        return false;
                    }
                }
            }
            else if (faceIndex < 4)
            {
                //Top of L
                x = anchorX;
                y = anchorY + hSize - 1;
                for (z = anchorZ; z < anchorZ + xWidth; z ++)
                {
                    //Is the current cell solid? & not been visited? and the same ID as the anchor?
                    if (!FaceIsVisible(x, y, z, faceIndex) || Visited(x, y, z, faceIndex, visited)
                        || (!generateCollisionMesh && GetTextureIndex(x, y, z, faceIndex) != anchorID))
                    {
                        return false;
                    }
                }                           
            }
            else if (faceIndex < 6)
            {
                //Top of L
                z = anchorZ;
                y = anchorY + hSize - 1;
                for (x = anchorX; x < anchorX + xWidth; x ++)
                {
                    //Is the current cell solid? & not been visited? and the same ID as the anchor?
                    if (!FaceIsVisible(x, y, z, faceIndex) || Visited(x, y, z, faceIndex, visited)
                        || (!generateCollisionMesh && GetTextureIndex(x, y, z, faceIndex) != anchorID))
                    {
                        return false;
                    }
                }                
            }

            return true;
        }

        private bool VerticalCheck(int anchorX, int anchorY, int anchorZ, int faceIndex, int vSize, int yWidth, NativeArray<bool> visited)
        {
            //This function checks in an vertical Line to see if the cells match the anchor cell and are solid
            //[A] - Anchor
            //[0-4] Cells being checked
            //size - in this example is 4

            // -----------[4]
            // |          [3]
            // |          [2]
            // |   size   [1]
            //[A]---------[0]

            int anchorID = GetTextureIndex(anchorX, anchorY, anchorZ, faceIndex);

            if (!InBounds(anchorX, anchorY, anchorZ) || !FaceIsVisible(anchorX, anchorY, anchorZ, faceIndex) || Visited(anchorX, anchorY, anchorZ, faceIndex, visited))
            {
                return false;
            }

            if (vSize < 1)
            {
                return false;
            }

            vSize = Mathf.Clamp(vSize, 1, MAX_START_END_SIZE);

            if (vSize == 1)
            {
                return true;
            }

            int x;
            int y;
            int z;

            if (faceIndex < 2)
            {
                //Right of L
                y = anchorY;
                x = anchorX + vSize - 1;
                for (z = anchorZ; z < anchorZ + yWidth; z ++)
                {
                    //Is the current cell solid? & not been visited? and the same ID as the anchor?
                    if (!FaceIsVisible(x, y, z, faceIndex) || Visited(x, y, z, faceIndex, visited)
                        || (!generateCollisionMesh && GetTextureIndex(x, y, z, faceIndex) != anchorID))
                    {
                        return false;
                    }
                }
            }
            else if (faceIndex < 4)
            {
                //Right of L
                x = anchorX;
                z = anchorZ + vSize - 1;
                for (y = anchorY; y < anchorY + yWidth; y ++)
                {
                    //Is the current cell solid? & not been visited? and the same ID as the anchor?
                    if (!FaceIsVisible(x, y, z, faceIndex) || Visited(x, y, z, faceIndex, visited)
                        || (!generateCollisionMesh && GetTextureIndex(x, y, z, faceIndex) != anchorID))
                    {
                        return false;
                    }
                }
            }
            else if (faceIndex < 6)
            {
                //Right of L
                z = anchorZ;
                x = anchorX + vSize - 1;
                for (y = anchorY; y < anchorY + yWidth; y ++)
                {
                    //Is the current cell solid? & not been visited? and the same ID as the anchor?
                    if (!FaceIsVisible(x, y, z, faceIndex) || Visited(x, y, z, faceIndex, visited)
                        || (!generateCollisionMesh && GetTextureIndex(x, y, z, faceIndex) != anchorID))
                    {
                        return false;
                    }
                }
            }


            return true;
        }

        private StartEnd ExpandDiagonally(StartEnd startEnd, NativeArray<bool> visited)
        {
            int lSize = 0;

            int3 pos3D;

            if(startEnd.faceIndex < 2)
            {
                pos3D = new int3(startEnd.start.x, startEnd.axisIndex, startEnd.start.y);
            }
            else if (startEnd.faceIndex < 4)
            {
                pos3D = new int3(startEnd.axisIndex, startEnd.start.y, startEnd.start.x);
            }
            else
            {
                pos3D = new int3(startEnd.start.x, startEnd.start.y, startEnd.axisIndex);
            }

            //Find the biggest L size
            for (int l = 1; l <= MAX_START_END_SIZE; l ++)
            {
                if (!LCheck(pos3D.x, pos3D.y, pos3D.z, startEnd.faceIndex, l, visited))
                {
                    break;
                }

                lSize = l;
            }

            //Resize end to reflect
            //startEnd.end = startEnd.start + new int2(1, 1) * (lSize - 1);

            return new StartEnd(startEnd.start, startEnd.start + new int2(1, 1) * (lSize - 1), startEnd.axisIndex, startEnd.faceIndex);
        }

        private StartEnd ExpandVertically(StartEnd startEnd, NativeArray<bool> visited)
        {
            int2 startEndSize = startEnd.SizeInts;

            int vSize = startEndSize.y;

            int3 pos3D;

            if (startEnd.faceIndex < 2)
            {
                pos3D = new int3(startEnd.start.x, startEnd.axisIndex, startEnd.start.y);
            }
            else if (startEnd.faceIndex < 4)
            {
                pos3D = new int3(startEnd.axisIndex, startEnd.start.y, startEnd.start.x);
            }
            else
            {
                pos3D = new int3(startEnd.start.x, startEnd.start.y, startEnd.axisIndex);
            }

            //Find the biggest V size
            for (int v = startEndSize.y + 1; v <= MAX_START_END_SIZE; v ++)
            {
                if (!HorizontalCheck(pos3D.x, pos3D.y, pos3D.z, startEnd.faceIndex, v, startEndSize.x, visited))
                {
                    break;
                }

                vSize = v;
            }

            //Resize end to reflect
            //startEnd.end = new Vector2Int(startEnd.end.x, startEnd.start.y + vSize - 1);

            return new StartEnd(startEnd.start, new int2(startEnd.end.x, startEnd.start.y + vSize - 1), startEnd.axisIndex, startEnd.faceIndex);
        }

        private StartEnd ExpandHorizontally(StartEnd startEnd, NativeArray<bool> visited)
        {
            int2 startEndSize = startEnd.SizeInts;

            int hSize = startEndSize.x;

            int3 pos3D;

            if (startEnd.faceIndex < 2)
            {
                pos3D = new int3(startEnd.start.x, startEnd.axisIndex, startEnd.start.y);
            }
            else if (startEnd.faceIndex < 4)
            {
                pos3D = new int3(startEnd.axisIndex, startEnd.start.y, startEnd.start.x);
            }
            else
            {
                pos3D = new int3(startEnd.start.x, startEnd.start.y, startEnd.axisIndex);
            }

            //Find the biggest V size
            for (int h = startEndSize.x + 1; h <= MAX_START_END_SIZE; h ++)
            {
                if (!VerticalCheck(pos3D.x, pos3D.y, pos3D.z, startEnd.faceIndex, h, startEndSize.y, visited))
                {
                    break;
                }

                hSize = h;
            }

            //Resize end to reflect
            //startEnd.end = new Vector2Int(startEnd.start.x + hSize - 1, startEnd.end.y);
            return new StartEnd(startEnd.start, new int2(startEnd.start.x + hSize - 1, startEnd.end.y), startEnd.axisIndex, startEnd.faceIndex);
        }

        private void SetVisited(StartEnd startEnd, NativeArray<bool> visited)
        {
            for (int x = startEnd.start.x; x <= startEnd.end.x; x++)
            {
                for (int y = startEnd.start.y; y <= startEnd.end.y; y++)
                {
                    visited[x + y * width] = true;
                }
            }
        }

        private void ShouldExecute(int index)
        {

        }

        public void Execute(int index)
        {
            // total =      top + bottom + others
            int numSlices = height * 2 + width * 4;

            int faceIndex = GetFaceIndex(index);
            int sliceIndex = GetSliceIndex(index);

            if (index >= height * 2 + width * 4)
            {
                return;
            }

            int vSize = (faceIndex < 2) ? width * width : width * height;

            NativeArray<bool> visited = new NativeArray<bool>(vSize, Allocator.Temp);

            for (int i = 0; i < vSize; i++)
            {
                visited[i] = false;
            }

            switch (faceIndex)
            {
                default:
                case 0:
                case 1:
                    for (int z = 0; z < width; z ++)
                    {
                        for (int x = 0; x < width; x ++)
                        {

                            if (!Visited(x, sliceIndex, z, faceIndex, visited) && FaceIsVisible(x, sliceIndex, z, faceIndex))
                            {
                                StartEnd startEnd = new StartEnd(x, z, x, z, sliceIndex, faceIndex);

                                startEnd = ExpandDiagonally(startEnd, visited);
                                startEnd = ExpandVertically(startEnd, visited);
                                startEnd = ExpandHorizontally(startEnd, visited);
                                SetVisited(startEnd, visited);

                                startEnds.AddNoResize(startEnd);
                            }
                        }
                    }
                    break;
                case 2:
                case 3:
                    for (int y = 0; y < height; y ++)
                    {
                        for (int z = 0; z < width; z ++)
                        {

                            if (!Visited(sliceIndex, y, z, faceIndex, visited) && FaceIsVisible(sliceIndex, y, z, faceIndex))
                            {
                                StartEnd startEnd = new StartEnd(z, y, z, y, sliceIndex, faceIndex);

                                startEnd = ExpandDiagonally(startEnd, visited);
                                startEnd = ExpandVertically(startEnd, visited);
                                startEnd = ExpandHorizontally(startEnd, visited);
                                SetVisited(startEnd, visited);

                                startEnds.AddNoResize(startEnd);                                
                            }
                        }
                    }
                    break;
                case 4:
                case 5:
                    for (int y = 0; y < height; y ++)
                    {
                        for (int x = 0; x < width; x ++)
                        {

                            if (!Visited(x, y, sliceIndex, faceIndex, visited) && FaceIsVisible(x, y, sliceIndex, faceIndex))
                            {
                                StartEnd startEnd = new StartEnd(x, y, x, y, sliceIndex, faceIndex);

                                startEnd = ExpandDiagonally(startEnd, visited);
                                startEnd = ExpandVertically(startEnd, visited);
                                startEnd = ExpandHorizontally(startEnd, visited);
                                SetVisited(startEnd, visited);

                                startEnds.AddNoResize(startEnd);
                            }
                        }
                    }
                    break;
            }

            visited.Dispose();

        }
    }

    [BurstCompile]
    struct MeshBuildJob : IJobParallelFor
    {
        public int width;
        public int height;

        public float3 voxelScale;

        public int numberOfStartEnds;
        public int NumberOfVertices
        {
            get
            {
                return numberOfStartEnds * 4;
            }
        }

        public int NumberOfIndices
        {
            get
            {
                return numberOfStartEnds * 6;
            }
        }

        [ReadOnly] [NativeDisableParallelForRestriction]
        public NativeList<SliceJob.StartEnd> startEnds;

        [ReadOnly] [NativeDisableParallelForRestriction]
        public NativeArray<int> cellIDs;

        [ReadOnly] [NativeDisableParallelForRestriction] 
        public NativeArray<int> textureIndices;

        public NativeArray<Vector3> vertices;
        public NativeArray<Vector3> uvs;
        public NativeArray<Vector3> normals;
        public NativeArray<int> indices;

        public bool generateCollisionMesh;

        private bool InBounds(int x, int y, int z)
        {
            return x >= 0 && x < width && y >= 0 && y < height && z >= 0 && z < width;
        }

        private bool InOuterBounds(int x, int y, int z)
        {
            return x >= -1 && x <= width && y >= 0 && y < height && z >= -1 && z <= width;
        }

        private int GetTextureIndex(int x, int y, int z, int faceIndex)
        {
            if (!InOuterBounds(x, y, z))
            {
                return 0;
            }

            int cellID = cellIDs[LinearIndex(x + 1, y, z + 1, width + 2)];

            if (cellID == 0)
            {
                return 0;
            }

            return textureIndices[(cellID - 1) * 6 + faceIndex % 6];
        }

        public void Execute(int index)
        {
            if(index >= NumberOfIndices)
            {
                return;
            }

            //Triangle arragement
            //1, 2, 3
            //1, 3, 0

            int triIndex = index % 6;
            int startIndex = index / 6 * 4;

            switch (triIndex)
            {
                default:
                case 0:
                    indices[index] = startIndex + 1;
                    break;
                case 1:
                    indices[index] = startIndex + 2;
                    break;
                case 2:
                    indices[index] = startIndex + 3;
                    break;
                case 3:
                    indices[index] = startIndex + 1;
                    break;
                case 4:
                    indices[index] = startIndex + 3;
                    break;
                case 5:
                    indices[index] = startIndex + 0;
                    break;
            }

            if (index >= NumberOfVertices)
            {
                return;
            }

            int cornerIndex = index % 4;
            int startEndIndex = index / 4;

            if(startEndIndex >= numberOfStartEnds)
            {
                return;
            }

            SliceJob.StartEnd startEnd = startEnds[startEndIndex];

            //vertices[index] = startEnd.GetCorner(cornerIndex) * voxelScale;
            Vector3 corner = startEnd.GetCorner(cornerIndex);
            vertices[index] = new Vector3(corner.x * voxelScale.x, corner.y * voxelScale.y, corner.z * voxelScale.z);

            int3 pos3D;

            if (startEnd.faceIndex < 2)
            {
                pos3D = new int3(startEnd.start.x, startEnd.axisIndex, startEnd.start.y);
            }
            else if (startEnd.faceIndex < 4)
            {
                pos3D = new int3(startEnd.axisIndex, startEnd.start.y, startEnd.start.x);
            }
            else
            {
                pos3D = new int3(startEnd.start.x, startEnd.start.y, startEnd.axisIndex);
            }

            int textureID = GetTextureIndex(pos3D.x, pos3D.y, pos3D.z, startEnd.faceIndex) - 1;

            if(!generateCollisionMesh)
                uvs[index] = startEnd.GetUV(cornerIndex, textureID);

            switch (startEnd.faceIndex)
            {
                default:
                case 0:
                    normals[index] = Vector3.up;
                    break;
                case 1:
                    normals[index] = Vector3.down;
                    break;
                case 2:
                    normals[index] = Vector3.left;
                    break;
                case 3:
                    normals[index] = Vector3.right;
                    break;
                case 4:
                    normals[index] = Vector3.forward;
                    break;
                case 5:
                    normals[index] = Vector3.back;
                    break;
            }



        }
    }

    /*
    private void PreformSlices()
    {
        var timer = new System.Diagnostics.Stopwatch();
        timer.Start();
        float startTime = Time.realtimeSinceStartup;
        float mms;
        
        StartSliceJob(width, height, textureIndices, cellIDs, false).Complete();

        mms = (Time.realtimeSinceStartup - startTime) * 1000;//timer.ElapsedMilliseconds        

        print("Preformed Slices, created " + startEnds.Length + " startEnds in " + mms + "mms");
    }
     
    Mesh CreateMesh()
    {
        var timer = new System.Diagnostics.Stopwatch();
        timer.Start();
        float startTime = Time.realtimeSinceStartup;
        float mms;

        Mesh newMesh = new Mesh();
        // Use 32 bit index buffer to allow water grids larger than ~250x250
        newMesh.indexFormat = IndexFormat.UInt32;

        StartMeshJob(width, height, textureIndices, cellIDs, false).Complete();    

        newMesh.SetVertices(vertices);
        //newMesh.SetTriangles(indices.ToArray(), 0);
        newMesh.SetIndices(indices, MeshTopology.Triangles, 0);
        newMesh.SetUVs(0, uvs);
        newMesh.SetNormals(normals);       

        mFilter.sharedMesh = newMesh;
        mCollider.sharedMesh = newMesh;
        SetMaterialProperties(mRenderer, textures, animationInfo);

        mms = (Time.realtimeSinceStartup - startTime) * 1000;//timer.ElapsedMilliseconds

        print("Mesh created with " + vertices.Length + " vertices in " + mms + " mms");

        return newMesh;
    }

    private void UpdateMesh()
    {
        var timer = new System.Diagnostics.Stopwatch();
        timer.Start();
        float startTime = Time.realtimeSinceStartup;
        float mms;

        StartMeshJob(width, height, textureIndices, cellIDs, false).Complete();        

        mesh.SetVertices(vertices);
        //mesh.SetTriangles(indices.ToArray(), 0);
        mesh.SetIndices(indices, MeshTopology.Triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.SetNormals(normals);

        mFilter = GetComponent<MeshFilter>();
        mCollider = GetComponent<MeshCollider>();
        mRenderer = GetComponent<MeshRenderer>();

        mFilter.sharedMesh = mesh;
        mCollider.sharedMesh = mesh;
        SetMaterialProperties(mRenderer, textures, animationInfo);

        mms = (Time.realtimeSinceStartup - startTime) * 1000;//timer.ElapsedMilliseconds

        print("Updated mesh with " + vertices.Length + " vertices in " + mms + " mms");        
    }
    */

    public static int LinearIndex(int x, int y, int z, int width)
    {
        return x + z * width + y * width * width;
    }

    public static Vector3Int LinearIndexToXYZ(int index, int width)
    {
        int sizeSqd = width * width;

        return new Vector3Int(index % sizeSqd % width, index / sizeSqd, index % sizeSqd / width);
    }

    public static int3 LinearIndexToXYZInt3(int index, int width)
    {
        int sizeSqd = width * width;

        return new int3(index % sizeSqd % width, index / sizeSqd, index % sizeSqd / width);
    }
}
