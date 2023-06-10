using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class MarchingCubesRenderer : ProceduralMeshRenderer
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

    private ComputeBuffer pointsBuffer;

    [Range(1, 128)]
    [Tooltip("How many voxels are in each axis of the mesh")]
    public int voxelsPerAxis;
    private int voxelsPerAxisLastGen;

    public PerlinNoiseComputeLoader perlin;

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
            OnEnable();
        }
    }

    private void Start()
    {
        UpdateMesh();
    }

    public override void OnEnable()
    {
        base.OnEnable();

        int pointsPerAxis = voxelsPerAxis + 1;
        int numPoints = (int)Mathf.Pow(pointsPerAxis, 3);        
        int numVoxels = voxelsPerAxis * voxelsPerAxis * voxelsPerAxis;        

        pointsBuffer = new ComputeBuffer(numPoints, sizeof(float) * 4);
        //pointsBuffer.SetData(GetRandomPoints());

        densityGenerator.UpdateData();

        //meshGenComputeShader.SetBuffer(idMeshGenKernel, "triangles", triBuffer);
        meshGenComputeShader.SetBuffer(idMeshGenKernel, "points", densityGenerator.densityValuesBuffer);
    }

    protected override void DisposeBuffers()
    {
        base.DisposeBuffers();

        pointsBuffer.Release();
    }

    public Vector4[] GetRandomPoints()
    {
        int pointsPerAxis = voxelsPerAxis + 1;
        int numberOfPoints = (int)Mathf.Pow(pointsPerAxis, 3);

        Vector4[] points = new Vector4[numberOfPoints];
        int lIndex = 0;
        float density = 0;

        perlin.GenerateNoise();
        float[] values2D = perlin.GetNoiseValues();

        material.SetTexture("_MainTex", perlin.texture);

        for (int x = 0; x < pointsPerAxis; x++)
        {
            for (int y = 0; y < pointsPerAxis; y++)
            {
                for (int z = 0; z < pointsPerAxis; z++)
                {
                    lIndex = LinearIndex(x, y, z);
                    density = 0;

                    //if(z > 5 && z < 10 && y > 5 && y < 10 && x > 5 && x < 10)
                    //{
                    //    density = 1f;
                    //}

                    //if (y < x && y < 5)
                    //{
                    //    density = 1f - Random.Range(0f, 0.5f);
                    //}
                    int height = Mathf.RoundToInt(values2D[x + z * pointsPerAxis] * pointsPerAxis) + 2;
                    if (height >= y)
                    {
                        density = 1 - y / (float)height;
                    }


                    Vector3 center = new Vector3(pointsPerAxis / 2, pointsPerAxis / 2, pointsPerAxis / 2);
                    float dist = Vector3.Distance(new Vector3(x, y, z), center);
                    float maxRadius = 15 + Random.Range(0, 1f);

                    

                    //if (dist < maxRadius && dist > 10)
                    //{
                    //    density = 1.0f;
                    //}

                    //Vector2 center2D = new Vector2(pointsPerAxis / 2, pointsPerAxis / 2);
                    //float dist2D = Vector2.Distance(new Vector2(x, z), center2D);

                    //if (dist2D <= 3f)
                    //{
                    //    density = 0;
                    //}

                    //density = 1 - (dist / maxRadius);

                    points[lIndex] = new Vector4(x, y, z, density);
                }
            }
        }

        return points;
    }


    private Texture2D texture;
    private void UpdateTexture()
    {
        if(texture == null || texture.width != voxelsPerAxis || texture.height != voxelsPerAxis)
        {
            texture = new Texture2D(voxelsPerAxis, voxelsPerAxis);
            texture.filterMode = FilterMode.Point;
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(MarchingCubesRenderer))]
public class MarchingCubesRendererEditor : Editor
{
    public MarchingCubesRenderer mRenderer;    

    private void OnEnable()
    {
        mRenderer = target as MarchingCubesRenderer;
    }

    public override void OnInspectorGUI()
    {        
        base.OnInspectorGUI();

        GUILayout.Space(10);
        if (GUILayout.Button("Update"))
        {
            mRenderer.shouldUpdateMeshThisFrame = true;
            mRenderer.OnEnable();
        }
    }
}
#endif