using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class VoxelP : MonoBehaviour
{
    public Vector2Int position;

    public float voxelScale = 0.25f;

    public int width; //width of the x and z of the chunk
    public int height; //Height of the chunk    

    [Range(1, 3)]
    public int levelOfDetail = 3;

    public int SimplificationAmount
    {
        get
        {
            switch (levelOfDetail)
            {
                default:
                case 1:
                    return 4;
                case 2:
                    return 2;
                case 3:
                    return 1;
            }
        }
    }

    [HideInInspector] public NativeArray<int> textureIndices; // -> index of zero always means no texture or nonsolid    
    [HideInInspector] public NativeArray<int> cellIDs;

    //mesh references        
    private MeshFilter mFilter;
    private MeshCollider mCollider;
    private MeshRenderer mRenderer;

    public Mesh RenderMesh
    {
        get
        {
            return mFilter.sharedMesh;
        }
    }

    public Mesh CollisionMesh
    {
        get
        {
            return mCollider.sharedMesh;
        }
    }

    //Voxel data
    public List<string> voxelIDs = new List<string>();
    private List<string> textureIDs = new List<string>();
    private List<Texture2D> textures = new List<Texture2D>();
    private List<Vector4> animationInfo = new List<Vector4>();

    public VoxelMeshGenerator meshGenerator;
    public VoxelDensityGenerator densityGenerator;

    public bool waitingForDensityValues = false;
    public bool waitingForMeshUpdate = false;      

    private void Start()
    {
        //densityGenerator.onDataAvalible -= OnDensityReady;
        //densityGenerator.onDataAvalible += OnDensityReady;

        //densityGenerator.noiseData.width = width + 2;
        //densityGenerator.noiseData.worldHeight = height;

        //densityGenerator.UpdateData();
        //densityGenerator.RequestData();


    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.O))
        {
            //int x = 8, z = 8;
            //for (int y = 0; y < height; y++)
            //{
            //    SetVoxel(new Vector3Int(x, y, z), "minecraft:stone");
            //    //cellIDs[LinearIndex(x + 1, y, z + 1, width + 2)] = 1;
            //}

            SetVoxelBoxFill(0, 100, 0, 32, 100, 32, "minecraft:animated");

            SetVoxelBoxFill(0, 250, 0, 32, 255, 32, "minecraft:quartz_pillar");

            SetVoxelBoxFill(0, 258, 0, 10, 258, 10, "minecraft:random_missing");

            SetVoxelBoxFill(0, 259, 0, 10, 259, 10, "minecraft:water");

            UpdateMesh();

            //meshGenerator.RequestMeshImmediately(request);
        }
    }

    private void OnDisable()
    {
        Dispose();
    }

    private void Dispose()
    {
        if (cellIDs.IsCreated)
            cellIDs.Dispose();

        if (textureIndices.IsCreated)
            textureIndices.Dispose();
    }

    public void OnDensityReady()
    {
        waitingForDensityValues = false;

        Initialize();

        UpdateMesh();
    }

    private void UpdateTextureList()
    {
        TextureDataCollection.UpdateCollectionsDict();
        VoxelDataCollection.UpdateCollectionsDict();

        string voxelID;
        string textureID;
        VoxelData vData;
        TextureData tData;

        textureIDs.Clear();
        textures.Clear();
        animationInfo.Clear();

        if(textureIndices.IsCreated && textureIndices.Length != voxelIDs.Count * 6)
        {
            textureIndices.Dispose();
            textureIndices = new NativeArray<int>(voxelIDs.Count * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }

        for (int vIndex = 0; vIndex < voxelIDs.Count; vIndex++)
        {
            voxelID = voxelIDs[vIndex];

            if (!VoxelDataCollection.ValidVoxelID(voxelID))
            {
                voxelID = "base:solid";
            }

            vData = VoxelDataCollection.GetVoxelData(voxelID);

            for (int faceIndex = 0; faceIndex < 6; faceIndex++)
            {
                textureID = vData.GetTextureID(faceIndex);

                if (textureID == "base:none")
                {
                    continue;
                }

                if (textureIDs.Contains(textureID))
                {
                    textureIndices[vIndex * 6 + faceIndex] = textureIDs.IndexOf(textureID) + 1;
                    continue;
                }

                //textureIDs.Add(textureID);
                tData = TextureDataCollection.GetTextureData(textureID);

                for (int tIndex = 0; tIndex < tData.textures.Count; tIndex++)
                {
                    textures.Add(tData.textures[tIndex]);
                    textureIDs.Add(tIndex == 0 ? textureID : textureID + "_frame_" + tIndex);
                    animationInfo.Add(tData.GetAnimationData());
                }

                textureIndices[vIndex * 6 + faceIndex] = textureIDs.IndexOf(textureID) + 1;
            }
        }

        //for (int i = 0; i < textureIndices.Length; i++)
        //{
        //    print("Texture Index (" + i + ") " + textureIndices[i]);
        //}
    }

    private void InitializeTextureIndices()
    {
        int numOfVoxels = width * width * height;
        int numOfVoxelsWithNeighbors = (width + 2) * (width + 2) * height;
        int numDiffVoxels = voxelIDs.Count;

        Dispose();

        cellIDs = new NativeArray<int>(numOfVoxelsWithNeighbors, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        textureIndices = new NativeArray<int>(6 * numDiffVoxels, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

        UpdateTextureList();

        densityGenerator.PopulateCellIDs(width, height, cellIDs);
        return;

        int vIndex = 0;
        float value;

        //for (int i = 0; i < numOfVoxelsWithNeighbors; i++)
        //{
        //    cellIDs[i] = 1;
        //}

        bool wentOutOfBounds = false;

        for (int y = 0; y < height; y++)
        {
            for (int z = -1; z <= width; z++)
            {
                for (int x = -1; x <= width; x++)
                {
                    vIndex = LinearIndex(x + 1, y, z + 1, width + 2);

                    if(!wentOutOfBounds && vIndex >= densityGenerator.densityValues.Length)
                    {                        
                        wentOutOfBounds = true;
                        continue;
                    }

                    value = densityGenerator.densityValues[vIndex];

                    cellIDs[vIndex] = 0;

                    if (value > 0f)
                    {
                        //value = UnityEngine.Random.Range(0f, 1f);
                        //int id = y < testValue ? 1 : 2;
                        //int id = value < 0.9f ? 1 : 2;
                        int id = 1; //Stone
                        cellIDs[vIndex] = id;
                    }                    
                }
            }
        }

        if(wentOutOfBounds)
            print("out of bounds");

        int emptyDist = 0;

        vIndex = 0;
        for (int y = 0; y < height; y++)
        {
            for (int z = 0; z < width; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    vIndex = LinearIndex(x + 1, y, z + 1, width + 2);

                    //Is solid
                    if (cellIDs[vIndex] > 0)
                    {
                        emptyDist = 10;

                        for (int i = 1; i <= 5; i++)
                        {
                            if(y + i >= height || cellIDs[LinearIndex(x + 1, y + i, z + 1, width + 2)] == 0)
                            {
                                emptyDist = i;
                                break;
                            }
                        }

                        if (emptyDist <= 5 && y < height * 0.8f && y > 150) //Dirt
                        {
                            cellIDs[vIndex] = 2;
                        }

                        if (emptyDist == 1 && y < height * 0.7f && y > 150) //Grass
                        {
                            cellIDs[vIndex] = 3;
                        }
                    }                    
                }
            }
        }
    }

    private void Initialize()
    {
        var timer = new System.Diagnostics.Stopwatch();
        timer.Start();
        float startTime = Time.realtimeSinceStartup;
        float mms;

        InitializeTextureIndices();

        mms = (Time.realtimeSinceStartup - startTime) * 1000;//timer.ElapsedMilliseconds

        //print("Initializtion of texture indices took " + mms.ToString("0.00") + " mms");

        mFilter = GetComponent<MeshFilter>();
        mCollider = GetComponent<MeshCollider>();
        mRenderer = GetComponent<MeshRenderer>();

        mms = (Time.realtimeSinceStartup - startTime) * 1000;//timer.ElapsedMilliseconds

        //print("Total Initializtion took " + mms.ToString("0.00") + " mms");
    }

    public void UpdatePosition()
    {
        transform.position = new Vector3(position.x * width, 0, position.y * width) * voxelScale;
    }

    public void SetMesh(Mesh mesh, bool render = true, bool collision = false)
    {
        if (render)
            mFilter.sharedMesh = mesh;

        if (collision)
            mCollider.sharedMesh = mesh;
    }

    public void ClearMesh()
    {
        if (mFilter == null)
        {
            mFilter = GetComponent<MeshFilter>();
        }

        if (mCollider == null)
        {
            mCollider = GetComponent<MeshCollider>();
        }

        mFilter.sharedMesh = null;
        mCollider.sharedMesh = null;
    }

    public void UpdateMesh()
    {
        if (waitingForMeshUpdate)
        {
            return;
        }

        waitingForMeshUpdate = true;        

        var request = new VoxelMeshGenerator.MeshRequest(width, height, voxelScale, SimplificationAmount, textureIndices, cellIDs, textures, animationInfo,
            mFilter, mRenderer, mCollider, VoxelMeshGenerator.MeshRequest.Type.RenderMesh, levelOfDetail == 3 ? null : OnMeshUpdateRequestProcessed);
        meshGenerator.RequestMesh(request);

        if(levelOfDetail == 3)
        {
            var cRequest = new VoxelMeshGenerator.MeshRequest(width, height, voxelScale, SimplificationAmount, textureIndices, cellIDs, textures, animationInfo,
                mFilter, mRenderer, mCollider, VoxelMeshGenerator.MeshRequest.Type.CollisionMesh, OnMeshUpdateRequestProcessed);

            meshGenerator.RequestMesh(cRequest);
        }

    }

    public void UpdateMeshWithPirority()
    {
        if (waitingForMeshUpdate)
        {
            return;
        }

        waitingForMeshUpdate = true;

        var request = new VoxelMeshGenerator.MeshRequest(width, height, voxelScale, SimplificationAmount, textureIndices, cellIDs, textures, animationInfo,
            mFilter, mRenderer, mCollider, VoxelMeshGenerator.MeshRequest.Type.RenderMesh, levelOfDetail == 3 ? null : OnMeshUpdateRequestProcessed);
        meshGenerator.RequestPriorityMesh(request);

        if (levelOfDetail == 3)
        {
            var cRequest = new VoxelMeshGenerator.MeshRequest(width, height, voxelScale, SimplificationAmount, textureIndices, cellIDs, textures, animationInfo,
                mFilter, mRenderer, mCollider, VoxelMeshGenerator.MeshRequest.Type.CollisionMesh, OnMeshUpdateRequestProcessed);

            meshGenerator.RequestPriorityMesh(cRequest);
        }

    }

    private void OnMeshUpdateRequestProcessed()
    {
        waitingForMeshUpdate = false;        
    }

    public void Hide()
    {
        if(mRenderer == null)
        {
            mRenderer = GetComponent<MeshRenderer>();
        }

        mRenderer.enabled = false;
    }

    public void Show()
    {
        mRenderer.enabled = true;
    }

    private void OnDrawGizmos()
    {
        //Gizmos.color = Color.white;
        //Gizmos.DrawWireCube(new Vector3(width, height, width) / 2f + transform.position, new Vector3(width, height, width));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = levelOfDetail == 3 ? Color.blue : levelOfDetail == 2 ? Color.yellow : Color.red;

        Gizmos.DrawWireCube(new Vector3(width, height, width) / 2f * voxelScale + transform.position, new Vector3(width, height, width) * voxelScale);
    }

    public bool InOuterBounds(int x, int y, int z)
    {
        return x >= -1 && x <= width && y >= 0 && y < height && z >= -1 && z <= width;
    }

    public bool InOuterBounds(Vector3Int pos)
    {
        return pos.x >= -1 && pos.x <= width && pos.y >= 0 && pos.y < height && pos.z >= -1 && pos.z <= width;
    }

    public Vector3Int ToLocalPosition(Vector3 worldPos)
    {
        Vector3 chunkAnchor = new Vector3(position.x * width, 0, position.y * width) * voxelScale;
        Vector3 start = worldPos - chunkAnchor;

        return new Vector3Int(Mathf.FloorToInt(start.x / voxelScale), Mathf.FloorToInt(start.y / voxelScale), Mathf.FloorToInt(start.z / voxelScale));
    }

    public void SetVoxel(Vector3Int pos, string voxelID)
    {
        if (!InOuterBounds(pos) || waitingForMeshUpdate)
        {
            return;
        }

        if (voxelID == "base:air")
        {
            cellIDs[LinearIndex(pos.x + 1, pos.y, pos.z + 1, width + 2)] = 0;
            return;
        }

        if (!voxelIDs.Contains(voxelID))
        {
            voxelIDs.Add(voxelID);
            UpdateTextureList();
        }        

        cellIDs[LinearIndex(pos.x + 1, pos.y, pos.z + 1, width + 2)] = voxelIDs.IndexOf(voxelID) + 1;
    }

    public void SetVoxelBoxFill(int x, int y, int z, int endX, int endY, int endZ, string voxelID)
    {
        if (!voxelIDs.Contains(voxelID))
        {
            voxelIDs.Add(voxelID);
            UpdateTextureList();
        }

        for (int Y = y; Y <= endY; Y++)
        {
            for (int Z = z; Z <= endZ; Z++)
            {
                for (int X = x; X <= endX; X++)
                {
                    if (!InOuterBounds(X, Y, Z))
                    {
                        continue;
                    }

                    cellIDs[LinearIndex(X + 1, Y, Z + 1, width + 2)] = voxelIDs.IndexOf(voxelID) + 1;

                }
            }

        }
    }

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
