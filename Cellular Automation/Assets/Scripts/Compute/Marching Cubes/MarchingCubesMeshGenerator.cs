using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class MarchingCubesMeshGenerator : ProceduralMeshGenerator
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

    public DensityGenerator densityGenerator;    

    int LinearIndex(int x, int y, int z)
    {
        int pointsPerAxis = voxelsPerAxis + 1;
        return (x + y * pointsPerAxis + z * pointsPerAxis * pointsPerAxis) % (pointsPerAxis * pointsPerAxis * pointsPerAxis);
    }

    protected override void UpdateMaxTriangles()
    {
        int numVoxels = voxelsPerAxis * voxelsPerAxis * voxelsPerAxis;
        //maxTriangles = (uint)(numVoxels * 5); //Max Triangles should be 5 times voxels

        maxTriangles = (uint)(numVoxels * 5) / 2; //Max Triangles can be reduced by half
    }

    protected override void UpdateDispatchTimes()
    {
        int pointsPerAxis = voxelsPerAxis + 1;
        dispatchTimes = new Vector3Int(pointsPerAxis, pointsPerAxis, pointsPerAxis);
    }

    protected override void SetComputeVariables()
    {
        base.SetComputeVariables();

        meshGenComputeShader.SetFloat("interpolate", interpolate ? 1f : 0f);
        meshGenComputeShader.SetInt("numPointsPerAxis", voxelsPerAxis + 1);
        meshGenComputeShader.SetFloat("isoLevel", isoLevel);

        meshGenComputeShader.SetInt("meshSimplificationLevel", meshSimplificationLevel);

        if (voxelsPerAxisLastGen != voxelsPerAxis)
        {
            voxelsPerAxisLastGen = voxelsPerAxis;
            shouldUpdateMeshThisFrame = true;
            InitializeBuffers();
        }
    }

    public override void UpdateMesh()
    {
        densityGenerator.UpdateData();

        base.UpdateMesh();
        
    }

    private void Start()
    {
        //UpdateMesh();
    }

    protected override void InitializeBuffers()
    {
        base.InitializeBuffers();

        int pointsPerAxis = voxelsPerAxis + 1;
        int numPoints = (int)Mathf.Pow(pointsPerAxis, 3);
        int numVoxels = voxelsPerAxis * voxelsPerAxis * voxelsPerAxis;
                
        densityGenerator.UpdateData();
        
        meshGenComputeShader.SetBuffer(idMeshGenKernel, "_Density_Values_Buffer", densityGenerator.densityValuesBuffer);
    }

    protected override void DisposeBuffers()
    {
        base.DisposeBuffers();

        densityGenerator.OnDisable();
    }    
}

#if UNITY_EDITOR
[CustomEditor(typeof(MarchingCubesMeshGenerator))]
public class MarchingCubesMeshGeneratorEditor : Editor
{
    public MarchingCubesMeshGenerator mGen;

    private void OnEnable()
    {
        mGen = target as MarchingCubesMeshGenerator;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        GUILayout.Space(10);
        if (GUILayout.Button("Update"))
        {
            mGen.shouldUpdateMeshThisFrame = true;            
        }
    }
}
#endif
