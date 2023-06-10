using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SimplifiedMarchingCubesLoader : MonoBehaviour
{
    [Header("Compute Components")]
    [Tooltip("The data creating compute shader")]
    [SerializeField] protected ComputeShader computeShader = default;
    [Tooltip("Should the loader create a new instanced copy of the compute shader")]
    public bool createCopyOfComputeShader = true;

    /// <summary>
    /// A state variable to help keep track of whether compute buffers have been set up
    /// </summary>
    protected bool initialized = false;
    protected bool initializedSecond = false;

    protected int idKernelTriCount;
    protected int idKernelOverallTriCount;
    protected int idKernelDrawTriangles;
    protected int idKernelMeshData;
    protected int idKernelFirstClose;
    protected int idKernelUniqueCount;
    protected int idKernelUniqueVertices;
    protected int idKernelUniqueTriangles;

    protected const string TRI_COUNT_FUNCTION_NAME = "CalculateTriangleCountPerVoxel";
    protected const string OVERALL_TRI_COUNT_FUNCTION_NAME = "CalculateOverallTriangleCount";
    protected const string DRAW_TRIANGLES_FUNCTION_NAME = "CreateDrawTriangles";
    protected const string MESH_DATA_FUNCTION_NAME = "CreateMeshDataFromDrawTriangles";
    protected const string FIRST_CLOSE_FUNCTION_NAME = "CalculateFirstCloseIndices";
    protected const string UNIQUE_COUNT_FUNCTION_NAME = "CalculateUniqueCount";
    protected const string UNIQUE_VERTICES_FUNCTION_NAME = "CalculateUniqueVertices";
    protected const string UNIQUE_TRIANGLES_FUNCTION_NAME = "CalculateUniqueTriangles";

    // The size of one entry into the various compute buffers
    protected const int DRAW_STRIDE = sizeof(float) * 3 + (sizeof(float) * (3 + 2 + 4)) * 3;//Normal + 3 * (V3 positions + V2 UV + V4 Color)

    protected ComputeBuffer densityBuffer;

    protected ComputeBuffer triCountBuffer;
    protected ComputeBuffer triIndexBuffer;
    protected ComputeBuffer overallTriCountBuffer;

    protected ComputeBuffer drawTrianglesBuffer;
    protected ComputeBuffer verticesBuffer;
    protected ComputeBuffer trianglesBuffer;

    protected ComputeBuffer firstCloseBuffer;
    protected ComputeBuffer uniqueIndicesBuffer;
    protected ComputeBuffer uniqueCountBuffer;
    protected ComputeBuffer uniqueVerticesBuffer;

    [Space(10)]
    [Header("Marching Cube Variables")]
    public bool interpolate = true;

    [Tooltip("All density values under this value will be drawn")]
    [Range(-1, 1)]
    public float isoLevel = 0.5f;

    [Tooltip("What level of simplification is applied to calculating triangles of the mesh")]
    [Range(0, 12)]
    public int meshSimplificationLevel = 0;

    [Range(1, 128)]
    [Tooltip("How many voxels are in each axis of the mesh")]
    public int voxelsPerAxis;

    public int triangleCount = 0;
    public int uniqueVerts = 0;

    Vector3[] vertices = new Vector3[0];
    int[] triangles = new int[0];

    public Mesh genMesh;

    public DensityGenerator densityGenerator;

    public MeshFilter filter;

    protected void Awake()
    {
        if (createCopyOfComputeShader)
        {
            computeShader = Instantiate(computeShader);
        }
    }

    private void Start()
    {
        initialized = false;
        initializedSecond = false;

        //CalculateTriCount();
        StartCoroutine(CreateData());
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.U))
        {
            //Deinitialize();
            StartCoroutine(CreateData());
        }
    }

    public void OnDisable()
    {
        Deinitialize();
    }
    
    IEnumerator CreateData()
    {
        float sTime = Time.realtimeSinceStartup;
        CalculateTriCount();
        yield return null;
        //yield return new WaitForSeconds(0.1f);

        RequestTriangleCount();

        while (!triCountAvalible)
        {
            yield return null;
        }

        CalculateDrawTriangles();
        yield return null;
        //yield return new WaitForSeconds(0.1f);

        CalculateMeshData();
        yield return null;
        //yield return new WaitForSeconds(0.1f);

        CalculateFirstClose();
        yield return null;
        //yield return new WaitForSeconds(0.1f);

        CalculateUniqueCount();
        yield return null;
        //yield return new WaitForSeconds(0.1f);

        RequestUniqueVerticesCount();

        while (!uniqueCountAvalible)
        {
            yield return null;
        }

        //yield return new WaitForSeconds(0.1f);

        CalculateFinal();

        while (!verticesAvalible)
        {
            yield return null;
        }

        //yield return new WaitForSeconds(0.1f);

        RequestTriangles();

        while (!trianglesAvalible)
        {
            yield return null;
        }

        yield return new WaitForSeconds(0.1f);
        CreateMesh();
        Debug.Log("Finished after " + (Time.realtimeSinceStartup - sTime).ToString("0.00 seconds"));
    }

    /// <summary>
    /// Create the buffers used in this compute shader and update thread group sizes
    /// </summary>
    public void Initialize()
    {
        // If initialized, call on disable to clean things up
        if (initialized)
        {
            Deinitialize();
        }
        initialized = true;        

        idKernelTriCount = computeShader.FindKernel(TRI_COUNT_FUNCTION_NAME);
        idKernelOverallTriCount = computeShader.FindKernel(OVERALL_TRI_COUNT_FUNCTION_NAME);
        idKernelDrawTriangles = computeShader.FindKernel(DRAW_TRIANGLES_FUNCTION_NAME);
        idKernelMeshData = computeShader.FindKernel(MESH_DATA_FUNCTION_NAME);
        idKernelFirstClose = computeShader.FindKernel(FIRST_CLOSE_FUNCTION_NAME);
        idKernelUniqueCount = computeShader.FindKernel(UNIQUE_COUNT_FUNCTION_NAME);
        idKernelUniqueVertices = computeShader.FindKernel(UNIQUE_VERTICES_FUNCTION_NAME);
        idKernelUniqueTriangles = computeShader.FindKernel(UNIQUE_TRIANGLES_FUNCTION_NAME);

        CreateCountBuffers();

        vertices = new Vector3[1];
        triangles = new int[1];
    }

    /// <summary>
    /// If this lodaer has been initialized the dispose of the buffers
    /// </summary>
    public void Deinitialize()
    {
        // Dispose of buffers
        if (initialized || initializedSecond)
        {
            DisposeBuffers();
        }
        initialized = false;
        initializedSecond = false;
    }    

    protected void CreateCountBuffers()
    {
        int voxelCount = voxelsPerAxis * voxelsPerAxis * voxelsPerAxis;

        int pointsPerAxis = voxelsPerAxis + 1;        
        int pointCount = pointsPerAxis * pointsPerAxis * pointsPerAxis;

        densityBuffer = new ComputeBuffer(pointCount, sizeof(float), ComputeBufferType.Structured);

        triCountBuffer = new ComputeBuffer(voxelCount, sizeof(int), ComputeBufferType.Structured);        
        triIndexBuffer = new ComputeBuffer(voxelCount, sizeof(int), ComputeBufferType.Structured);
        overallTriCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Structured);        

    }

    protected void CreateMeshDataBuffers()
    {   
        drawTrianglesBuffer = new ComputeBuffer(triangleCount, DRAW_STRIDE, ComputeBufferType.Structured);
        verticesBuffer = new ComputeBuffer(triangleCount * 3, sizeof(float) * 3, ComputeBufferType.Structured);
        trianglesBuffer = new ComputeBuffer(triangleCount * 3, sizeof(int), ComputeBufferType.Structured);

        firstCloseBuffer = new ComputeBuffer(triangleCount * 3, sizeof(int), ComputeBufferType.Structured);
        uniqueIndicesBuffer = new ComputeBuffer(triangleCount * 3, sizeof(int), ComputeBufferType.Structured);
        uniqueCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Structured);

        initializedSecond = true;
    }

    protected void CreateUnqiueVerticesBuffers()
    {
        uniqueVerticesBuffer?.Release();
        uniqueVerticesBuffer = new ComputeBuffer(uniqueVerts, sizeof(float) * 3, ComputeBufferType.Structured);
    }

    protected virtual void DisposeBuffers()
    {
        densityBuffer?.Release();
        triCountBuffer?.Release();
        triIndexBuffer?.Release();
        overallTriCountBuffer?.Release();

        drawTrianglesBuffer?.Release();
        verticesBuffer?.Release();
        trianglesBuffer?.Release();

        firstCloseBuffer?.Release();
        uniqueCountBuffer?.Release();
        uniqueIndicesBuffer?.Release();
        uniqueVerticesBuffer?.Release();
    }

    protected virtual void SetComputeVariables()
    {
        computeShader.SetFloat("interpolate", interpolate ? 1f : 0f);
        computeShader.SetInt("numPointsPerAxis", voxelsPerAxis + 1);
        computeShader.SetFloat("isoLevel", isoLevel);
        computeShader.SetInt("meshSimplificationLevel", meshSimplificationLevel);
    }

    public void CalculateTriCount()
    {
        //Debug.Log("Calculating triangle count");
        if (!initialized)
        {
            Initialize();
        }

        int pointsPerAxis = voxelsPerAxis + 1;

        SetComputeVariables();

        densityGenerator.noiseData.chunkSize = pointsPerAxis;
        densityGenerator.UpdateData();

        computeShader.SetBuffer(idKernelTriCount, "_Density_Values_Buffer", densityGenerator.densityValuesBuffer);
        computeShader.SetBuffer(idKernelTriCount, "_Triangles_Per_Voxel_Buffer", triCountBuffer);

        int dSize = Mathf.CeilToInt(pointsPerAxis / 8f);
        computeShader.Dispatch(idKernelTriCount, dSize, dSize, dSize);

        computeShader.SetBuffer(idKernelOverallTriCount, "_Triangles_Per_Voxel_Buffer", triCountBuffer);
        computeShader.SetBuffer(idKernelOverallTriCount, "_Triangle_Index_Buffer", triIndexBuffer);
        computeShader.SetBuffer(idKernelOverallTriCount, "_Overall_Triangle_Count_Buffer", overallTriCountBuffer);        

        //Calcualte the overall triCount
        computeShader.Dispatch(idKernelOverallTriCount, 1, 1, 1);

        //RequestTriangleCount();
    }

    public void CalculateDrawTriangles()
    {
        //Debug.Log("Calculating mesh data");

        verticesAvalible = false;
        triCountAvalible = false;

        if (triangleCount <= 0)
        {
            Debug.LogWarning("Skipped calculating mesh data beacuse the triangle count was " + triangleCount);
            return;
        }

        if (!initializedSecond)
        {
            CreateMeshDataBuffers();
        }


        SetComputeVariables();

        computeShader.SetInt("triangleCount", triangleCount);

        computeShader.SetBuffer(idKernelDrawTriangles, "_Density_Values_Buffer", densityGenerator.densityValuesBuffer);
        computeShader.SetBuffer(idKernelDrawTriangles, "_Triangles_Per_Voxel_Buffer", triCountBuffer);
        computeShader.SetBuffer(idKernelDrawTriangles, "_Triangle_Index_Buffer", triIndexBuffer);
        computeShader.SetBuffer(idKernelDrawTriangles, "_Draw_Triangles_Buffer", drawTrianglesBuffer);

        int pointsPerAxis = voxelsPerAxis + 1;
        int dSize = Mathf.CeilToInt(pointsPerAxis / 8f);
        //Create Draw Triangles and store into buffer
        computeShader.Dispatch(idKernelDrawTriangles, dSize, dSize, dSize);

        //CalculateMeshData();

        //CalculateFirstClose();

        //CalculateUniqueCount();

        //RequestUniqueVerticesCount();
        //RequestVertices();

    }

    public void CalculateMeshData()
    {
        computeShader.SetBuffer(idKernelMeshData, "_Draw_Triangles_Buffer", drawTrianglesBuffer);
        computeShader.SetBuffer(idKernelMeshData, "_Vertices_Buffer", verticesBuffer);
        computeShader.SetBuffer(idKernelMeshData, "_Triangles_Buffer", trianglesBuffer);

        int tSize = Mathf.CeilToInt(triangleCount / 8f);
        //Create vertices array and triangles array
        computeShader.Dispatch(idKernelMeshData, tSize, 1, 1);
    }

    public void CalculateFirstClose()
    {
        computeShader.SetBuffer(idKernelFirstClose, "_Draw_Triangles_Buffer", drawTrianglesBuffer);
        computeShader.SetBuffer(idKernelFirstClose, "_First_Close_Index_Buffer", firstCloseBuffer);
        computeShader.SetBuffer(idKernelFirstClose, "_Vertices_Buffer", verticesBuffer);

        int vSize = Mathf.CeilToInt((triangleCount * 3) / 8f);

        computeShader.Dispatch(idKernelFirstClose, vSize, 1, 1);
    }

    public void CalculateUniqueCount()
    {
        computeShader.SetBuffer(idKernelUniqueCount, "_Vertices_Buffer", verticesBuffer);
        computeShader.SetBuffer(idKernelUniqueCount, "_First_Close_Index_Buffer", firstCloseBuffer);
        computeShader.SetBuffer(idKernelUniqueCount, "_Unique_Indices_Buffer", uniqueIndicesBuffer);
        computeShader.SetBuffer(idKernelUniqueCount, "_Unique_Count_Buffer", uniqueCountBuffer);
        computeShader.SetBuffer(idKernelUniqueCount, "_Draw_Triangles_Buffer", drawTrianglesBuffer);

        computeShader.Dispatch(idKernelUniqueCount, 1, 1, 1);
    }

    public void CalculateFinal()
    {
        if(uniqueVerts <= 0)
        {
            Debug.LogError("No Unique Verts");
            return;
        }

        CreateUnqiueVerticesBuffers();

        computeShader.SetInt("uniqueCount", uniqueVerts);        

        computeShader.SetBuffer(idKernelUniqueVertices, "_Vertices_Buffer", verticesBuffer);
        computeShader.SetBuffer(idKernelUniqueVertices, "_First_Close_Index_Buffer", firstCloseBuffer);
        computeShader.SetBuffer(idKernelUniqueVertices, "_Unique_Indices_Buffer", uniqueIndicesBuffer);
        computeShader.SetBuffer(idKernelUniqueVertices, "_Unique_Count_Buffer", uniqueCountBuffer);
        computeShader.SetBuffer(idKernelUniqueVertices, "_Draw_Triangles_Buffer", drawTrianglesBuffer);
        computeShader.SetBuffer(idKernelUniqueVertices, "_Unique_Vertices_Buffer", uniqueVerticesBuffer);


        computeShader.Dispatch(idKernelUniqueVertices, 1, 1, 1);

        computeShader.SetBuffer(idKernelUniqueTriangles, "_First_Close_Index_Buffer", firstCloseBuffer);
        computeShader.SetBuffer(idKernelUniqueTriangles, "_Unique_Indices_Buffer", uniqueIndicesBuffer);
        computeShader.SetBuffer(idKernelUniqueTriangles, "_Triangles_Buffer", trianglesBuffer);

        int tSize = Mathf.CeilToInt((triangleCount * 3) / 8f);
        computeShader.Dispatch(idKernelUniqueTriangles, tSize, 1, 1);

        RequestVertices();
    }

    public delegate void AlertOnDataAvalible();
    public AlertOnDataAvalible onTriCountAvalible;
    public AlertOnDataAvalible onVerticesAvalible;
    public AlertOnDataAvalible onTrianglesAvalible;

    private bool trianglesAvalible = false;
    private bool verticesAvalible = false;
    private bool triCountAvalible = false;
    private bool uniqueCountAvalible = false;

    private void RequestTriangleCount()
    {
        triCountAvalible = false;
        AsyncGPUReadback.Request(overallTriCountBuffer, r1 => OnTriangleCountAvalible(r1));
    }

    private void OnTriangleCountAvalible(AsyncGPUReadbackRequest request)
    {
        if (request.hasError || !Application.isPlaying)
        {
            Debug.LogError("Error getting triangle count");
            triangleCount = -1;
            return;
        }

        var data = request.GetData<int>();
        triangleCount = data[0];

        onTriCountAvalible?.Invoke();
        triCountAvalible = true;

        Debug.Log("Got triangle count " + triangleCount);

        //CalculateDrawTriangles();
    }

    private void RequestUniqueVerticesCount()
    {
        uniqueVerts = -1;
        uniqueCountAvalible = false;
        AsyncGPUReadback.Request(uniqueCountBuffer, r1 => OnUniqueVerticesCountAvalible(r1));
    }

    private void OnUniqueVerticesCountAvalible(AsyncGPUReadbackRequest request)
    {
        if (request.hasError || !Application.isPlaying)
        {
            Debug.LogError("Error getting unique count");
            uniqueVerts = -1;
            return;
        }

        var data = request.GetData<int>();
        uniqueVerts = data[0];
        uniqueCountAvalible = true;
        //Debug.Log("Got unique count " + uniqueVerts);

        //CalculateFinal();
    }

    private void RequestVertices()
    {
        verticesAvalible = false;
        //AsyncGPUReadback.Request(verticesBuffer, r1 => OnVerticesAvalible(r1));
        AsyncGPUReadback.Request(uniqueVerticesBuffer, r1 => OnUniqueVerticesAvalible(r1));
    }

    private void RequestTriangles()
    {
        trianglesAvalible = false;
        AsyncGPUReadback.Request(trianglesBuffer, r1 => OnTrianglesAvalible(r1));
    }

    private void OnUniqueVerticesAvalible(AsyncGPUReadbackRequest request)
    {
        if (request.hasError || !Application.isPlaying)
        {
            Debug.LogError("Error getting unique vertices");
            return;
        }

        var data = request.GetData<Vector3>();

        if (data.Length != vertices.Length)
        {
            vertices = new Vector3[data.Length];
        }

        data.CopyTo(vertices);

        onVerticesAvalible?.Invoke();
        verticesAvalible = true;

        //Debug.Log("Got verts: " + vertices.Length + " vs " + (triangleCount * 3));

        //RequestTriangles();
    }

    private void OnVerticesAvalible(AsyncGPUReadbackRequest request)
    {
        if (request.hasError || !Application.isPlaying)
        {
            Debug.LogError("Error getting vertices");
            return;
        }

        var data = request.GetData<Vector3>();
        if(data.Length != vertices.Length || vertices.Length != triangleCount * 3)
        {
            vertices = new Vector3[data.Length];
        }

        data.CopyTo(vertices);

        onVerticesAvalible?.Invoke();
        verticesAvalible = true;

        //Debug.Log("Got verts");        

        RequestTriangles();
    }

    private void OnTrianglesAvalible(AsyncGPUReadbackRequest request)
    {
        if (request.hasError || !Application.isPlaying)
        {
            Debug.LogError("Error getting triangles");
            return;
        }

        var data = request.GetData<int>();
        if (data.Length != triangles.Length || triangles.Length != triangleCount * 3)
        {
            triangles = new int[data.Length];
        }

        data.CopyTo(triangles);

        onTrianglesAvalible?.Invoke();

        trianglesAvalible = true;

        //Debug.Log("Got triangles");

        //CreateMesh();
    }

    private void CreateMesh()
    {
        if (!genMesh)
        {
            genMesh = new Mesh();
            genMesh.MarkDynamic();
            genMesh.name = "Gen Mesh";
        }


        //triangles = new int[vertices.Length];

        //for (int i = 0; i < triangles.Length; i++)
        //{
        //    if (triangles[i] >= vertices.Length)
        //    {
        //        triangles[i] = 0;
        //    }
        //}

        //string prnt = "First 20 triangles: ";

        //for (int i = 0; i < 20; i++)
        //{
        //    prnt += triangles[i] + ", ";
        //}
        //Debug.Log(prnt);

        //prnt = "First 20 verts: ";
        //int strt = 20;
        //for (int i = strt; i < strt + 20; i++)
        //{
        //    prnt += vertices[i].ToString("0.0") + ", ";
        //}
        //Debug.Log(prnt);

        genMesh.Clear();

        genMesh.vertices = vertices;
        genMesh.triangles = triangles;
        genMesh.RecalculateBounds();
        //genMesh.RecalculateNormals();

        //genMesh.Optimize();

        genMesh.name = "Gen Mesh " + triangles.Length + " (" + vertices.Length + ")";

        if (filter)
        {
            filter.sharedMesh = genMesh;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;

        for (int i = 0; i < vertices.Length && i < 100; i++)
        {
            Gizmos.DrawSphere(vertices[i], 0.5f);
        }
    }
}


#if UNITY_EDITOR
[CustomEditor(typeof(SimplifiedMarchingCubesLoader))]
public class SMCLEditor : Editor
{
    static SimplifiedMarchingCubesLoader smcl;

    private void OnEnable()
    {
        smcl = target as SimplifiedMarchingCubesLoader;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        GUILayout.Space(10);
        if (GUILayout.Button("Update") || GUI.changed)
        {
            smcl.CalculateTriCount();
        }
    }
}
#endif