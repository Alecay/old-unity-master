using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class MarchingCubeMesh : MonoBehaviour
{
    #region Editor Vars

    public static bool drawGizmos = true;

    #endregion

    #region Jobs Variables

    [Header("Jobs Variables")]
    [Space(5)]

    public bool useJobs = true;
    public bool simplifyMesh = true;

    #endregion

    #region Marching Vars
    [Space(10)]
    [Header("Voxel Variables")]
    [Space(5)]

    public float voxelScale = 1f;

    [Range(1,32)]
    public int numVoxelsPerAxis;

    public int NumVoxels
    {
        get
        {
            return numVoxelsPerAxis * numVoxelsPerAxis * numVoxelsPerAxis;
        }
    }
    public int NumPointsPerAxis
    {
        get
        {
            return numVoxelsPerAxis + 1;
        }
    }

    public int NumPoints
    {
        get
        {
            return NumPointsPerAxis * NumPointsPerAxis * NumPointsPerAxis;
        }
    }

    [Range(-1, 1)]
    public float isoLevel;
    public bool interpolate;

    [Range(0, 10)]
    public int meshSimplificationLevel = 0;

    #endregion

    public int vertexCount = 0;
    public int indicesCount = 0;

    public int triangleCount = 0;

    #region Mesh Vars
    [Space(10)]
    [Header("Mesh Variables")]
    [Space(5)]

    public bool updateMeshOnStart = true;

    public Mesh generatedMesh;

    [HideInInspector]
    public MeshFilter filter;

    public bool setCollider = true;

    [HideInInspector]
    public MeshCollider meshCollider;

    private bool meshCreated = false;
    int lastVertexIndex = 0;

    NativeArray<Vector3> meshVertices;
    NativeArray<short> meshTriangles;

    NativeArray<Vector3> meshNormals;
    NativeArray<Color> meshColors;

    #endregion

    #region Density / Noise Vars
    [Space(10)]
    [Header("Noise Variables")]
    [Space(5)]
    public DensityGenerator densityGenerator;

    float[] densityValues;

    NativeArray<Vector4> densityPoints;

    private bool denistyHasChanged = false;
    private float[] modifedDensityValues;

    #endregion


    private void OnDisable()
    {
        DisposeOfNativeArrays();
    }

    private void Start()
    {
        filter = GetComponent<MeshFilter>();
        meshCollider = GetComponent<MeshCollider>();

        CreateMesh();

        filter.mesh = generatedMesh;

        if(setCollider && meshCollider != null)
        {
            meshCollider.sharedMesh = generatedMesh;
        }

        if (updateMeshOnStart)
        {
            UpdateMesh();
        }
    }

    #region Marching Methods

    Vector3 interpolateVerts(Vector4 v1, Vector4 v2)
    {
        Vector3 v3 = v1;
        Vector3 v4 = v2;

        float t = (isoLevel - v1.w) / (v2.w - v1.w);
        return v3 + t * (v4 - v3);
    }

    int indexFromCoord(int x, int y, int z)
    {
        return z * NumPointsPerAxis * NumPointsPerAxis + y * NumPointsPerAxis + x;
    }

    int indexFromCoord(Vector3Int id)
    {
        return indexFromCoord(id.x, id.y, id.z);
    }

    Vector2 GetUV(Vector3 p)
    {
        Vector2 uv;

        uv = new Vector2(p.x / (float)NumPointsPerAxis, p.z / (float)NumPointsPerAxis);

        return uv;
    }

    //Gets the nth factor of num
    int GetFactor(int num, int n)
    {
        int testNum = 1;
        int currentFactorIndex = 0;
        int lastFactor = 1;

        if (n <= 0 || num <= 1)
        {            
            return 1;
        }

        //increase testNum while it's less than or equal to num
        while (testNum < num)
        {
            //If testNum is a factor of num
            if (num % testNum == 0)
            {
                //Save this testNum in lastFactor
                lastFactor = testNum;

                //If the currentIndex == the desired index N then return testNum
                if (currentFactorIndex == n)
                {
                    return testNum;
                }

                //Every found factor increment the index
                currentFactorIndex++;
            }

            testNum++;
        }

        return lastFactor;
    }

    private Vector4 GetDenistyPoint(int x, int y, int z)
    {
        return densityPoints[indexFromCoord(x, y, z)];
    }

    private float GetDenistyValue(int x, int y, int z)
    {
        return densityValues[indexFromCoord(x, y, z)];
    }

    private float GetDenistyValue(Vector3Int id)
    {
        return densityValues[indexFromCoord(id)];
    }

    private Vector3Int[] cubeOffsets =
    {
        new Vector3Int(0, 0, 0),
        new Vector3Int(1, 0, 0),
        new Vector3Int(1, 0, 1),
        new Vector3Int(0, 0, 1),

        new Vector3Int(0, 1, 0),
        new Vector3Int(1, 1, 0),
        new Vector3Int(1, 1, 1),
        new Vector3Int(0, 1, 1)
    };

    private Vector4 GetCubeCorner(Vector3Int id, int increment, int index)
    {
        index %= 8;

        Vector3Int offset = cubeOffsets[index] * increment;
        Vector3Int pos = id + offset;

        return GetDenistyPoint(pos.x, pos.y, pos.z);

    }

    #endregion

    #region Mesh Creation and Updating

    [BurstCompile]
    struct TriangleAddJob : IJobParallelFor
    {
        public NativeArray<VoxelTriangleInfo> triangles;        

        [ReadOnly] [NativeDisableParallelForRestriction] public NativeArray<Vector4> densityPoints;

        public int numPointsPerAxis;
        public float isoLevel;
        public bool interpolate;

        public int increment;

        Vector3 interpolateVerts(Vector4 v1, Vector4 v2)
        {
            Vector3 v3 = v1;
            Vector3 v4 = v2;

            float t = (isoLevel - v1.w) / (v2.w - v1.w);
            return v3 + t * (v4 - v3);
        }

        Vector3Int IDFromIndex(int index)
        {
            return new Vector3Int(index % numPointsPerAxis, (index / (numPointsPerAxis)) % numPointsPerAxis, index / (numPointsPerAxis * numPointsPerAxis));
        }

        int indexFromCoord(int x, int y, int z)
        {
            return (z * numPointsPerAxis * numPointsPerAxis + y * numPointsPerAxis + x);
        }

        private Vector4 GetDenistyPoint(int x, int y, int z)
        {
            return densityPoints[indexFromCoord(x, y, z)];
        }

        private Vector4 GetCubeCorner(Vector3Int id, int increment, int index)
        {
            index %= 8;

            Vector3Int offset = CubeOffset(index) * increment;
            Vector3Int pos = id + offset;

            return GetDenistyPoint(pos.x, pos.y, pos.z);

        }

        private Vector3Int CubeOffset(int index)
        {
            index %= 8;

            switch (index)
            {
                default:
                case 0:
                    return new Vector3Int(0, 0, 0);
                case 1:
                    return new Vector3Int(1, 0, 0);
                case 2:
                    return new Vector3Int(1, 0, 1);
                case 3:
                    return new Vector3Int(0, 0, 1);
                case 4:
                    return new Vector3Int(0, 1, 0);
                case 5:
                    return new Vector3Int(1, 1, 0);
                case 6:
                    return new Vector3Int(1, 1, 1);
                case 7:
                    return new Vector3Int(0, 1, 1);
            }
        }

        public void Execute(int index)
        {
            Vector3Int id = IDFromIndex(index);

            //print("Starting " + id.ToString());

            if (index >= numPointsPerAxis * numPointsPerAxis * numPointsPerAxis)
            {
                return;
            }

            if(index != indexFromCoord(id.x, id.y, id.z))
            {
                //print("Skip " + id.ToString());
            }

            // Stop one point before the end because voxel includes neighbouring points
            if (id.x >= numPointsPerAxis - 1 || id.y >= numPointsPerAxis - 1 || id.z >= numPointsPerAxis - 1)
            {
                //print("Skip " + id.ToString());
                return;
            }

            if (increment < 0)
            {
                increment = 1;
            }

            triangles[index] = new VoxelTriangleInfo();

            if (id.x % increment > 0 || id.y % increment > 0 || id.z % increment > 0)
            {
                return;
            }

            NativeArray<Vector4> cubeCorners = new NativeArray<Vector4>(8, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            cubeCorners[0] = GetCubeCorner(id, increment, 0);
            cubeCorners[1] = GetCubeCorner(id, increment, 1);
            cubeCorners[2] = GetCubeCorner(id, increment, 2);
            cubeCorners[3] = GetCubeCorner(id, increment, 3);

            cubeCorners[4] = GetCubeCorner(id, increment, 4);
            cubeCorners[5] = GetCubeCorner(id, increment, 5);
            cubeCorners[6] = GetCubeCorner(id, increment, 6);
            cubeCorners[7] = GetCubeCorner(id, increment, 7);

            // Calculate unique index for each cube configuration.
            // There are 256 possible values
            // A value of 0 means cube is entirely inside surface; 255 entirely outside.
            // The value is used to look up the edge table, which indicates which edges of the cube are cut by the isosurface.
            int cubeIndex = 0;
            if (cubeCorners[0].w < isoLevel)
                cubeIndex |= 1;
            if (cubeCorners[1].w < isoLevel)
                cubeIndex |= 2;
            if (cubeCorners[2].w < isoLevel)
                cubeIndex |= 4;
            if (cubeCorners[3].w < isoLevel)
                cubeIndex |= 8;
            if (cubeCorners[4].w < isoLevel)
                cubeIndex |= 16;
            if (cubeCorners[5].w < isoLevel)
                cubeIndex |= 32;
            if (cubeCorners[6].w < isoLevel)
                cubeIndex |= 64;
            if (cubeCorners[7].w < isoLevel)
                cubeIndex |= 128;

            int tCount = 0;

            for (int i = 0; MarchTables.Triangulation[cubeIndex][i] != -1; i += 3)
            {
                tCount++;
            }

            // Create triangles for current cube configuration


            NativeArray<Vector4> triPoints = new NativeArray<Vector4>(tCount * 3, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            NativeArray<Color> triColors = new NativeArray<Color>(tCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            for (int i = 0; MarchTables.Triangulation[cubeIndex][i] != -1; i += 3)
            {
                // Get indices of corner points A and B for each of the three edges
                // of the cube that need to be joined to form the triangle.
                int a0 = MarchTables.CornerIndexAFromEdge[MarchTables.Triangulation[cubeIndex][i]];
                int b0 = MarchTables.CornerIndexBFromEdge[MarchTables.Triangulation[cubeIndex][i]];

                int a1 = MarchTables.CornerIndexAFromEdge[MarchTables.Triangulation[cubeIndex][i + 1]];
                int b1 = MarchTables.CornerIndexBFromEdge[MarchTables.Triangulation[cubeIndex][i + 1]];

                int a2 = MarchTables.CornerIndexAFromEdge[MarchTables.Triangulation[cubeIndex][i + 2]];
                int b2 = MarchTables.CornerIndexBFromEdge[MarchTables.Triangulation[cubeIndex][i + 2]];

                //DrawTriangle dTri;

                if (interpolate)
                {
                    triPoints[i + 0] = interpolateVerts(cubeCorners[a0], cubeCorners[b0]);
                    triPoints[i + 1] = interpolateVerts(cubeCorners[a1], cubeCorners[b1]);
                    triPoints[i + 2] = interpolateVerts(cubeCorners[a2], cubeCorners[b2]);
                }
                else
                {
                    triPoints[i + 0] = cubeCorners[a0];
                    triPoints[i + 1] = cubeCorners[a1];
                    triPoints[i + 2] = cubeCorners[a2];
                }

                //triColors[i / 3] = new Color(1, 1, 1, 1);
                triColors[i / 3] = new Color((Mathf.Sin(id.x) + 1) / 2, (Mathf.Sin(id.y) + 1) / 2, (Mathf.Sin(id.z) + 1) / 2, 1);
            }

            triangles[index] = new VoxelTriangleInfo(triPoints, triColors);

            triPoints.Dispose();
            triColors.Dispose();
            cubeCorners.Dispose();
        }
    }

    struct VoxelTriangleInfo
    {
        public int triangleCount;

        public struct TriangleInfo
        {
            //Triangle 1
            public Vector3 vertex0;
            public Vector3 vertex1;
            public Vector3 vertex2;

            public Color color;

            public Vector3 Normal
            {
                get
                {
                    return Vector3.Cross((vertex1 - vertex0), (vertex2 - vertex0)).normalized;
                }
            }

            public TriangleInfo(Vector3 vertex0, Vector3 vertex1, Vector3 vertex2, Color color)
            {
                this.vertex0 = vertex0;
                this.vertex1 = vertex1;
                this.vertex2 = vertex2;
                this.color = color;
            }
        }

        public TriangleInfo triangle0;
        public TriangleInfo triangle1;
        public TriangleInfo triangle2;
        public TriangleInfo triangle3;
        public TriangleInfo triangle4;

        public Vector3[] VertexArray
        {
            get
            {
                Vector3[] arr = new Vector3[triangleCount * 3];

                for (int i = 0; i < triangleCount * 3; i++)
                {
                    switch (i)
                    {
                        case 0:
                            arr[i] = triangle0.vertex0;
                            break;
                        case 1:
                            arr[i] = triangle0.vertex1;
                            break;
                        case 2:
                            arr[i] = triangle0.vertex2;
                            break;
                        case 3:
                            arr[i] = triangle1.vertex0;
                            break;
                        case 4:
                            arr[i] = triangle1.vertex1;
                            break;
                        case 5:
                            arr[i] = triangle1.vertex2;
                            break;
                        case 6:
                            arr[i] = triangle2.vertex0;
                            break;
                        case 7:
                            arr[i] = triangle2.vertex1;
                            break;
                        case 8:
                            arr[i] = triangle2.vertex2;
                            break;
                        case 9:
                            arr[i] = triangle3.vertex0;
                            break;
                        case 10:
                            arr[i] = triangle3.vertex1;
                            break;
                        case 11:
                            arr[i] = triangle3.vertex2;
                            break;
                        case 12:
                            arr[i] = triangle4.vertex0;
                            break;
                        case 13:
                            arr[i] = triangle4.vertex1;
                            break;
                        case 14:
                            arr[i] = triangle4.vertex2;
                            break;
                        default:
                            break;
                    }
                }

                return arr;
            }
        }

        public Color[] ColorArray
        {
            get
            {
                Color[] arr = new Color[triangleCount];

                for (int i = 0; i < triangleCount; i++)
                {
                    switch (i)
                    {
                        case 0:
                            arr[i] = triangle0.color;
                            break;
                        case 1:
                            arr[i] = triangle1.color;
                            break;
                        case 2:
                            arr[i] = triangle2.color;
                            break;
                        case 3:
                            arr[i] = triangle3.color;
                            break;
                        case 4:
                            arr[i] = triangle4.color;
                            break;                        
                        default:
                            break;
                    }
                }

                return arr;
            }
        }

        public Vector3[] NormalArray
        {
            get
            {
                Vector3[] arr = new Vector3[triangleCount];

                for (int i = 0; i < triangleCount; i++)
                {
                    switch (i)
                    {
                        case 0:
                            arr[i] = triangle0.Normal;
                            break;
                        case 1:
                            arr[i] = triangle1.Normal;
                            break;
                        case 2:
                            arr[i] = triangle2.Normal;
                            break;
                        case 3:
                            arr[i] = triangle3.Normal;
                            break;
                        case 4:
                            arr[i] = triangle4.Normal;
                            break;
                        default:
                            break;
                    }
                }

                return arr;
            }
        }

        public VoxelTriangleInfo(Vector3[] vertices, Color[] colors)
        {
            this.triangle0 = new TriangleInfo();
            this.triangle1 = new TriangleInfo();
            this.triangle2 = new TriangleInfo();
            this.triangle3 = new TriangleInfo();
            this.triangle4 = new TriangleInfo();

            this.triangleCount = vertices.Length / 3;

            for (int i = 0; i < this.triangleCount; i++)
            {
                switch (i)
                {
                    case 0:
                        triangle0 = new TriangleInfo(vertices[i * 3 + 0], vertices[i * 3 + 1], vertices[i * 3 + 2], colors[i]);
                        break;
                    case 1:
                        triangle1 = new TriangleInfo(vertices[i * 3 + 0], vertices[i * 3 + 1], vertices[i * 3 + 2], colors[i]);
                        break;
                    case 2:
                        triangle2 = new TriangleInfo(vertices[i * 3 + 0], vertices[i * 3 + 1], vertices[i * 3 + 2], colors[i]);
                        break;
                    case 3:
                        triangle3 = new TriangleInfo(vertices[i * 3 + 0], vertices[i * 3 + 1], vertices[i * 3 + 2], colors[i]);
                        break;
                    case 4:
                        triangle4 = new TriangleInfo(vertices[i * 3 + 0], vertices[i * 3 + 1], vertices[i * 3 + 2], colors[i]);
                        break;
                    default:
                        break;
                }
            }
        }

        public VoxelTriangleInfo(NativeArray<Vector4> vertices, NativeArray<Color> colors)
        {
            this.triangle0 = new TriangleInfo();
            this.triangle1 = new TriangleInfo();
            this.triangle2 = new TriangleInfo();
            this.triangle3 = new TriangleInfo();
            this.triangle4 = new TriangleInfo();

            this.triangleCount = vertices.Length / 3;

            for (int i = 0; i < this.triangleCount; i++)
            {
                switch (i)
                {
                    case 0:
                        triangle0 = new TriangleInfo(vertices[i * 3 + 0], vertices[i * 3 + 1], vertices[i * 3 + 2], colors[i]);
                        break;
                    case 1:
                        triangle1 = new TriangleInfo(vertices[i * 3 + 0], vertices[i * 3 + 1], vertices[i * 3 + 2], colors[i]);
                        break;
                    case 2:
                        triangle2 = new TriangleInfo(vertices[i * 3 + 0], vertices[i * 3 + 1], vertices[i * 3 + 2], colors[i]);
                        break;
                    case 3:
                        triangle3 = new TriangleInfo(vertices[i * 3 + 0], vertices[i * 3 + 1], vertices[i * 3 + 2], colors[i]);
                        break;
                    case 4:
                        triangle4 = new TriangleInfo(vertices[i * 3 + 0], vertices[i * 3 + 1], vertices[i * 3 + 2], colors[i]);
                        break;
                    default:
                        break;
                }
            }
        }
    }

    private void AddTriangle(VoxelTriangleInfo info)
    {
        Vector3[] arr = info.VertexArray;
        Vector3[] normals = info.NormalArray;
        Color[] colors = info.ColorArray;

        for (int i = 0; i < arr.Length; i++)
        {
            meshVertices[lastVertexIndex] = arr[i];

            meshTriangles[lastVertexIndex] = (short)lastVertexIndex;

            meshNormals[lastVertexIndex] = normals[i / 3];

            meshColors[lastVertexIndex] = colors[i / 3];

            lastVertexIndex++;
        }

    }

    private void AddTriangles(int increment, Vector3Int id)
    {
        // Stop one point before the end because voxel includes neighbouring points
        if (id.x >= NumPointsPerAxis - 1 || id.y >= NumPointsPerAxis - 1 || id.z >= NumPointsPerAxis - 1)
        {
            return;
        }

        if (increment < 0)
        {
            increment = 1;
        }

        if (id.x % increment > 0 || id.y % increment > 0 || id.z % increment > 0)
        {
            return;
        }

        Vector4[] cubeCorners = new Vector4[8];

        cubeCorners[0] = GetCubeCorner(id, increment, 0);
        cubeCorners[1] = GetCubeCorner(id, increment, 1);
        cubeCorners[2] = GetCubeCorner(id, increment, 2);
        cubeCorners[3] = GetCubeCorner(id, increment, 3);

        cubeCorners[4] = GetCubeCorner(id, increment, 4);
        cubeCorners[5] = GetCubeCorner(id, increment, 5);
        cubeCorners[6] = GetCubeCorner(id, increment, 6);
        cubeCorners[7] = GetCubeCorner(id, increment, 7);

        // Calculate unique index for each cube configuration.
        // There are 256 possible values
        // A value of 0 means cube is entirely inside surface; 255 entirely outside.
        // The value is used to look up the edge table, which indicates which edges of the cube are cut by the isosurface.
        int cubeIndex = 0;
        if (cubeCorners[0].w < isoLevel)
            cubeIndex |= 1;
        if (cubeCorners[1].w < isoLevel)
            cubeIndex |= 2;
        if (cubeCorners[2].w < isoLevel)
            cubeIndex |= 4;
        if (cubeCorners[3].w < isoLevel)
            cubeIndex |= 8;
        if (cubeCorners[4].w < isoLevel)
            cubeIndex |= 16;
        if (cubeCorners[5].w < isoLevel)
            cubeIndex |= 32;
        if (cubeCorners[6].w < isoLevel)
            cubeIndex |= 64;
        if (cubeCorners[7].w < isoLevel)
            cubeIndex |= 128;

        // Create triangles for current cube configuration
        for (int i = 0; MarchTables.Triangulation[cubeIndex][i] != -1; i += 3)
        {
            // Get indices of corner points A and B for each of the three edges
            // of the cube that need to be joined to form the triangle.
            int a0 = MarchTables.CornerIndexAFromEdge[MarchTables.Triangulation[cubeIndex][i]];
            int b0 = MarchTables.CornerIndexBFromEdge[MarchTables.Triangulation[cubeIndex][i]];

            int a1 = MarchTables.CornerIndexAFromEdge[MarchTables.Triangulation[cubeIndex][i + 1]];
            int b1 = MarchTables.CornerIndexBFromEdge[MarchTables.Triangulation[cubeIndex][i + 1]];

            int a2 = MarchTables.CornerIndexAFromEdge[MarchTables.Triangulation[cubeIndex][i + 2]];
            int b2 = MarchTables.CornerIndexBFromEdge[MarchTables.Triangulation[cubeIndex][i + 2]];

            Vector3[] triPoints = new Vector3[3];
            //DrawTriangle dTri;

            if (interpolate)
            {
                triPoints[0] = interpolateVerts(cubeCorners[a0], cubeCorners[b0]);
                triPoints[1] = interpolateVerts(cubeCorners[a1], cubeCorners[b1]);
                triPoints[2] = interpolateVerts(cubeCorners[a2], cubeCorners[b2]);
            }
            else
            {
                triPoints[0] = cubeCorners[a0];
                triPoints[1] = cubeCorners[a1];
                triPoints[2] = cubeCorners[a2];
            }

            meshVertices[lastVertexIndex] = triPoints[0];
            meshVertices[lastVertexIndex + 1] = triPoints[1];
            meshVertices[lastVertexIndex + 2] = triPoints[2];

            meshTriangles[lastVertexIndex] = (short)lastVertexIndex;
            meshTriangles[lastVertexIndex + 1] = (short)(lastVertexIndex + 1);
            meshTriangles[lastVertexIndex + 2] = (short)(lastVertexIndex + 2);

            lastVertexIndex += 3;

            triangleCount++;
        }
    }

    private void CalculateTrianglesFromJobs()
    {
        int increment = GetFactor(NumPointsPerAxis - 1, meshSimplificationLevel);
        if (increment < 1)
        {
            increment = 1;
        }

        int numPoints = NumPoints;
        int numVoxels = NumVoxels;

        NativeArray<VoxelTriangleInfo> triangles = new NativeArray<VoxelTriangleInfo>(numPoints, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        var job = new TriangleAddJob
        {
            triangles = triangles,
            densityPoints = densityPoints,
            numPointsPerAxis = numVoxelsPerAxis + 1,
            isoLevel = isoLevel,
            interpolate = interpolate,
            increment = increment,
        };

        job.Schedule(numPoints, 16).Complete();
        //for (var i = 0; i < numPoints; ++i)
        //{
        //    job.Execute(i);
        //}

        lastVertexIndex = 0;

        for (var i = 0; i < numPoints; i++)
        {
            if(triangles[i].triangleCount > 0)
            {
                AddTriangle(triangles[i]);
            }
        }

        triangleCount = 0;

        for (var i = 0; i < numPoints; ++i)
        {
            triangleCount += triangles[i].triangleCount;
        }

        triangles.Dispose();
        return;
    }

    private void CalculateTriangles()
    {
        int increment = GetFactor(NumPointsPerAxis - 1, meshSimplificationLevel);
        if (increment < 1)
        {
            increment = 1;
        }

        int numPoints = NumPoints;
        int numVoxels = NumVoxels;

        Vector3Int id = new Vector3Int();
        for (int z = 0; z < NumPointsPerAxis; z++)
        {
            for (int y = 0; y < NumPointsPerAxis; y++)
            {
                for (int x = 0; x < NumPointsPerAxis; x++)
                {
                    id = new Vector3Int(x, y, z);

                    AddTriangles(increment, id);
                }
            }
        }
    }

    public void UpdateMesh()
    {

        if (!meshCreated)
        {
            CreateMesh();
        }

        lastVertexIndex = 0;

        if (densityValues?.Length != NumPoints)
        {
            UpdateDensityValues();
        }

        if (denistyHasChanged)
        {
            //print("applying modified values");
            ApplyModifiedDensityValues();
            denistyHasChanged = false;
        }

        triangleCount = 0;

        if (useJobs)
        {
            CalculateTrianglesFromJobs();
        }
        else
        {
            CalculateTriangles();
        }

        if (triangleCount == 0)
        {
            return;
        }

        vertexCount = triangleCount * 3;
        indicesCount = triangleCount * 3;

        if (simplifyMesh)
        {
            SimplifyMesh();
        }

        generatedMesh.Clear();

        generatedMesh.SetVertices(meshVertices, 0, vertexCount);
        

        generatedMesh.SetIndexBufferParams(indicesCount, IndexFormat.UInt16);
        generatedMesh.SetIndexBufferData(meshTriangles, 0, 0, indicesCount, MeshUpdateFlags.Default);
        
        generatedMesh.SetSubMesh(0, new SubMeshDescriptor(0, indicesCount, MeshTopology.Triangles));

        if (!useJobs)
        {
            generatedMesh.RecalculateNormals();
        }
        else
        {
            //generatedMesh.SetColors(meshColors, 0, vertexCount);
            generatedMesh.SetNormals(meshNormals, 0, vertexCount);
            //generatedMesh.RecalculateNormals();
        }        

        if (setCollider && meshCollider != null)
        {
            meshCollider.sharedMesh = generatedMesh;
        }
    }

    Mesh CreateMesh()
    {
        Mesh newMesh = new Mesh();
        newMesh.name = "Marching Cube Mesh";
        // Use 32 bit index buffer to allow water grids larger than ~250x250
        newMesh.indexFormat = IndexFormat.UInt32;
        newMesh.MarkDynamic();

        int numPoints = NumPoints;
        int numVoxels = NumVoxels;
        int maxTriangles = numVoxels * 5;
        int maxVertices = maxTriangles * 3;

        if (meshVertices != null && meshVertices.IsCreated)
        {
            meshVertices.Dispose();
        }

        if (meshTriangles != null && meshTriangles.IsCreated)
        {
            meshTriangles.Dispose();
        }

        if (meshNormals != null && meshNormals.IsCreated)
        {
            meshNormals.Dispose();
        }

        if (meshColors != null && meshColors.IsCreated)
        {
            meshColors.Dispose();
        }

        meshVertices = new NativeArray<Vector3>(maxVertices, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        meshTriangles = new NativeArray<short>(maxVertices, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        meshNormals = new NativeArray<Vector3>(maxVertices, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        meshColors = new NativeArray<Color>(maxVertices, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        meshCreated = true;

        generatedMesh = newMesh;

        return newMesh;
    }

    #endregion

    #region Simplification

    [BurstCompile]
    struct FirstCloseVertexJob : IJobParallelFor
    {
        public struct FirstCloseInfo
        {
            public int oldIndex;
            public int newIndex;

            public FirstCloseInfo(int oldIndex, int newIndex)
            {
                this.oldIndex = oldIndex;
                this.newIndex = newIndex;
            }

            public new string ToString()
            {
                return "(" + oldIndex + ", " + newIndex + ")";
            }
        }

        public NativeArray<FirstCloseInfo> firstClose;

        [ReadOnly] [NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;

        public float minDist;

        public void Execute(int index)
        {
            for (int i = 0, length = firstClose.Length; i < length; i++)
            {
                if (i == index || Vector3.Distance(vertices[i], vertices[index]) <= minDist)
                {
                    firstClose[index] = new FirstCloseInfo(index, i);
                    break;
                }
            }
        }
    }

    [BurstCompile]
    struct UniqueVerticesJob : IJobParallelFor
    {
        [ReadOnly] [NativeDisableParallelForRestriction] public NativeArray<FirstCloseVertexJob.FirstCloseInfo> firstClose;

        public void Execute(int index)
        {
            // Go Through each first close pair and find the unique ones
        }
    }

    public void SimplifyMesh()
    {
        NativeArray<FirstCloseVertexJob.FirstCloseInfo> firstClose = 
            new NativeArray<FirstCloseVertexJob.FirstCloseInfo>(vertexCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        var job = new FirstCloseVertexJob
        {
            firstClose = firstClose,
            vertices = meshVertices,
            minDist = 0.05f
        };        

        job.Schedule(vertexCount, 16).Complete();

        List<int> newVertIndices = new List<int>();
        List<Vector3> uniqueVertices = new List<Vector3>();
        List<Color> uniqueColors = new List<Color>();
        List<FirstCloseVertexJob.FirstCloseInfo> uniqueFirstClose = new List<FirstCloseVertexJob.FirstCloseInfo>();

        int tCount = triangleCount * 3;

        for (int i = 0; i < vertexCount; i++)
        {
            if (!newVertIndices.Contains(firstClose[i].newIndex))
            {
                newVertIndices.Add(firstClose[i].newIndex);
                uniqueVertices.Add(meshVertices[i]);
                uniqueColors.Add(meshColors[i]);
            }
        }

        for (int i = 0; i < uniqueVertices.Count; i++)
        {
            meshVertices[i] = uniqueVertices[i];
        }

        for (int i = 0; i < uniqueColors.Count; i++)
        {
            meshColors[i] = uniqueColors[i];
        }

        for (int i = 0; i < indicesCount; i++)
        {
            meshTriangles[i] = (short)firstClose[i].newIndex;
        }

        for (int j = 0; j < indicesCount; j++)
        {
            for (int i = 0; i < newVertIndices.Count; i++)
            {
                if (meshTriangles[j] == newVertIndices[i])
                {
                    meshTriangles[j] = (short)i;
                    break;
                }
                else if (meshTriangles[j] >= uniqueVertices.Count)
                {
                    //meshTriangles[j] = -1;
                }
            }
        }

        //tCount = 0;
        //for (int j = 0; j < vertexCount; j++, tCount++)
        //{
        //    if (meshTriangles[j] >= uniqueVertices.Count)
        //    {
        //        meshTriangles[j] = -1;
        //        break;
        //    }
        //}

        //for (int i = 0; i < tCount; i++)
        //{
        //    if (meshTriangles[i] >= uniqueVertices.Count)
        //    {
        //        meshTriangles[i] = 0;
        //    }
        //}


        //print("Old Vert Count: " + (vertexCount) + ", New Vert Count: " + uniqueVertices.Count);        

        vertexCount = uniqueVertices.Count;
        indicesCount = tCount;

        firstClose.Dispose();
    }

    #endregion

    #region Density Methods

    public void UpdateDensityValues()
    {
        int numPoints = NumPoints;
        int pointsPerAxis = NumPointsPerAxis;

        if (densityPoints != null && densityPoints.IsCreated)
        {
            densityPoints.Dispose();
        }

        densityValues = new float[numPoints];
        densityPoints = new NativeArray<Vector4>(numPoints, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

        modifedDensityValues = new float[numPoints];

        int index = 0;
        Vector3 center = new Vector3(pointsPerAxis, pointsPerAxis, pointsPerAxis) / 2f;
        float HeightMultiplier = 0.5f;
        float Radius = 5;
        float density = 0;

        if(densityGenerator != null)
        {
            densityGenerator.noiseData.chunkSize = pointsPerAxis;
            densityGenerator.UpdateData();
            densityGenerator.densityValuesBuffer.GetData(densityValues);
        }
        else
        {
            for (int i = 0; i < numPoints; i++)
            {
                densityValues[i] = Random.Range(-1, 1);
            }

            for (int x = 0; x < pointsPerAxis; x++)
            {
                for (int y = 0; y < pointsPerAxis; y++)
                {
                    for (int z = 0; z < pointsPerAxis; z++)
                    {
                        index = x + y * pointsPerAxis + z * pointsPerAxis * pointsPerAxis;

                        Vector3 point3D = new Vector3(x, y, z) * voxelScale;
                        float dist = Vector3.Distance(center, point3D);

                        float height = Random.Range(-1, 1) * HeightMultiplier;
                        height = 0;
                        density = 1 - (dist / (Radius + height));

                        if (x < y)
                        {
                            densityValues[index] = 1;
                        }
                        else
                        {
                            densityValues[index] = -1;
                        }

                        densityValues[index] = density;

                        densityPoints[index] = new Vector4(x * voxelScale, y * voxelScale, z * voxelScale, density);
                    }
                }
            }
        }

        for (int x = 0; x < pointsPerAxis; x++)
        {
            for (int y = 0; y < pointsPerAxis; y++)
            {
                for (int z = 0; z < pointsPerAxis; z++)
                {
                    index = x + y * pointsPerAxis + z * pointsPerAxis * pointsPerAxis;
                    densityPoints[index] = new Vector4(x * voxelScale, y * voxelScale, z * voxelScale, densityValues[index]);                    
                }
            }
        }
    }    

    private void ApplyModifiedDensityValues()
    {
        int numPoints = NumPoints;
        int pointsPerAxis = NumPointsPerAxis;
        int index = 0;
        float density = 0f;

        if(modifedDensityValues?.Length != numPoints)
        {
            Debug.LogError("Error modified values array is not inititalized");
            return;
        }

        for (int x = 0; x < pointsPerAxis; x++)
        {
            for (int y = 0; y < pointsPerAxis; y++)
            {
                for (int z = 0; z < pointsPerAxis; z++)
                {
                    index = x + y * pointsPerAxis + z * pointsPerAxis * pointsPerAxis;
                    density = Mathf.Clamp(densityValues[index] + modifedDensityValues[index], -1, 1);
                    densityPoints[index] = new Vector4(x * voxelScale, y * voxelScale, z * voxelScale, density);
                }
            }
        }
    }

    public void ModifyDenisty(Vector3Int localPos, Vector3Int offset, float value)
    {
        int numPoints = NumPoints;
        int pointsPerAxis = NumPointsPerAxis;
        Vector3Int pos = localPos + offset;
        int index = pos.x + pos.y * pointsPerAxis + pos.z * pointsPerAxis * pointsPerAxis;

        if(index >= numPoints || index < 0)
        {
            Debug.LogError("Out of bounds");
            return;
        }

        modifedDensityValues[index] = value;

        denistyHasChanged = true;
    }

    public void ModifyDenisty(Vector3Int[] localPosArr, Vector3Int offset, float[] values)
    {
        int numPoints = NumPoints;
        int pointsPerAxis = NumPointsPerAxis;
        int index = 0;// localPos.x + localPos.y * pointsPerAxis + localPos.z * pointsPerAxis * pointsPerAxis;

        int length = Mathf.Min(localPosArr.Length, values.Length);
        Vector3Int pos;

        for (int i = 0; i < length; i++)
        {
            pos = localPosArr[i] + offset;

            index = pos.x + pos.y * pointsPerAxis + pos.z * pointsPerAxis * pointsPerAxis;

            if(index >= numPoints || index < 0)
            {
                continue;
            }

            modifedDensityValues[index] = values[i];
        }

        denistyHasChanged = true;
    }

    #endregion

    #region Editing Methods

    public Vector3Int GetLocalVoxelPos(Vector3 globalPos)
    {
        Vector3Int pos;

        Vector3 localPos = globalPos - transform.position;

        pos = new Vector3Int(
            Mathf.FloorToInt(localPos.x / voxelScale),
            Mathf.FloorToInt(localPos.y / voxelScale),
            Mathf.FloorToInt(localPos.z / voxelScale));

        pos = new Vector3Int(
            Mathf.Clamp(pos.x, 0, numVoxelsPerAxis - 1),
            Mathf.Clamp(pos.y, 0, numVoxelsPerAxis - 1),
            Mathf.Clamp(pos.z, 0, numVoxelsPerAxis - 1));

        return pos;

    }

    #endregion

    public void DisposeOfNativeArrays()
    {
        if (meshVertices != null && meshVertices.IsCreated)
        {
            meshVertices.Dispose();
        }

        if (meshTriangles != null && meshTriangles.IsCreated)
        {
            meshTriangles.Dispose();
        }

        if (meshNormals != null && meshNormals.IsCreated)
        {
            meshNormals.Dispose();
        }

        if (meshColors != null && meshColors.IsCreated)
        {
            meshColors.Dispose();
        }

        if (densityPoints != null && densityPoints.IsCreated)
        {
            densityPoints.Dispose();
        }
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos)
        {
            return;
        }

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position + new Vector3(numVoxelsPerAxis, numVoxelsPerAxis, numVoxelsPerAxis) / 2f * voxelScale, 
            new Vector3(numVoxelsPerAxis, numVoxelsPerAxis, numVoxelsPerAxis) * voxelScale);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
        {
            return;
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position + new Vector3(numVoxelsPerAxis, numVoxelsPerAxis, numVoxelsPerAxis) / 2f * voxelScale, 
            new Vector3(numVoxelsPerAxis, numVoxelsPerAxis, numVoxelsPerAxis) * 0.95f * voxelScale);
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(MarchingCubeMesh))]
[CanEditMultipleObjects]
public class JMarchingCubesEditor : Editor
{
    MarchingCubeMesh cubes;
    static bool autoUpdate = false;

    private void OnEnable()
    {
        cubes = target as MarchingCubeMesh;
    }

    public override void OnInspectorGUI()
    {
        GUILayout.Label("Editor Vars", EditorStyles.boldLabel);
        MarchingCubeMesh.drawGizmos = EditorGUILayout.Toggle("Draw Gizmos", MarchingCubeMesh.drawGizmos);
        GUILayout.Space(10);

        base.OnInspectorGUI();

        GUILayout.Space(10);

        GUILayout.BeginHorizontal();

        autoUpdate = GUILayout.Toggle(autoUpdate, new GUIContent("Auto"));

        if (GUILayout.Button("Update") || (autoUpdate && GUI.changed))
        {
            cubes.UpdateMesh();
        }
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Randomize Density "))
        {
            cubes.UpdateDensityValues();
            cubes.UpdateMesh();
        }
    }
}

#endif

public static class MarchTables
{
    // Values from http://paulbourke.net/geometry/polygonise/

    public static readonly int[] Edges = new int[256]
    {
    0x0,
    0x109,
    0x203,
    0x30a,
    0x406,
    0x50f,
    0x605,
    0x70c,
    0x80c,
    0x905,
    0xa0f,
    0xb06,
    0xc0a,
    0xd03,
    0xe09,
    0xf00,
    0x190,
    0x99,
    0x393,
    0x29a,
    0x596,
    0x49f,
    0x795,
    0x69c,
    0x99c,
    0x895,
    0xb9f,
    0xa96,
    0xd9a,
    0xc93,
    0xf99,
    0xe90,
    0x230,
    0x339,
    0x33,
    0x13a,
    0x636,
    0x73f,
    0x435,
    0x53c,
    0xa3c,
    0xb35,
    0x83f,
    0x936,
    0xe3a,
    0xf33,
    0xc39,
    0xd30,
    0x3a0,
    0x2a9,
    0x1a3,
    0xaa,
    0x7a6,
    0x6af,
    0x5a5,
    0x4ac,
    0xbac,
    0xaa5,
    0x9af,
    0x8a6,
    0xfaa,
    0xea3,
    0xda9,
    0xca0,
    0x460,
    0x569,
    0x663,
    0x76a,
    0x66,
    0x16f,
    0x265,
    0x36c,
    0xc6c,
    0xd65,
    0xe6f,
    0xf66,
    0x86a,
    0x963,
    0xa69,
    0xb60,
    0x5f0,
    0x4f9,
    0x7f3,
    0x6fa,
    0x1f6,
    0xff,
    0x3f5,
    0x2fc,
    0xdfc,
    0xcf5,
    0xfff,
    0xef6,
    0x9fa,
    0x8f3,
    0xbf9,
    0xaf0,
    0x650,
    0x759,
    0x453,
    0x55a,
    0x256,
    0x35f,
    0x55,
    0x15c,
    0xe5c,
    0xf55,
    0xc5f,
    0xd56,
    0xa5a,
    0xb53,
    0x859,
    0x950,
    0x7c0,
    0x6c9,
    0x5c3,
    0x4ca,
    0x3c6,
    0x2cf,
    0x1c5,
    0xcc,
    0xfcc,
    0xec5,
    0xdcf,
    0xcc6,
    0xbca,
    0xac3,
    0x9c9,
    0x8c0,
    0x8c0,
    0x9c9,
    0xac3,
    0xbca,
    0xcc6,
    0xdcf,
    0xec5,
    0xfcc,
    0xcc,
    0x1c5,
    0x2cf,
    0x3c6,
    0x4ca,
    0x5c3,
    0x6c9,
    0x7c0,
    0x950,
    0x859,
    0xb53,
    0xa5a,
    0xd56,
    0xc5f,
    0xf55,
    0xe5c,
    0x15c,
    0x55,
    0x35f,
    0x256,
    0x55a,
    0x453,
    0x759,
    0x650,
    0xaf0,
    0xbf9,
    0x8f3,
    0x9fa,
    0xef6,
    0xfff,
    0xcf5,
    0xdfc,
    0x2fc,
    0x3f5,
    0xff,
    0x1f6,
    0x6fa,
    0x7f3,
    0x4f9,
    0x5f0,
    0xb60,
    0xa69,
    0x963,
    0x86a,
    0xf66,
    0xe6f,
    0xd65,
    0xc6c,
    0x36c,
    0x265,
    0x16f,
    0x66,
    0x76a,
    0x663,
    0x569,
    0x460,
    0xca0,
    0xda9,
    0xea3,
    0xfaa,
    0x8a6,
    0x9af,
    0xaa5,
    0xbac,
    0x4ac,
    0x5a5,
    0x6af,
    0x7a6,
    0xaa,
    0x1a3,
    0x2a9,
    0x3a0,
    0xd30,
    0xc39,
    0xf33,
    0xe3a,
    0x936,
    0x83f,
    0xb35,
    0xa3c,
    0x53c,
    0x435,
    0x73f,
    0x636,
    0x13a,
    0x33,
    0x339,
    0x230,
    0xe90,
    0xf99,
    0xc93,
    0xd9a,
    0xa96,
    0xb9f,
    0x895,
    0x99c,
    0x69c,
    0x795,
    0x49f,
    0x596,
    0x29a,
    0x393,
    0x99,
    0x190,
    0xf00,
    0xe09,
    0xd03,
    0xc0a,
    0xb06,
    0xa0f,
    0x905,
    0x80c,
    0x70c,
    0x605,
    0x50f,
    0x406,
    0x30a,
    0x203,
    0x109,
    0x0
};

    public static readonly int[][] Triangulation = new int[256][]
    {
	    new int[16] { -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[16] { 0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[16] { 0, 1, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[16] { 1, 8, 3, 9, 8, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[16] { 1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[16] { 0, 8, 3, 1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[16] { 9, 2, 10, 0, 2, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[16] { 2, 8, 3, 2, 10, 8, 10, 9, 8, -1, -1, -1, -1, -1, -1, -1 },
        new int[16] { 3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[16] { 0, 11, 2, 8, 11, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[16] { 1, 9, 0, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[16] { 1, 11, 2, 1, 9, 11, 9, 8, 11, -1, -1, -1, -1, -1, -1, -1 },
        new int[16] { 3, 10, 1, 11, 10, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        new int[16] { 0, 10, 1, 0, 8, 10, 8, 11, 10, -1, -1, -1, -1, -1, -1, -1 },
        new int[16] { 3, 9, 0, 3, 11, 9, 11, 10, 9, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 9, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 4, 3, 0, 7, 3, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 0, 1, 9, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 4, 1, 9, 4, 7, 1, 7, 3, 1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 1, 2, 10, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 3, 4, 7, 3, 0, 4, 1, 2, 10, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 9, 2, 10, 9, 0, 2, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 2, 10, 9, 2, 9, 7, 2, 7, 3, 7, 9, 4, -1, -1, -1, -1 },
	    new int[16] { 8, 4, 7, 3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 11, 4, 7, 11, 2, 4, 2, 0, 4, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 9, 0, 1, 8, 4, 7, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 4, 7, 11, 9, 4, 11, 9, 11, 2, 9, 2, 1, -1, -1, -1, -1 },
	    new int[16] { 3, 10, 1, 3, 11, 10, 7, 8, 4, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 1, 11, 10, 1, 4, 11, 1, 0, 4, 7, 11, 4, -1, -1, -1, -1 },
	    new int[16] { 4, 7, 8, 9, 0, 11, 9, 11, 10, 11, 0, 3, -1, -1, -1, -1 },
	    new int[16] { 4, 7, 11, 4, 11, 9, 9, 11, 10, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 9, 5, 4, 0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 0, 5, 4, 1, 5, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 8, 5, 4, 8, 3, 5, 3, 1, 5, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 1, 2, 10, 9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 3, 0, 8, 1, 2, 10, 4, 9, 5, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 5, 2, 10, 5, 4, 2, 4, 0, 2, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 2, 10, 5, 3, 2, 5, 3, 5, 4, 3, 4, 8, -1, -1, -1, -1 },
	    new int[16] { 9, 5, 4, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 0, 11, 2, 0, 8, 11, 4, 9, 5, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 0, 5, 4, 0, 1, 5, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 2, 1, 5, 2, 5, 8, 2, 8, 11, 4, 8, 5, -1, -1, -1, -1 },
	    new int[16] { 10, 3, 11, 10, 1, 3, 9, 5, 4, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 4, 9, 5, 0, 8, 1, 8, 10, 1, 8, 11, 10, -1, -1, -1, -1 },
	    new int[16] { 5, 4, 0, 5, 0, 11, 5, 11, 10, 11, 0, 3, -1, -1, -1, -1 },
	    new int[16] { 5, 4, 8, 5, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 9, 7, 8, 5, 7, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 9, 3, 0, 9, 5, 3, 5, 7, 3, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 0, 7, 8, 0, 1, 7, 1, 5, 7, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 1, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 9, 7, 8, 9, 5, 7, 10, 1, 2, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 10, 1, 2, 9, 5, 0, 5, 3, 0, 5, 7, 3, -1, -1, -1, -1 },
	    new int[16] { 8, 0, 2, 8, 2, 5, 8, 5, 7, 10, 5, 2, -1, -1, -1, -1 },
	    new int[16] { 2, 10, 5, 2, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 7, 9, 5, 7, 8, 9, 3, 11, 2, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 9, 5, 7, 9, 7, 2, 9, 2, 0, 2, 7, 11, -1, -1, -1, -1 },
	    new int[16] { 2, 3, 11, 0, 1, 8, 1, 7, 8, 1, 5, 7, -1, -1, -1, -1 },
	    new int[16] { 11, 2, 1, 11, 1, 7, 7, 1, 5, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 9, 5, 8, 8, 5, 7, 10, 1, 3, 10, 3, 11, -1, -1, -1, -1 },
	    new int[16] { 5, 7, 0, 5, 0, 9, 7, 11, 0, 1, 0, 10, 11, 10, 0, -1 },
	    new int[16] { 11, 10, 0, 11, 0, 3, 10, 5, 0, 8, 0, 7, 5, 7, 0, -1 },
	    new int[16] { 11, 10, 5, 7, 11, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 0, 8, 3, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 9, 0, 1, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 1, 8, 3, 1, 9, 8, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 1, 6, 5, 2, 6, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 1, 6, 5, 1, 2, 6, 3, 0, 8, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 9, 6, 5, 9, 0, 6, 0, 2, 6, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 5, 9, 8, 5, 8, 2, 5, 2, 6, 3, 2, 8, -1, -1, -1, -1 },
	    new int[16] { 2, 3, 11, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 11, 0, 8, 11, 2, 0, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 0, 1, 9, 2, 3, 11, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 5, 10, 6, 1, 9, 2, 9, 11, 2, 9, 8, 11, -1, -1, -1, -1 },
	    new int[16] { 6, 3, 11, 6, 5, 3, 5, 1, 3, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 0, 8, 11, 0, 11, 5, 0, 5, 1, 5, 11, 6, -1, -1, -1, -1 },
	    new int[16] { 3, 11, 6, 0, 3, 6, 0, 6, 5, 0, 5, 9, -1, -1, -1, -1 },
	    new int[16] { 6, 5, 9, 6, 9, 11, 11, 9, 8, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 5, 10, 6, 4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 4, 3, 0, 4, 7, 3, 6, 5, 10, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 1, 9, 0, 5, 10, 6, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 10, 6, 5, 1, 9, 7, 1, 7, 3, 7, 9, 4, -1, -1, -1, -1 },
	    new int[16] { 6, 1, 2, 6, 5, 1, 4, 7, 8, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 1, 2, 5, 5, 2, 6, 3, 0, 4, 3, 4, 7, -1, -1, -1, -1 },
	    new int[16] { 8, 4, 7, 9, 0, 5, 0, 6, 5, 0, 2, 6, -1, -1, -1, -1 },
	    new int[16] { 7, 3, 9, 7, 9, 4, 3, 2, 9, 5, 9, 6, 2, 6, 9, -1 },
	    new int[16] { 3, 11, 2, 7, 8, 4, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 5, 10, 6, 4, 7, 2, 4, 2, 0, 2, 7, 11, -1, -1, -1, -1 },
	    new int[16] { 0, 1, 9, 4, 7, 8, 2, 3, 11, 5, 10, 6, -1, -1, -1, -1 },
	    new int[16] { 9, 2, 1, 9, 11, 2, 9, 4, 11, 7, 11, 4, 5, 10, 6, -1 },
	    new int[16] { 8, 4, 7, 3, 11, 5, 3, 5, 1, 5, 11, 6, -1, -1, -1, -1 },
	    new int[16] { 5, 1, 11, 5, 11, 6, 1, 0, 11, 7, 11, 4, 0, 4, 11, -1 },
	    new int[16] { 0, 5, 9, 0, 6, 5, 0, 3, 6, 11, 6, 3, 8, 4, 7, -1 },
	    new int[16] { 6, 5, 9, 6, 9, 11, 4, 7, 9, 7, 11, 9, -1, -1, -1, -1 },
	    new int[16] { 10, 4, 9, 6, 4, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 4, 10, 6, 4, 9, 10, 0, 8, 3, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 10, 0, 1, 10, 6, 0, 6, 4, 0, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 8, 3, 1, 8, 1, 6, 8, 6, 4, 6, 1, 10, -1, -1, -1, -1 },
	    new int[16] { 1, 4, 9, 1, 2, 4, 2, 6, 4, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 3, 0, 8, 1, 2, 9, 2, 4, 9, 2, 6, 4, -1, -1, -1, -1 },
	    new int[16] { 0, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 8, 3, 2, 8, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 10, 4, 9, 10, 6, 4, 11, 2, 3, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 0, 8, 2, 2, 8, 11, 4, 9, 10, 4, 10, 6, -1, -1, -1, -1 },
	    new int[16] { 3, 11, 2, 0, 1, 6, 0, 6, 4, 6, 1, 10, -1, -1, -1, -1 },
	    new int[16] { 6, 4, 1, 6, 1, 10, 4, 8, 1, 2, 1, 11, 8, 11, 1, -1 },
	    new int[16] { 9, 6, 4, 9, 3, 6, 9, 1, 3, 11, 6, 3, -1, -1, -1, -1 },
	    new int[16] { 8, 11, 1, 8, 1, 0, 11, 6, 1, 9, 1, 4, 6, 4, 1, -1 },
	    new int[16] { 3, 11, 6, 3, 6, 0, 0, 6, 4, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 6, 4, 8, 11, 6, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 7, 10, 6, 7, 8, 10, 8, 9, 10, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 0, 7, 3, 0, 10, 7, 0, 9, 10, 6, 7, 10, -1, -1, -1, -1 },
	    new int[16] { 10, 6, 7, 1, 10, 7, 1, 7, 8, 1, 8, 0, -1, -1, -1, -1 },
	    new int[16] { 10, 6, 7, 10, 7, 1, 1, 7, 3, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 1, 2, 6, 1, 6, 8, 1, 8, 9, 8, 6, 7, -1, -1, -1, -1 },
	    new int[16] { 2, 6, 9, 2, 9, 1, 6, 7, 9, 0, 9, 3, 7, 3, 9, -1 },
	    new int[16] { 7, 8, 0, 7, 0, 6, 6, 0, 2, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 7, 3, 2, 6, 7, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 2, 3, 11, 10, 6, 8, 10, 8, 9, 8, 6, 7, -1, -1, -1, -1 },
	    new int[16] { 2, 0, 7, 2, 7, 11, 0, 9, 7, 6, 7, 10, 9, 10, 7, -1 },
	    new int[16] { 1, 8, 0, 1, 7, 8, 1, 10, 7, 6, 7, 10, 2, 3, 11, -1 },
	    new int[16] { 11, 2, 1, 11, 1, 7, 10, 6, 1, 6, 7, 1, -1, -1, -1, -1 },
	    new int[16] { 8, 9, 6, 8, 6, 7, 9, 1, 6, 11, 6, 3, 1, 3, 6, -1 },
	    new int[16] { 0, 9, 1, 11, 6, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 7, 8, 0, 7, 0, 6, 3, 11, 0, 11, 6, 0, -1, -1, -1, -1 },
	    new int[16] { 7, 11, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 3, 0, 8, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 0, 1, 9, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 8, 1, 9, 8, 3, 1, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 10, 1, 2, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 1, 2, 10, 3, 0, 8, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 2, 9, 0, 2, 10, 9, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 6, 11, 7, 2, 10, 3, 10, 8, 3, 10, 9, 8, -1, -1, -1, -1 },
	    new int[16] { 7, 2, 3, 6, 2, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 7, 0, 8, 7, 6, 0, 6, 2, 0, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 2, 7, 6, 2, 3, 7, 0, 1, 9, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 1, 6, 2, 1, 8, 6, 1, 9, 8, 8, 7, 6, -1, -1, -1, -1 },
	    new int[16] { 10, 7, 6, 10, 1, 7, 1, 3, 7, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 10, 7, 6, 1, 7, 10, 1, 8, 7, 1, 0, 8, -1, -1, -1, -1 },
	    new int[16] { 0, 3, 7, 0, 7, 10, 0, 10, 9, 6, 10, 7, -1, -1, -1, -1 },
	    new int[16] { 7, 6, 10, 7, 10, 8, 8, 10, 9, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 6, 8, 4, 11, 8, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 3, 6, 11, 3, 0, 6, 0, 4, 6, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 8, 6, 11, 8, 4, 6, 9, 0, 1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 9, 4, 6, 9, 6, 3, 9, 3, 1, 11, 3, 6, -1, -1, -1, -1 },
	    new int[16] { 6, 8, 4, 6, 11, 8, 2, 10, 1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 1, 2, 10, 3, 0, 11, 0, 6, 11, 0, 4, 6, -1, -1, -1, -1 },
	    new int[16] { 4, 11, 8, 4, 6, 11, 0, 2, 9, 2, 10, 9, -1, -1, -1, -1 },
	    new int[16] { 10, 9, 3, 10, 3, 2, 9, 4, 3, 11, 3, 6, 4, 6, 3, -1 },
	    new int[16] { 8, 2, 3, 8, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 0, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 1, 9, 0, 2, 3, 4, 2, 4, 6, 4, 3, 8, -1, -1, -1, -1 },
	    new int[16] { 1, 9, 4, 1, 4, 2, 2, 4, 6, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 8, 1, 3, 8, 6, 1, 8, 4, 6, 6, 10, 1, -1, -1, -1, -1 },
	    new int[16] { 10, 1, 0, 10, 0, 6, 6, 0, 4, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 4, 6, 3, 4, 3, 8, 6, 10, 3, 0, 3, 9, 10, 9, 3, -1 },
	    new int[16] { 10, 9, 4, 6, 10, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 4, 9, 5, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 0, 8, 3, 4, 9, 5, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 5, 0, 1, 5, 4, 0, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 11, 7, 6, 8, 3, 4, 3, 5, 4, 3, 1, 5, -1, -1, -1, -1 },
	    new int[16] { 9, 5, 4, 10, 1, 2, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 6, 11, 7, 1, 2, 10, 0, 8, 3, 4, 9, 5, -1, -1, -1, -1 },
	    new int[16] { 7, 6, 11, 5, 4, 10, 4, 2, 10, 4, 0, 2, -1, -1, -1, -1 },
	    new int[16] { 3, 4, 8, 3, 5, 4, 3, 2, 5, 10, 5, 2, 11, 7, 6, -1 },
	    new int[16] { 7, 2, 3, 7, 6, 2, 5, 4, 9, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 9, 5, 4, 0, 8, 6, 0, 6, 2, 6, 8, 7, -1, -1, -1, -1 },
	    new int[16] { 3, 6, 2, 3, 7, 6, 1, 5, 0, 5, 4, 0, -1, -1, -1, -1 },
	    new int[16] { 6, 2, 8, 6, 8, 7, 2, 1, 8, 4, 8, 5, 1, 5, 8, -1 },
	    new int[16] { 9, 5, 4, 10, 1, 6, 1, 7, 6, 1, 3, 7, -1, -1, -1, -1 },
	    new int[16] { 1, 6, 10, 1, 7, 6, 1, 0, 7, 8, 7, 0, 9, 5, 4, -1 },
	    new int[16] { 4, 0, 10, 4, 10, 5, 0, 3, 10, 6, 10, 7, 3, 7, 10, -1 },
	    new int[16] { 7, 6, 10, 7, 10, 8, 5, 4, 10, 4, 8, 10, -1, -1, -1, -1 },
	    new int[16] { 6, 9, 5, 6, 11, 9, 11, 8, 9, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 3, 6, 11, 0, 6, 3, 0, 5, 6, 0, 9, 5, -1, -1, -1, -1 },
	    new int[16] { 0, 11, 8, 0, 5, 11, 0, 1, 5, 5, 6, 11, -1, -1, -1, -1 },
	    new int[16] { 6, 11, 3, 6, 3, 5, 5, 3, 1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 1, 2, 10, 9, 5, 11, 9, 11, 8, 11, 5, 6, -1, -1, -1, -1 },
	    new int[16] { 0, 11, 3, 0, 6, 11, 0, 9, 6, 5, 6, 9, 1, 2, 10, -1 },
	    new int[16] { 11, 8, 5, 11, 5, 6, 8, 0, 5, 10, 5, 2, 0, 2, 5, -1 },
	    new int[16] { 6, 11, 3, 6, 3, 5, 2, 10, 3, 10, 5, 3, -1, -1, -1, -1 },
	    new int[16] { 5, 8, 9, 5, 2, 8, 5, 6, 2, 3, 8, 2, -1, -1, -1, -1 },
	    new int[16] { 9, 5, 6, 9, 6, 0, 0, 6, 2, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 1, 5, 8, 1, 8, 0, 5, 6, 8, 3, 8, 2, 6, 2, 8, -1 },
	    new int[16] { 1, 5, 6, 2, 1, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 1, 3, 6, 1, 6, 10, 3, 8, 6, 5, 6, 9, 8, 9, 6, -1 },
	    new int[16] { 10, 1, 0, 10, 0, 6, 9, 5, 0, 5, 6, 0, -1, -1, -1, -1 },
	    new int[16] { 0, 3, 8, 5, 6, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 10, 5, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 11, 5, 10, 7, 5, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 11, 5, 10, 11, 7, 5, 8, 3, 0, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 5, 11, 7, 5, 10, 11, 1, 9, 0, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 10, 7, 5, 10, 11, 7, 9, 8, 1, 8, 3, 1, -1, -1, -1, -1 },
	    new int[16] { 11, 1, 2, 11, 7, 1, 7, 5, 1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 0, 8, 3, 1, 2, 7, 1, 7, 5, 7, 2, 11, -1, -1, -1, -1 },
	    new int[16] { 9, 7, 5, 9, 2, 7, 9, 0, 2, 2, 11, 7, -1, -1, -1, -1 },
	    new int[16] { 7, 5, 2, 7, 2, 11, 5, 9, 2, 3, 2, 8, 9, 8, 2, -1 },
	    new int[16] { 2, 5, 10, 2, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 8, 2, 0, 8, 5, 2, 8, 7, 5, 10, 2, 5, -1, -1, -1, -1 },
	    new int[16] { 9, 0, 1, 5, 10, 3, 5, 3, 7, 3, 10, 2, -1, -1, -1, -1 },
	    new int[16] { 9, 8, 2, 9, 2, 1, 8, 7, 2, 10, 2, 5, 7, 5, 2, -1 },
	    new int[16] { 1, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 0, 8, 7, 0, 7, 1, 1, 7, 5, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 9, 0, 3, 9, 3, 5, 5, 3, 7, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 9, 8, 7, 5, 9, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 5, 8, 4, 5, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 5, 0, 4, 5, 11, 0, 5, 10, 11, 11, 3, 0, -1, -1, -1, -1 },
	    new int[16] { 0, 1, 9, 8, 4, 10, 8, 10, 11, 10, 4, 5, -1, -1, -1, -1 },
	    new int[16] { 10, 11, 4, 10, 4, 5, 11, 3, 4, 9, 4, 1, 3, 1, 4, -1 },
	    new int[16] { 2, 5, 1, 2, 8, 5, 2, 11, 8, 4, 5, 8, -1, -1, -1, -1 },
	    new int[16] { 0, 4, 11, 0, 11, 3, 4, 5, 11, 2, 11, 1, 5, 1, 11, -1 },
	    new int[16] { 0, 2, 5, 0, 5, 9, 2, 11, 5, 4, 5, 8, 11, 8, 5, -1 },
	    new int[16] { 9, 4, 5, 2, 11, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 2, 5, 10, 3, 5, 2, 3, 4, 5, 3, 8, 4, -1, -1, -1, -1 },
	    new int[16] { 5, 10, 2, 5, 2, 4, 4, 2, 0, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 3, 10, 2, 3, 5, 10, 3, 8, 5, 4, 5, 8, 0, 1, 9, -1 },
	    new int[16] { 5, 10, 2, 5, 2, 4, 1, 9, 2, 9, 4, 2, -1, -1, -1, -1 },
	    new int[16] { 8, 4, 5, 8, 5, 3, 3, 5, 1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 0, 4, 5, 1, 0, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 8, 4, 5, 8, 5, 3, 9, 0, 5, 0, 3, 5, -1, -1, -1, -1 },
	    new int[16] { 9, 4, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 4, 11, 7, 4, 9, 11, 9, 10, 11, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 0, 8, 3, 4, 9, 7, 9, 11, 7, 9, 10, 11, -1, -1, -1, -1 },
	    new int[16] { 1, 10, 11, 1, 11, 4, 1, 4, 0, 7, 4, 11, -1, -1, -1, -1 },
	    new int[16] { 3, 1, 4, 3, 4, 8, 1, 10, 4, 7, 4, 11, 10, 11, 4, -1 },
	    new int[16] { 4, 11, 7, 9, 11, 4, 9, 2, 11, 9, 1, 2, -1, -1, -1, -1 },
	    new int[16] { 9, 7, 4, 9, 11, 7, 9, 1, 11, 2, 11, 1, 0, 8, 3, -1 },
	    new int[16] { 11, 7, 4, 11, 4, 2, 2, 4, 0, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 11, 7, 4, 11, 4, 2, 8, 3, 4, 3, 2, 4, -1, -1, -1, -1 },
	    new int[16] { 2, 9, 10, 2, 7, 9, 2, 3, 7, 7, 4, 9, -1, -1, -1, -1 },
	    new int[16] { 9, 10, 7, 9, 7, 4, 10, 2, 7, 8, 7, 0, 2, 0, 7, -1 },
	    new int[16] { 3, 7, 10, 3, 10, 2, 7, 4, 10, 1, 10, 0, 4, 0, 10, -1 },
	    new int[16] { 1, 10, 2, 8, 7, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 4, 9, 1, 4, 1, 7, 7, 1, 3, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 4, 9, 1, 4, 1, 7, 0, 8, 1, 8, 7, 1, -1, -1, -1, -1 },
	    new int[16] { 4, 0, 3, 7, 4, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 4, 8, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 9, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 3, 0, 9, 3, 9, 11, 11, 9, 10, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 0, 1, 10, 0, 10, 8, 8, 10, 11, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 3, 1, 10, 11, 3, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 1, 2, 11, 1, 11, 9, 9, 11, 8, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 3, 0, 9, 3, 9, 11, 1, 2, 9, 2, 11, 9, -1, -1, -1, -1 },
	    new int[16] { 0, 2, 11, 8, 0, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 3, 2, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 2, 3, 8, 2, 8, 10, 10, 8, 9, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 9, 10, 2, 0, 9, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 2, 3, 8, 2, 8, 10, 0, 1, 8, 1, 10, 8, -1, -1, -1, -1 },
	    new int[16] { 1, 10, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 1, 3, 8, 9, 1, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 0, 9, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { 0, 3, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
	    new int[16] { -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 }
    };

    public static readonly int[] CornerIndexAFromEdge = new int[12]
    {
        0,
        1,
        2,
        3,
        4,
        5,
        6,
        7,
        0,
        1,
        2,
        3
    };

    public static readonly int[] CornerIndexBFromEdge = new int[12]
        {
        1,
        2,
        3,
        0,
        5,
        6,
        7,
        4,
        4,
        5,
        6,
        7
    };

}
