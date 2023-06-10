using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChunkRequester : MonoBehaviour
{
    public int chunkWidth = 16;
    public int chunkHeight = 128;

    public int PointsCount
    {
        get
        {
            return (chunkWidth + 1) * (chunkWidth + 1) * (chunkHeight + 1);
        }
    }

    public ComputeShader densityShader;
    public DensityGenerator.NoiseData noiseData;
    [HideInInspector]
    public DensityGenerator densityCompute;
    public MarchingCubesMeshGenerator collisionMeshGen;

    public Mesh mesh;
    public MeshFilter filter;

    [Range(-1,1)]
    public float isoLevel = 0;
    [Range(0, 8)]
    public int meshSimplificationLevel = 0;

    public void Start()
    {
        densityCompute = new DensityGenerator(densityShader, chunkWidth + 1, chunkHeight + 1, noiseData);
        densityCompute.UpdateData();
        densityCompute.RequestData();

        densityCompute.onDataAvalible -= OnDensity;
        densityCompute.onDataAvalible += OnDensity;

        collisionMeshGen = new MarchingCubesMeshGenerator(chunkWidth, chunkHeight, isoLevel, true);
        collisionMeshGen.meshSimplificationLevel = meshSimplificationLevel;
    }

    private void OnDisable()
    {
        densityCompute.Deinitialize();
        collisionMeshGen.Dispose();
    }

    private void OnDensity()
    {
        collisionMeshGen.densityValues = densityCompute.values;
        //var mesh = marchMeshGen.GetMesh();
        StartCoroutine(collisionMeshGen.UpdateMeshAndAssign(mesh, filter));

        //filter.sharedMesh = mesh;

    }

    private void Update()
    {
        if (!Input.GetKey(KeyCode.U) && !collisionMeshGen.creatingMesh && !densityCompute.WaitingForData)
        {
            //isoLevel -= 0.1f * Time.deltaTime;
            //marchMeshGen.isoLevel = isoLevel;
            //StartCoroutine(marchMeshGen.UpdateMeshAndAssign(mesh, filter));
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;

        Vector3 size = new Vector3(chunkWidth, chunkHeight, chunkWidth);
        Vector3 center = size * 0.5f + transform.position;

        Gizmos.DrawWireCube(center, size);
    }
}
