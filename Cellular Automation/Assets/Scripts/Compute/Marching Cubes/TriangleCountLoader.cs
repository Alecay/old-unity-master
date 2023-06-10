using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class TriangleCountLoader : ComputeLoader
{    

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
    private int voxelsPerAxisLastGen;
    
    public int triangleCount;
    public int vertexCount;

    public DensityGenerator densityGenerator;
    private ComputeBuffer trianglesPerVoxelBuffer;
    private ComputeBuffer overallTrianglesBuffer;

    new protected const string FUNCTION_NAME = "CalculateTriangleCountPerVoxel";

    private int idKernelOverallCount = 4;

    protected override void UpdateDispatchTimes()
    {
        int pointsPerAxis = voxelsPerAxis + 1;
        dispatchTimes = new Vector3Int(pointsPerAxis, pointsPerAxis, pointsPerAxis);
    }

    protected override void SetComputeVariables()
    {
        base.SetComputeVariables();

        if (voxelsPerAxisLastGen != voxelsPerAxis)
        {
            voxelsPerAxisLastGen = voxelsPerAxis;
            shouldUpdateDataThisFrame = true;
            Initialize();
        }

        computeShader.SetFloat("interpolate", interpolate ? 1f : 0f);
        computeShader.SetInt("numPointsPerAxis", voxelsPerAxis + 1);
        computeShader.SetFloat("isoLevel", isoLevel);

        computeShader.SetInt("meshSimplificationLevel", meshSimplificationLevel);

        //densityValuesBuffer.SetData(densityValues);        

    }

    protected override void CreateBuffers()
    {
        base.CreateBuffers();

        idKernel = computeShader.FindKernel("CalculateTriangleCountPerVoxel");

        int pointsPerAxis = voxelsPerAxis + 1;
        int numPoints = (int)Mathf.Pow(pointsPerAxis, 3);
        int numVoxels = voxelsPerAxis * voxelsPerAxis * voxelsPerAxis;        

        //densityValuesBuffer = new ComputeBuffer(pointsPerAxis * pointsPerAxis * pointsPerAxis, sizeof(float));
        //densityValuesBuffer.SetData(densityValues);
        
        computeShader.SetBuffer(idKernel, "_Density_Values_Buffer", densityGenerator.densityValuesBuffer);

        trianglesPerVoxelBuffer = new ComputeBuffer(voxelsPerAxis * voxelsPerAxis * voxelsPerAxis, sizeof(int));
        computeShader.SetBuffer(idKernel, "_Triangles_Per_Voxel_Buffer", trianglesPerVoxelBuffer);

        overallTrianglesBuffer = new ComputeBuffer(1, sizeof(int));
        computeShader.SetBuffer(idKernel, "_Overall_Triangle_Count_Buffer", overallTrianglesBuffer);
    }

    protected override void DisposeBuffers()
    {
        base.DisposeBuffers();
        
        trianglesPerVoxelBuffer.Release();
        overallTrianglesBuffer.Release();

        //densityGenerator.OnDisable();
    }

    public override void UpdateData()
    {
        base.UpdateData();

        idKernelOverallCount = computeShader.FindKernel("CalculateOverallTriangleCount");

        computeShader.SetBuffer(idKernelOverallCount, "_Triangles_Per_Voxel_Buffer", trianglesPerVoxelBuffer);
        computeShader.SetBuffer(idKernelOverallCount, "_Overall_Triangle_Count_Buffer", overallTrianglesBuffer);

        computeShader.Dispatch(idKernelOverallCount, 1, 1, 1);

        int[] triangleCountArr = new int[1];
        overallTrianglesBuffer.GetData(triangleCountArr);

        triangleCount = triangleCountArr[0];
        vertexCount = triangleCount * 3;
    }

    public override void RequestData()
    {        
        AsyncGPUReadback.Request(overallTrianglesBuffer, r1 => OnDataAvalible(r1));
    }

    protected override void OnDataAvalible(AsyncGPUReadbackRequest request)
    {
        if (request.hasError || !Application.isPlaying)
        {
            return;
        }

        var data = request.GetData<int>();
        triangleCount = data[0];        

        onDataAvalible?.Invoke();
    }
}
